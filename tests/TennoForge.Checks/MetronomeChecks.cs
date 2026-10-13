using System.Text.Json;
using TennoForge.Core;

internal static class MetronomeChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool b){if(!b)throw new Exception("메트로놈 검증 실패");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-6*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(string name="Quassus",string attack="First Heavy Attack - Ethereal Daggers",MetronomeSetup? s=null){var w=catalog.Weapons.Single(w=>w.EnglishName==name);return new(w.Id,new ModSlot?[8],new(Combo:12,BaseInterval:10,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:attack,ExternalEquipment:new(Metronome:s??new(Opera:true))),Encounter:new(EncounterMode.FiveSeconds));}
        CombatTimelineResult Run(Build b,int n=1){var r=CombatTimeline.Calculate(catalog,b,stances,n);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        Build Flight(MetronomeSetup? m=null)
        {
            var b=B(attack:"Normal Attack",s:m);var contacts=Enumerable.Range(1,16).SelectMany(i=>new[]{new WeaponActionContact(.2+i*.001,Projectile:i),new WeaponActionContact(.4+i*.001,Target:2,Projectile:i,Travel:1)}).ToArray();
            return b with {Encounter=b.Encounter! with {ScriptedAttacksOnly=true,NearbyEnemies=[new("관통 적",1,Defense:new(1e9,0))],WeaponActions=[new(0,.15,Contacts:contacts,IndependentDaggers:true)]}};
        }
        check("메트로놈 위키랭크:오페라12/20/25/30·포르테20/25/25/30·5/8/12/15초",()=>{
            foreach(var rank in Enumerable.Range(0,4)){var s=new MetronomeSetup(rank,2,1.5,Opera:true,Forte:true);Near(MetronomeRules.Opera(s),new[]{.12,.2,.25,.3}[rank]*2);Near(MetronomeRules.Forte(s),new[]{.2,.25,.25,.3}[rank]*2);Near(MetronomeRules.Lifetime(s),new[]{5d,8,12,15}[rank]*1.5);}
        });
        check("멀티샷 분포:12×1.3=15.6·15확정+60%1개·기존정수발사난수보존",()=>{
            Near(MetronomeRules.Sample(15.6,.599),16);Near(MetronomeRules.Sample(15.6,.601),15);Near(MetronomeRules.Sample(10.2,0),11);Near(MetronomeRules.Sample(10.2,.9),10);
            var calls=0;Near(MetronomeRules.Roll(12,()=>{calls++;return .5;}),12);Near(calls,0);
            var average=Enumerable.Range(0,1000).Average(i=>MetronomeRules.Sample(15.6,(i+.5)/1000));Near(average,15.6);
        });
        check("오페라 정적피해:콰서스12/6·프라임18/9·리디머프라임10·칼날제외",()=>{
            foreach(var (name,attack,n) in new[]{("Quassus","First Heavy Attack - Ethereal Daggers",12),("Quassus","Second Heavy Attack - Ethereal Daggers",6),("Quassus Prime","First Heavy Attack - Ethereal Daggers",18),("Quassus Prime","Second Heavy Attack - Ethereal Daggers",9),("Redeemer Prime","Ranged Attack",10)})
            {var b=B(name,attack);var c=MeleeEngine.Calculate(catalog,b);var plain=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {ExternalEquipment=null}});Near(c.Melee!.Projectiles,n*1.3);Near(c.ExpectedHit/plain.ExpectedHit,1.3);Near(c.StatusChance,plain.StatusChance);Near(c.CriticalChance,plain.CriticalChance);}
            var blade=MeleeEngine.Calculate(catalog,B(attack:"Normal Attack"));Near(blade.Melee!.Projectiles,1);
        });
        check("포르테 브라켓:프레셔·컨오와가산·오페라와별도·기본수치불변",()=>{
            var b=B("Skana","Normal Attack",new(Forte:true));var pp=catalog.Mods.Single(m=>m.EnglishName=="Pressure Point");var co=catalog.Mods.Single(m=>m.EnglishName=="Condition Overload");b.Slots[0]=new(pp.Id,pp.MaxRank);b.Slots[1]=new(co.Id,co.MaxRank);b=b with {Melee=b.Melee! with {Statuses=new(){[DamageType.Impact]=1,[DamageType.Slash]=1}}};
            var c=MeleeEngine.Calculate(catalog,b);var plain=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {ExternalEquipment=null}});Near(c.ExpectedHit/plain.ExpectedHit,(1+1.2+1.6+.3)/(1+1.2+1.6));
        });
        check("오페라 실제비행:한발사당확률1회·추가단검관통도같은판정·피해중복배율없음",()=>{
            var r=Run(Flight());var fired=r.Events.Single(e=>e.Kind=="operaVolley").Amount;Yes(fired is 15 or 16);
            Near(r.Events.Count(e=>e.Kind=="weaponProjectileImpact"),fired*2);Near(r.Events.Count(e=>e.Kind=="heavyCombo"),1);
            var b=Flight();var input=b.Encounter!.WeaponActions![0];var single=WeaponActionFlight.Calculate(catalog,b,input,input.Contacts![0]);Near(single.Melee!.Projectiles,1);
        });
        check("오페라 발사시각:입력뒤만료면기본12개·입력뒤획득하면추가단검",()=>{
            var expired=Run(Flight(new(Opera:true,OperaRemaining:.1)));Near(expired.Events.Single(e=>e.Kind=="operaVolley").Amount,12);Near(expired.Events.Count(e=>e.Kind=="weaponProjectileImpact"),24);
            var b=Flight(new(Opera:false));b=b with {Encounter=b.Encounter! with {Inputs=[new(.1,"OperaBuff")]}};var gained=Run(b);Yes(gained.Events.Single(e=>e.Kind=="operaVolley").Amount is 15 or 16);
        });
        check("메트로놈 시간계산:오페라·포르테독립만료/갱신·캐시와화력그래프",()=>{
            var b=B("Redeemer Prime","Ranged Attack",new(Opera:true,Forte:true,OperaRemaining:1,ForteRemaining:2));var r=Run(b);
            var first=r.Points.Single(p=>p.Time==0);var afterOpera=r.Points.Single(p=>p.Time==1);var afterBoth=r.Points.Single(p=>p.Time==2);Near(first.SnapshotDps/afterOpera.SnapshotDps,1.3);Near(afterOpera.SnapshotDps/afterBoth.SnapshotDps,1.3);
            var renewed=Run(b with {Encounter=b.Encounter! with {Inputs=[new(3,"OperaBuff"),new(3,"ForteBuff")]}});Near(renewed.Points.Last().SnapshotDps/afterBoth.SnapshotDps,1.69);
        });
        check("멀티샷 TTK분포:15.6발을16발확정으로반올림하지않음",()=>{
            var c=MeleeEngine.Calculate(catalog,B());c=c with {Damage=new(){[DamageType.Impact]=1},CriticalChance=0,CriticalMultiplier=1,DuplicateChance=0,StatusChance=0,HitInterval=1,Melee=c.Melee! with {Projectiles=15.6,ExtraDamage=0,StealthBonus=0,TargetDamage=new(){[DamageType.Impact]=1}}};
            var r=DamageEngine.Simulate(c,16,4096,12345,1);Yes(r.KillFraction>.56&&r.KillFraction<.64);
        });
        check("메트로놈 타이머:동기화완료사건만갱신·수신버프해제·직렬화",()=>{
            var s=new MetronomeState(new(Opera:true,Forte:true,OperaRemaining:1,ForteRemaining:2));Yes(!s.At(1).Opera&&s.At(1).Forte);s.Receive(3,true);Near(s.At(4).OperaRemaining!.Value,14);Yes(!s.At(4).Forte);s.Clear();Yes(!s.At(4).Opera);
            var b=Flight();var round=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b))!;Yes(round.Melee!.ExternalEquipment==b.Melee!.ExternalEquipment);
        });
    }
}
