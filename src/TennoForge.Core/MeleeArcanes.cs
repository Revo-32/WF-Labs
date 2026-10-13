namespace TennoForge.Core;

internal static class MeleeArcanes
{
    internal static void Apply(Build build,bool heavy,bool aoe,bool frozen,int statusTypes,Dictionary<string,double> effects,List<(DamageType Type,double Value)> elements,List<string> notes)
    {
        var setup=build.Arcanes??new ArcaneSetup();var melee=build.Melee!;var conditions=melee.Conditions??[];
        double C(string key)=>melee.Overguard>0&&key is "Lifted" or "Knockdown"?0:conditions.GetValueOrDefault(key);
        void Add(string key,double amount)=>effects[key]=effects.GetValueOrDefault(key)+amount;
        var source=ReferenceCatalog.Load();var rules=MeleeRuleCatalog.Load();var ids=new HashSet<string>();
        foreach(var (slot,category) in new[]{(setup.Melee,"Melee"),(setup.Warframe1,"Warframe"),(setup.Warframe2,"Warframe"),(melee.Exodia,"Zaw"),(setup.Operator,"Operator")})
        {
            if(slot is null)continue;
            if(!ids.Add(slot.Id))throw new ArgumentException("같은 아케인은 중복 장착할 수 없습니다.");
            var arcane=source.Arcanes.SingleOrDefault(a=>a.Id==slot.Id)??throw new ArgumentException("아케인 식별자 오류");
            if(arcane.Category!=category)throw new ArgumentException("아케인 슬롯 종류가 다릅니다.");
            if(category=="Zaw"&&!ZawCatalog.IsZaw(build.WeaponId))throw new ArgumentException("엑소디아는 조우 부품·조립 무기 계산이 필요합니다. 일반 근접 무기에 적용할 수 없습니다.");
            MeleeEngine.Range(slot.Rank,0,arcane.MaxRank,"아케인 랭크");
            var rule=rules.Arcanes.Single(a=>a.Id==slot.Id);var level=slot.Rank+1;
            notes.Add(arcane.Name+": "+arcane.Describe(slot.Rank));
            if(!slot.Active)continue;
            switch(rule.Code)
            {
                case "MagusAggress":Add("CriticalDamage",MagusAggressState.CriticalDamage(build));notes.Add("마구스 어그레스는 헤비 블레이드/해머의 치명타 피해 모드 브라켓에 가산합니다. 전용·간접 익절티드는 강화하지 않습니다. "+MagusAggressState.Source);break;
                case "Fury":Add("Damage",.3*level);break;
                case "Strike":Add("AttackSpeed",.1*level);break;
                case "Avenger":Add("FlatCritical",.075*level);break;
                case "BladeCharger":Add("Damage",.5*level);break;
                case "Crepuscular":if(C("Invisible")>0)Add("FlatCriticalDamage",.5*level);break;
                case "HotShot":MeleeEngine.Range(C("HotShotStacks"),0,50,"핫 샷 중첩");Add("ArcaneCriticalChance",.01*level*C("HotShotStacks"));break;
                case "Arachne":Add("UniversalDamage",.25*level);break;
                case "Demulcent":
                    foreach(var type in new[]{DamageType.Heat,DamageType.Cold,DamageType.Electricity,DamageType.Toxin})
                    {var count=C("Residual"+type);MeleeEngine.Range(count,0,15,"레지듀얼 중첩");if(count>0)elements.Add((type,.02*level*count));}break;
                case "Exposure":MeleeEngine.Range(setup.ExposureStacks,0,24,"익스포저 횟수");Add("Exposure",Math.Min(2.4,.1*level*setup.ExposureStacks));break;
                case "Retaliation":
                    MeleeEngine.Range(setup.Shields,0,1e6,"현재 실드");MeleeEngine.Range(setup.Overshields,0,1e6,"현재 오버실드");
                    Add("Damage",Math.Min(4.2,.05*level*(Math.Floor(setup.Shields/200)+.5*Math.Floor(setup.Overshields/200))));break;
                case "Doughty":Add("Doughty",.5+.1*slot.Rank);break;
                case "Duplicate":Add("Duplicate",.25+.15*slot.Rank);break;
                case "Crescendo":MeleeEngine.Range(setup.FinisherKills,0,10000,"마무리 처치 횟수");Add("InitialCombo",level*setup.FinisherKills);break;
                case "Animosity":
                    var stacks=C("AnimosityStacks");MeleeEngine.Range(stacks,0,10,"애니모시티 중첩");
                    if(stacks!=Math.Truncate(stacks))throw new ArgumentException("애니모시티 중첩은 0~10의 정수입니다.");
                    if(MeleeAnimosityRules.Receives(build.WeaponId,melee,heavy))Add("ArcaneCriticalChance",.07*level*stacks);break;
                case "Assimilation":
                    if(heavy)
                    {
                        Add("Damage",.25*level);
                        var name=rules.Weapon(build.WeaponId).Name;
                        if(!(name=="Edun"&&melee.Attack=="Polearm Explosion")&&!(name=="Ceramic Dagger"&&melee.Attack=="Spectral Dagger"))
                            Add("ShieldRestoreOnKill",.05*level);
                    }
                    break;
                case "Careen":if(frozen)Add("FinalDamage",.25*level);Add("FreezeRadius",3+.5*slot.Rank);Add("CareenColdStacks",5+slot.Rank);break;
                case "Fortification":Add("ArmorPerKill",35*level);break;
                case "Vortex":Add("VortexChance",.2+.05*slot.Rank);Add("VortexRadius",18);break;
                case "Influence":Add("Influence",1);Add("InfluenceDuration",3*level);Add("InfluenceRadius",10+2*slot.Rank);break;
                case "Afflictions":Add("Afflictions",level);break;
                case "Brave":Add("EnergyOnHeavyKill",1.25*level);break;
                case "Triumph":Add("ComboChance",.125*level);break;
                case "Valor":if(C("Lifted")>0)Add("ComboChance",.5*level);break;
                case "Hunt":Add("HuntChance",.5);Add("HuntRadius",6+2*slot.Rank);break;
                case "Might":Add("LifeSteal",.075*level);break;
                case "Epidemic":Add("Epidemic",level);break;
                case "Force":Add("ExodiaForceChance",.5);Add("ExodiaForceDamage",.5*level);break;
                case "Contagion":Add("ExodiaContagion",level);break;
                case "Guardian":case "Reaper":case "Ultimatum":case "Tanker":notes.Add("발동 방어력은 워프레임 방어력에 가산하며 방어력 조건 진화에 반영합니다.");break;
                case "Trickery":notes.Add("발동 은신은 치명타와 가산되는 은신 브라켓 및 은신 조건 아케인에 반영합니다.");break;
                case "Aegis":case "Barrier":notes.Add("실드 피격 사건과 실제 실드 회복량을 시간 계산에서 처리하며 리탈리에이션의 후속 피해에 반영합니다.");break;
                case "Eruption":notes.Add("에너지/범용 오브 습득의 넉다운 파동은 시간 교전 행동에서 계산합니다. 적별 회복시간 미기재는 사용자 조건과 구분합니다.");break;
                case "UniversalFallout":notes.Add("어빌리티 방사능의 영구 처치 드롭 확률과 생성 오브를 시간 교전에서 계산합니다. 자동 습득하지 않습니다.");break;
                case "Truculence":notes.Add("오버가드 획득량·시야·바이러스 파동은 시간 교전의 전투 중 행동에서 계산합니다.");break;
                case "Utility":notes.Add("이 아케인의 비공격 효과는 선택한 일반 근접 타격의 피해 배수를 바꾸지 않습니다.");break;
                default:throw new ArgumentException("아케인 규칙 누락: "+arcane.Name);
            }
        }
    }
}
