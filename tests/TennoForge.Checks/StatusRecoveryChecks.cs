using TennoForge.Core;

internal static class StatusRecoveryChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool value){if(!value)throw new Exception("상태 지속/회복 사건 불일치");}
        void Near(double x,double y,double tolerance=1e-8){if(!double.IsFinite(x)||Math.Abs(x-y)>tolerance*Math.Max(1,Math.Abs(y)))throw new Exception($"{x} != {y}");}
        Build B(string name,string attack="Normal Attack",bool healing=false)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==name);var slots=new ModSlot?[8];if(healing){var mod=catalog.Mods.Single(m=>m.EnglishName=="Healing Return");slots[0]=new(mod.Id,mod.MaxRank);}
            return new(w.Id,slots,new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:attack),Encounter:new(EncounterMode.TenSeconds));
        }
        CombatTimelineResult Run(Build b,int trials=1){var t=CombatTimeline.Calculate(catalog,b,stances,trials);if(t.Unsupported.Length>0)throw new Exception(string.Join(";",t.Unsupported));return t;}
        check("제이드 버프: 30초 개별 지속·겹친 세 버프만60초/60%로 합산",()=>{
            var s=new JadeWeaponBuffs();s.Receive(0,"HarmonicResonance");Near(s.StatusDurationBonus(29.99),.3);Near(s.StatusDurationBonus(30),0);
            s.Receive(30,"EvensongDevotion");s.Receive(40,"CantareChorale");Near(s.StatusDurationBonus(40),0);Yes(s.Receive(50,"HarmonicResonance"));Near(s.StatusDurationBonus(109.99),.6);Near(s.StatusDurationBonus(110),0);
            var expired=new JadeWeaponBuffs();expired.Receive(0,"EvensongDevotion");expired.Receive(1,"CantareChorale");Yes(!expired.Receive(31,"HarmonicResonance"));Near(expired.StatusDurationBonus(31),.3);
        });
        check("하모니: 다른 적 직접 강공격 처치부터 지속 버프·일반 처치 제외",()=>{
            var b=B("Harmony");var t=Run(b with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"MeleeKill"),new(2,"HeavyMeleeKill"),new(3,"EvensongDevotion"),new(4,"CantareChorale")])});
            Near(t.Points.Single(p=>p.Time==1).JadeStatusDurationBonus!.Value,0);Near(t.Points.Single(p=>p.Time==2).JadeStatusDurationBonus!.Value,.3);Near(t.Points.Single(p=>p.Time==4).JadeStatusDurationBonus!.Value,.6);Yes(t.Events.Count(e=>e.Kind=="jadeBuff")==3);
            var killed=Run(b with {Melee=new(Attack:"Heavy Attack"),Scenario=new(Enemy:new(1,0))});Yes(killed.Events.Any(e=>e.Kind=="jadeBuff"));Near(killed.Points.Last().JadeStatusDurationBonus!.Value,.3);
        });
        check("하모닉 레조넌스: 타 무기에도 범용 가산·기존 베기 소급 연장 없음",()=>{
            var b=B("Nikana Prime","Heavy Attack");b=b with {Melee=b.Melee! with {Conditions=new(){["UniversalStatusDuration"]=50}}};
            var baseline=Run(b,64);var after=Run(b with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"HarmonicResonance")])},64);Near(after.StatusDamage,baseline.StatusDamage);
            var before=Run(b with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(0,"HarmonicResonance")])},64);Yes(before.StatusDamage>baseline.StatusDamage);Near(before.DirectDamage,baseline.DirectDamage);
        });
        check("힐링 리턴: 같은 명중의 강제 프록·추첨 상태, 첫 타 즉사에는 없음",()=>{
            var b=B("Glaive Prime","Charged Throw Recall Explosion",true) with {Encounter=new(EncounterMode.FirstHit)};var t=Run(b,2048);
            // Impact and Slash are forced; one Blast roll has probability .34.
            Near(t.Recovery!.HealingReturn!.Mean,11*(2+.34),.015);Near(t.Recovery.HealingReturnUpper!.Mean,t.Recovery.HealingReturn.Mean);
            var dead=Run(b with {Scenario=new(Enemy:new(1,0))},64);Near(dead.Recovery!.HealingReturn!.Mean,0);
            var miss=Run(b with {Melee=b.Melee! with {Distance=100}},64);Near(miss.Recovery!.HealingReturn!.Mean,0);
        });
        check("힐링 리턴: 상태 중첩 수는 중복하지 않음·건블레이드 산탄마다 회복",()=>{
            var b=B("Redeemer Prime","Heavy Attack",true);var c=MeleeEngine.Calculate(catalog,b);var types=c.Damage.Keys.Concat(c.Melee!.ForcedProcs.Where(p=>Enum.TryParse<DamageType>(p,out _)).Select(Enum.Parse<DamageType>)).Distinct().ToArray();
            var states=types.ToDictionary(t=>t,_=>1);b=b with {Melee=b.Melee! with {Statuses=states},Encounter=new(EncounterMode.Primer)};
            var t=Run(b,64);Near(t.Recovery!.HealingReturn!.Mean,11*types.Length*c.Melee.Projectiles);Yes(t.Events.Count(e=>e.Kind=="healingReturn")==c.Melee.Projectiles);
            var many=Run(b with {Melee=b.Melee! with {Statuses=states.ToDictionary(p=>p.Key,p=>Math.Min(5,TargetStatusRules.Maximum(p.Key)))}} ,64);Near(many.Recovery!.HealingReturn!.Mean,t.Recovery.HealingReturn.Mean);
        });
        check("힐링 리턴: 확정 움찔 회복과 미기재 중복은 별도 근거 범위",()=>{
            Yes(HealingReturnRules.StatusCount(0,true)==(1,1));Yes(HealingReturnRules.StatusCount(2,true)==(2,3));
            var t=Run(B("Gram",healing:true),2048);Yes(t.Recovery!.HealingReturn!.Mean>=11&&t.Recovery.HealingReturnUpper!.Mean>t.Recovery.HealingReturn.Mean&&t.Notes.Any(n=>n.Contains("움찔")));
        });
        check("생티 마기스타: 첫 발동 콤보당5%·최대60%, 이후 다른 콤보는 근거 범위",()=>{
            var s=new SanctiMagistarState();s.Heavy(4);Near(s.Minimum,.2);Near(s.Maximum,.2);s.Heavy(1);Near(s.Minimum,.05);Near(s.Maximum,.2);s.Death();Near(s.Minimum,0);Near(s.Maximum,0);
            var max=new SanctiMagistarState(12);Near(max.Minimum,.6);
        });
        check("생티 마기스타: 시작 영구 흡혈·오버가드 직접 피해와 불명확 상태 피해 구분",()=>{
            var b=B("Sancti Magistar");b=b with {Melee=new(Overguard:1e6,Conditions:new(){["SanctiHealingCombo"]=12})};
            var t=Run(b,64);Near(t.Recovery!.SanctiHealing!.Mean,t.DirectDamage*.6);Near(t.Recovery.SanctiHealingUpper!.Mean,(t.DirectDamage+t.StatusDamage)*.6);
            var no=Run(b with {Melee=b.Melee with {Conditions=[]}},64);Near(no.Recovery!.SanctiHealing!.Mean,0);
        });
        check("생티 마기스타: 강공격 사용 자체로 발동·헛쳐도 이후 영구 효과 보존",()=>{
            var b=B("Sancti Magistar","Heavy Slam Attack");b=b with {Scenario=b.Scenario with {Combo=4},Melee=b.Melee! with {Distance=100}};
            var t=Run(b);Near(t.Recovery!.SanctiHealing!.Mean,0);Near(t.Events.Single(e=>e.Kind=="sanctiBuff").Amount,.2);Yes(t.Events.Any(e=>e.Kind=="miss"));
            var hit=Run(b with {Melee=b.Melee! with {Distance=0}},64);Near(hit.Recovery!.SanctiHealing!.Mean,hit.DirectDamage*.2);
        });
    }
}
