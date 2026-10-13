using System.Text.Json;
using TennoForge.Core;

internal static class IncarnonFormChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("인카논 수명 불일치");}
        void Near(double x,double y){if(Math.Abs(x-y)>1e-8*Math.Max(1,Math.Abs(y)))throw new Exception($"{x} != {y}");}
        Build B(string name,double? remaining=null)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==name);return new(w.Id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Incarnon:true,IncarnonSecondsRemaining:remaining),Encounter:new(EncounterMode.TenSeconds));
        }
        check("인카논 전체28프로필:90/180초·5/6콤보·일반슬램3군·사용 중 갱신 없음",()=>{
            var definitions=MeleeRuleCatalog.Load().Incarnons;Yes(definitions.Length==28);
            foreach(var d in definitions){var b=B(catalog.Weapons.Single(w=>w.Id==d.WeaponId).EnglishName);var f=new IncarnonFormState(b);var seconds=d.Code is "Obex" or "Innodem" or "Praedos" or "Ruvox" or "Thalys"?90:180;
                Near(f.Duration,seconds);Yes(!f.TryActivate(10,12,"Heavy Slam Attack"));Near(f.End,seconds);f.Advance(seconds);Yes(!f.Active(seconds));
                var combo=d.Code is "Innodem" or "Praedos"?5:6;Yes(!f.TryActivate(seconds,combo-1,"Heavy Attack"));Yes(f.TryActivate(seconds,combo,"Heavy Attack"));f.Death();
                Yes(f.TryActivate(seconds,combo,"Slam Attack")== (d.Code is "Destreza" or "Obex" or "Thalys"));
                foreach(var perk in d.Perks.Where(p=>p.Effects.ContainsKey("TransformCombo"))){var reduced=new IncarnonFormState(b with {Melee=new(Evolutions:[perk.Id])});Near(reduced.RequiredCombo,3);Yes(reduced.TryActivate(0,3,"Heavy Attack"));}
            }
        });
        check("인카논: 만료 후 일반공속·준비 중 공격 재배치·별도 발동 시각·저장 왕복",()=>{
            var b=B("Praedos",2);b=b with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(1,"IncarnonHeavyInput",5),new(3,"IncarnonHeavyInput",5)])};
            var restored=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Near(restored.Melee!.IncarnonSecondsRemaining!.Value,2);
            var t=CombatTimeline.Calculate(catalog,restored,stances,1);Yes(t.Unsupported.Length==0);Near(t.Points.Single(p=>Math.Abs(p.Time-2.5)<1e-8).IncarnonRemaining!.Value,0);Near(t.Points.Last().IncarnonRemaining!.Value,88);
            Yes(t.Events.Count(e=>e.Kind=="incarnonStart")==1&&t.Events.Count(e=>e.Kind=="incarnonEnd")==1);Near(t.Events.Single(e=>e.Kind=="incarnonEnd").Time,2);
            var hits=t.Events.Where(e=>e.Kind=="hit").Select(e=>e.Time).ToArray();var next=hits.First(x=>x>2);var last=hits.Last(x=>x<2);
            var normal=MeleeEngine.Calculate(catalog,b with {Melee=new()}).AttackSpeed;var active=MeleeEngine.Calculate(catalog,b).AttackSpeed;
            Near(next,2+(1-(2-last)*active)/normal);
        });
        check("데스트레자: 직접 강공격 처치 누적30상한·변신 만료 초기화·다음 변신 독립",()=>{
            var b=B("Destreza Prime",2.5);b=b with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"HeavyMeleeKill"),new(1,"HeavyMeleeKill"),new(3,"IncarnonSlamInput",6),new(3.1,"HeavyMeleeKill")])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);Near(t.Points[0].IncarnonPunctureBonus,.1);Near(t.Points.Single(p=>Math.Abs(p.Time-2)<1e-8).IncarnonPunctureBonus,.2);Near(t.Points.Single(p=>Math.Abs(p.Time-2.5)<1e-8).IncarnonPunctureBonus,0);Near(t.Points.Last().IncarnonPunctureBonus,.1);
            var f=new IncarnonFormState(B("Destreza"));for(var i=0;i<35;i++)f.DirectHeavyKill(0);Near(f.HeavyKills,30);f.Death();Near(f.HeavyKills,0);
        });
        check("인노뎀: 비치명적 마무리도리질리언스·파라존제외·네중첩34.39%·변신만료",()=>{
            var b=B("Innodem",5);b=b with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"FinisherHit"),new(1,"FinisherHit"),new(2,"FinisherHit"),new(3,"FinisherHit"),new(4,"ParazonKill")])};
            var t=CombatTimeline.Calculate(catalog,b,stances,1);Yes(t.Unsupported.Length==0);Near(t.Points[0].IncarnonResilience,.1);Near(t.Points.Single(p=>Math.Abs(p.Time-4)<1e-8).IncarnonResilience,1-Math.Pow(.9,4));Near(t.Points.Last().IncarnonResilience,0);
            var s=stances.ForWeapon(catalog.Weapons.Single(w=>w.Id==b.WeaponId)).First(s=>s.Combos.Any(c=>c.Key=="Finisher"));b=b with {Stance=new(s.Id,"Finisher",stances.Revision),Capacity=CapacityCatalog.Load().Default(b.WeaponId,s.IsDefault?null:s.Id),Melee=b.Melee! with {ComboDurationOverride=2},Encounter=new(EncounterMode.StanceCycle)};
            var sc=StanceEngine.Calculate(catalog,b,stances);b=b with {Encounter=new(EncounterMode.StanceCycle,HitTimes:Enumerable.Repeat(0d,sc.Combo.HitCount).ToArray())};var actual=CombatTimeline.Calculate(catalog,b,stances,1);Yes(actual.Unsupported.Length==0);Near(actual.Points.Last().IncarnonResilience,.1);
        });
    }
}
