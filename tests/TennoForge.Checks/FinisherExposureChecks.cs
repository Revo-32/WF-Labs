using TennoForge.Core;

internal static class FinisherExposureChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("마무리 노출/수면 불일치");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-7*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Unknown(Action a){try{a();}catch(CombatRuleUnavailableException){return;}throw new Exception("원문 미기재 전이를 확정함");}
        Build B(string id=ShieldThrowMechanic.CobraPrime)=>new(id,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Conditions:new(){["Baruuk"]=1}),Encounter:new(EncounterMode.FiveSeconds));
        CombatTimelineResult Run(Build b,int samples=1){var r=CombatTimeline.Calculate(catalog,b,stances,samples);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        check("마무리 노출:2초 수면·면역/오버가드·조기 기상은 알려진 만료와 구분",()=>{
            var s=new FinisherExposureState();Yes(!s.Apply(0,2,true,1)&&!s.Apply(0,2,true,0,true));Yes(s.Apply(0,2,true,0));Yes(s.Active(0)&&s.Active(1.999)&&!s.Active(2));
            Yes(s.Damage(1));Unknown(()=>s.EnsureKnown(1));s.EnsureKnown(2);Yes(!s.Active(2));
        });
        check("방패 마무리 노출:미지정 수명과 충격/전기 덮어쓰기·임의1초 없음",()=>{
            var s=new FinisherExposureState();s.Apply(1,null,false,0);s.EnsureKnown(1);Unknown(()=>s.EnsureKnown(1.001));Yes(!s.Status(DamageType.Viral,1.1));Yes(s.Status(DamageType.Impact,1.2));s.EnsureKnown(1.3);Yes(!s.Active(1.3));
            s.Apply(2,.5,false,0);Yes(s.Active(2.4)&&!s.Active(2.5));Yes(!s.Damage(2.1));Yes(s.Status(DamageType.Electricity,2.2));
        });
        check("마무리 노출:은신700%와 치명타가 가산·컨오 종류는 증가하지 않음",()=>{
            var b=B();var plain=MeleeEngine.Calculate(catalog,b);var exposed=b with {Melee=b.Melee! with {Conditions=new(){["FinisherExposed"]=1}}};var c=MeleeEngine.Calculate(catalog,exposed);
            Near(c.ExpectedHit,plain.Damage.Values.Sum()*(8+plain.CriticalChance*(plain.CriticalMultiplier-1)));Near(TargetStatusRules.Count(exposed),0);
            Near(MeleeEngine.Calculate(catalog,exposed with {Melee=exposed.Melee with {Overguard=1}}).ExpectedHit,plain.ExpectedHit);
        });
        check("코브라 수면:바루크 첫타 후 발동·최초 피해에700% 소급하지 않음·생존 적만",()=>{
            foreach(var id in new[]{ShieldThrowMechanic.Cobra,ShieldThrowMechanic.CobraPrime})
            {
                var b=B(id);var sleep=Run(b);var plain=Run(b with {Melee=b.Melee! with {Conditions=[]}});Near(sleep.DirectDamage,plain.DirectDamage);Yes(sleep.Events.Count(e=>e.Kind=="cobraSleep")==1&&!sleep.HasSnapshotDps);
                Yes(!Run(b with {Scenario=b.Scenario with {Enemy=new(1,0)}}).Events.Any(e=>e.Kind=="cobraSleep"));
                Yes(!Run(b with {Melee=b.Melee with {Overguard=1e8}}).Events.Any(e=>e.Kind=="cobraSleep"));
                Yes(MeleeEngine.Calculate(catalog,b).Melee!.ForcedProcs.Contains("Stagger"));
            }
        });
        check("코브라 수면:널리파이어 보호막은 소유자에게 거리/시야 무관·대상별 적용",()=>{
            var b=B();b=b with {Encounter=b.Encounter! with {ScriptedAttacksOnly=true,Inputs=[new(.1,"CobraNullifierBubble",2)],NearbyEnemies=[new("nullifier",1000,Defense:new(1e9,0))],BlockedSight=[new(-1,1)]}};
            var r=Run(b);Near(r.DirectDamage+r.StatusDamage,0);Yes(r.Events.Single(e=>e.Kind=="cobraSleep").Description.Contains("대상2"));
        });
        check("코브라 수면:스탠스 첫 접촉만 발동하며 조기 기상 불명인 세번째 타격은 거부",()=>{
            var b=B();var stance=stances.ForWeapon(catalog.Weapons.Single(w=>w.Id==b.WeaponId)).Single(s=>s.IsDefault);
            b=b with {Stance=new(stance.Id,"Neutral",stances.Revision),Encounter=new(EncounterMode.StanceCycle,HitTimes:[0,.2,.4])};
            Unknown(()=>CombatTimeline.Calculate(catalog,b,stances,1));
        });
        check("방패 후속 피해:마무리 노출 시간은 명시한 조건으로 표시·미지정 전이는 거부",()=>{
            var b=B(ShieldThrowMechanic.Sigma);b=b with {Melee=new(),Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,ShieldFlights:[new(0,1,.2,Detonation:new(.4),FinisherWindowSeconds:.5)])};
            var r=Run(b);Yes(r.Notes.Any(n=>n.StartsWith("미검증: 방패")));Yes(DpsEquation.FromTimeline(r).Context!.Contains("조건부")&&DpsEquation.FromTimeline(r).Limitations!.Any(n=>n.Contains("마무리 노출")));Yes(r.Events.Any(e=>e.Kind=="shieldFinisherOpen"));
            var early=Run(b with {Encounter=b.Encounter with {ShieldFlights=[b.Encounter.ShieldFlights![0] with {FinisherWindowSeconds=.05}]}});
            Yes(r.DirectDamage>early.DirectDamage);
            Unknown(()=>CombatTimeline.Calculate(catalog,b with {Encounter=b.Encounter with {ShieldFlights=[b.Encounter.ShieldFlights![0] with {FinisherWindowSeconds=null}]}},stances,1));
        });
    }
}
