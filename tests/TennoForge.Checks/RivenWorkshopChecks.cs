using System.Text.Json;
using TennoForge.Core;

internal static class RivenWorkshopChecks
{
    public static void Run(Action<string,Action> check)
    {
        void Yes(bool b){if(!b)throw new Exception("리벤 작업대 검증 불일치");}
        void Reject(Action a){try{a();}catch(ArgumentException){return;}throw new Exception("잘못된 작업을 허용함");}
        var id=Catalog.LoadMelee().Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id;
        RivenRoll Make(params RivenTrait[] traits)
        {
            var r=new RivenRoll(id,8,traits,RivenValues.Version,BaseDrain:6,Polarity:"AP_ATTACK");
            return r with{Traits=traits.Select(t=>{var range=RivenValues.Range(r,t);return t with{Value=Math.Round((range.Minimum+range.Maximum)/2,1,MidpointRounding.AwayFromZero)};}).ToArray()};
        }
        var roll=Make(new(RivenStat.Damage,0),new(RivenStat.StatusChance,0));
        check("자동 실행: 로그인·게임 실행 전이·수동 종료 후 재실행 억제",()=>{
            foreach(var windows in new[]{false,true})foreach(var game in new[]{false,true})
            {
                var policy=new WFLabs.Client.LaunchPolicy();
                Yes(policy.ShouldLaunch(windows,game,true,false)==windows);
                Yes(!policy.ShouldLaunch(windows,game,false,false));
                Yes(policy.ShouldLaunch(windows,game,false,true)==game);
                for(var i=0;i<10;i++)Yes(!policy.ShouldLaunch(windows,game,false,true));
                Yes(!policy.ShouldLaunch(windows,game,false,false));Yes(policy.ShouldLaunch(windows,game,false,true)==game);
            }
            Yes(new WFLabs.Client.LaunchPolicy().ShouldLaunch(false,true,false,true));
        });
        check("리벤 작업대: 위키 순환 비용 10단계와 고정 2배",()=>{
            var expected=new[]{900,1000,1200,1400,1700,2000,2350,2750,3150,3500};
            for(var i=0;i<expected.Length;i++){Yes(RivenWorkshop.NextCost(i,false)==expected[i]);Yes(RivenWorkshop.NextCost(i,true)==2*expected[i]);}
            Yes(RivenWorkshop.NextCost(999,false)==3500);Yes(RivenWorkshop.Budget(0,9,false)==16450);Yes(RivenWorkshop.Budget(0,10,false)==19950);Yes(RivenWorkshop.Budget(0,10,true)==39900);
            Yes(RivenWorkshop.Budget(int.MaxValue,int.MaxValue,true)==(long)int.MaxValue*7000);
            Reject(()=>RivenWorkshop.Budget(-1,1,false));Reject(()=>RivenWorkshop.NextCost(-1,false));
        });
        check("리벤 작업대: 이전 옵션 유지에도 쿠바·순환 횟수 소비",()=>{
            var pending=RivenWorkshop.CycleExample(new(roll),new(12));Yes(pending.Cycles==1&&pending.KuvaSpent==900&&pending.Current==roll);
            Reject(()=>RivenWorkshop.CycleExample(pending,new(1)));Reject(()=>RivenWorkshop.Lock(pending,RivenStat.Damage));
            var kept=RivenWorkshop.Resolve(pending,false);Yes(kept.Current==roll&&kept.Pending is null&&kept.KuvaSpent==900);
            var accepted=RivenWorkshop.Resolve(pending,true);Yes(accepted.Current==pending.Pending&&accepted.Cycles==1);Reject(()=>RivenWorkshop.Resolve(accepted,true));
        });
        check("리벤 작업대: 일반 옵션 고정은 구성·수치 유지와 비용 2배",()=>{
            var initial=Make(new(RivenStat.CriticalChance,0),new(RivenStat.Damage,0),new(RivenStat.ComboChance,0,true));
            foreach(var locked in initial.Traits)
            {
                var state=RivenWorkshop.Lock(new(initial),locked.Stat);
                for(var i=0;i<30;i++){var next=RivenWorkshop.CycleExample(state,new(i));Yes(next.Pending!.Traits.Contains(locked));Yes(next.Pending.Traits.Length==3&&next.Pending.Traits.Count(t=>t.Negative)==1);Yes(next.KuvaSpent==1800);Yes(next.Pending.Traits[Array.IndexOf(initial.Traits,locked)]==locked);Yes(next.Pending.Traits[^1].Negative);}
            }
            Reject(()=>RivenWorkshop.Lock(new(initial),RivenStat.Heat));
        });
        check("리벤 작업대: 14종 접합은 계승 구간과 원래 옵션 순서 보존",()=>{
            foreach(var recipe in RivenEngine.Recipes)
            {
                var original=Make(new(RivenStat.CriticalDamage,0),new(recipe.A,0),new(recipe.B,0));
                var p=RivenValues.Predict(original).Single(p=>p.Candidate.Result==recipe.Result);
                var s=RivenWorkshop.SpliceExample(new(original),p.Candidate,new(4));
                Yes(s.Current.Traits[0]==original.Traits[0]);Yes(s.Current.Traits[p.Candidate.First].Stat==recipe.Result&&s.SplicersSpent==1);
                var value=s.Current.Traits[p.Candidate.First].Value;Yes(value>=p.Value.Low-.05&&value<=p.Value.High+.05);RivenWorkshop.Validate(s);
                Reject(()=>RivenWorkshop.SpliceExample(s,p.Candidate,new(1)));
            }
        });
        check("리벤 작업대: 음옵 소비 후 대체 옵션도 음옵",()=>{
            var r=Make(new(RivenStat.Damage,0),new(RivenStat.CriticalChance,0),new(RivenStat.StatusChance,0,true));
            var s=RivenWorkshop.SpliceExample(new(r),RivenValues.Predict(r).Single().Candidate,new(9));
            Yes(s.Current.Traits[0].Stat==RivenStat.StatusDamage&&!s.Current.Traits[0].Negative);Yes(s.Current.Traits[2].Negative&&s.Current.Traits[1]==r.Traits[1]);
        });
        check("리벤 작업대: 접합 자동 고정은 무료이며 일반 고정과 공존",()=>{
            var s=RivenWorkshop.SpliceExample(new(roll),RivenValues.Predict(roll).Single().Candidate,new(18));
            var spliced=s.Current.Traits.Single(t=>t.Spliced);var ordinary=s.Current.Traits.Single(t=>!t.Spliced);
            var pending=RivenWorkshop.CycleExample(s,new(3));Yes(pending.KuvaSpent==900&&pending.Pending!.Traits.Contains(spliced));
            var both=RivenWorkshop.CycleExample(RivenWorkshop.Lock(s,ordinary.Stat),new(3));Yes(both.KuvaSpent==1800&&both.Pending!.Traits.Contains(spliced)&&both.Pending.Traits.Contains(ordinary));
            Reject(()=>RivenWorkshop.Lock(s,spliced.Stat));
        });
        check("리벤 작업대: 접합 해제 후 새 결과 채택 시에만 제거",()=>{
            var s=RivenWorkshop.SpliceExample(new(roll),RivenValues.Predict(roll).Single().Candidate,new(1));
            var pending=RivenWorkshop.CycleExample(RivenWorkshop.Preserve(s,false),new(8));
            Yes(!pending.Pending!.Traits.Any(t=>t.Spliced));Yes(RivenWorkshop.Resolve(pending,false).Current.Traits.Any(t=>t.Spliced));
            Yes(!RivenWorkshop.Resolve(pending,true).Current.Traits.Any(t=>t.Spliced));
        });
        check("리벤 작업대: 가정 추첨 1000회 모두 허용 구성·범위",()=>{
            var rng=new Random(482);var s=new RivenWorkshopState(roll);
            for(var i=0;i<1000;i++){s=RivenWorkshop.CycleExample(s,rng);RivenWorkshop.Validate(s);s=RivenWorkshop.Resolve(s,true);}
            Yes(s.KuvaSpent==19950+990L*3500&&s.Cycles==1000);
        });
        check("리벤 작업대: 진행 중 작업 JSON 복원·위조 결과 거부",()=>{
            var s=RivenWorkshop.CycleExample(RivenWorkshop.Lock(new(roll),RivenStat.Damage),new(99));
            var json=JsonSerializer.Serialize(s,Catalog.Json);var restored=JsonSerializer.Deserialize<RivenWorkshopState>(json,Catalog.Json)!;RivenWorkshop.Validate(restored);Yes(JsonSerializer.Serialize(restored,Catalog.Json)==json);
            Reject(()=>RivenWorkshop.Validate(restored with{Pending=restored.Pending! with{WeaponId="invalid"}}));
            Reject(()=>RivenWorkshop.Validate(restored with{Pending=restored.Pending! with{Traits=Make(new(RivenStat.Heat,0),new(RivenStat.Cold,0)).Traits}}));
            Reject(()=>RivenWorkshop.Validate(restored with{KuvaSpent=-1}));Reject(()=>RivenWorkshop.Validate(restored with{Current=null!}));
            Reject(()=>RivenWorkshop.CycleExample(new(roll,int.MaxValue),new(1)));
        });
    }
}
