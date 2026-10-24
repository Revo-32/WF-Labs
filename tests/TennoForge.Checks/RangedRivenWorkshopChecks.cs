using System.Text.Json;
using TennoForge.Core;

internal static class RangedRivenWorkshopChecks
{
    public static void Run(Action<string,Action> check)
    {
        void Yes(bool b){if(!b)throw new Exception("총기 리벤 작업대 불일치");}
        void Reject(Action a){try{a();}catch(ArgumentException){return;}throw new Exception("잘못된 작업 허용");}
        foreach(var name in new[]{"Braton","Strun","Lato"})
        {
            var weapon=RangedEngine.Weapons.Single(w=>w.EnglishName==name);
            RangedRivenRoll Make(params RangedRivenTrait[] traits)
            {
                var roll=new RangedRivenRoll(weapon.Id,8,traits,RivenBalance.Version,10,"Madurai");
                return roll with{Traits=traits.Select(t=>{var r=RangedRivens.Range(roll,t);return t with{Value=Math.Round((r.Minimum+r.Maximum)/2,1,MidpointRounding.AwayFromZero)};}).ToArray()};
            }
            var original=Make(new("Damage",0),new("StatusChance",0),new("Zoom",0,true));
            check(name+" 리벤: 고정 순환·유지·채택·비용·중복 금지",()=>{
                foreach(var trait in original.Traits)for(int seed=0;seed<32;seed++)
                {
                    var state=RangedRivenWorkshop.CycleExample(new(original,Locked:trait.Stat),new(seed));
                    Yes(state.KuvaSpent==1800&&state.Cycles==1&&state.Current==original&&state.Pending!.Traits.Contains(trait));
                    Yes(state.Pending!.Traits.Length==3&&state.Pending.Traits.Count(t=>t.Negative)==1);
                    Yes(state.Pending.Traits[Array.IndexOf(original.Traits,trait)]==trait);RangedRivenWorkshop.Validate(state);
                    Reject(()=>RangedRivenWorkshop.CycleExample(state,new(2)));Reject(()=>RangedRivenWorkshop.Lock(state,null));
                    var keep=RangedRivenWorkshop.Resolve(state,false);Yes(keep.Current==original&&keep.KuvaSpent==1800&&keep.Pending is null);
                    Yes(RangedRivenWorkshop.Resolve(state,true).Current==state.Pending);
                }
            });
            check(name+" 리벤: 모든 접합 조합·계승 구간·접합 유지",()=>{
                foreach(var recipe in RangedRivens.Table.Recipes)
                {
                    var roll=Make(new(recipe.First,0),new(recipe.Second,0));var prediction=RangedRivens.Predict(roll).Single(p=>p.Result==recipe.Result);
                    for(int seed=0;seed<8;seed++)
                    {
                        var state=RangedRivenWorkshop.SpliceExample(new(roll),prediction,new(seed));var splice=state.Current.Traits[prediction.First];
                        Yes(splice.Stat==recipe.Result&&splice.Value>=prediction.Value.Low-.05&&splice.Value<=prediction.Value.High+.05&&state.SplicersSpent==1);
                        var pending=RangedRivenWorkshop.CycleExample(state,new(seed));Yes(pending.Pending!.Traits[prediction.First]==splice);RangedRivenWorkshop.Validate(pending);
                        Reject(()=>RangedRivenWorkshop.SpliceExample(state,prediction,new(0)));Reject(()=>RangedRivenWorkshop.Lock(state,recipe.Result));
                        var reroll=RangedRivenWorkshop.CycleExample(state with{PreserveSplice=false},new(seed));Yes(!reroll.Pending!.Traits.Any(t=>t.Spliced));
                    }
                }
            });
            check(name+" 리벤: 음옵 접합·순서·무작위 음옵 제한",()=>{
                var roll=Make(new("CriticalChance",0),new("Damage",0),new("StatusChance",0,true));var prediction=RangedRivens.Predict(roll).Single();
                for(int seed=0;seed<32;seed++){var state=RangedRivenWorkshop.SpliceExample(new(roll),prediction,new(seed));Yes(state.Current.Traits[0]==roll.Traits[0]&&state.Current.Traits[2].Negative);Yes(!RangedRivens.Attribute(state.Current.Traits[2].Stat).PositiveOnly);}
            });
            check(name+" 리벤: 메타데이터 없는 분석·작업 파일·이전 버전 거부",()=>{
                var roll=original with{BaseDrain=null,Polarity=null};var state=RangedRivenWorkshop.CycleExample(new(roll),new(4));
                var restored=JsonSerializer.Deserialize<RangedRivenWorkshopState>(JsonSerializer.Serialize(state,Catalog.Json),Catalog.Json)!;RangedRivenWorkshop.Validate(restored);
                Yes(restored.Current.Traits.SequenceEqual(state.Current.Traits)&&restored.Pending!.Traits.SequenceEqual(state.Pending!.Traits));Reject(()=>RangedRivens.Validate(roll));
                Reject(()=>RangedRivenWorkshop.Validate(state with{Current=roll with{Version="old"}}));
                Reject(()=>RangedRivenWorkshop.Validate(state with{Pending=state.Pending! with{Rank=7}}));Reject(()=>RangedRivenWorkshop.Validate(state with{KuvaSpent=-1}));
                Reject(()=>RangedRivenWorkshop.CycleExample(new(original,Cycles:int.MaxValue),new(1)));
            });
            check(name+" 리벤: 전투 비교 동일 시드·입력 불변·통계 일치",()=>{
                var enemy=EnemyCatalog.Load().Enemies.First(e=>e.SupportsEximus);var build=new RangedBuild(weapon.Id,0,new ModSlot?[8],Enumerable.Repeat("None",8).ToArray(),Enemy:new(enemy.Id,200));
                var settings=new RangedEncounter(5,16);var result=RangedRivenComparison.Calculate(build,original,original,3,settings);
                Yes(result.Baseline.Dps==result.Candidate.Dps&&result.Baseline.Ttk==result.Candidate.Ttk);Yes(result.Baseline.Points.SequenceEqual(result.Candidate.Points));Yes(build.Riven is null&&build.Slots.All(s=>s is null));
                var equipped=RangedRivenComparison.Equip(build,original,3);var moved=RangedRivenComparison.Equip(equipped,original,4);Yes(moved.Slots[3] is null&&moved.Slots[4] is not null);
                Reject(()=>RangedRivenComparison.Equip(build,original with{WeaponId="unknown"},3));Reject(()=>RangedRivenComparison.Equip(build,original,8));
                using var cts=new CancellationTokenSource();cts.Cancel();try{RangedRivenComparison.Calculate(build,original,original,3,settings,cts.Token);throw new Exception("취소 누락");}catch(OperationCanceledException){}
            });
        }
    }
}
