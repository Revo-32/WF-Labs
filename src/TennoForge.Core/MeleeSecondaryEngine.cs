namespace TennoForge.Core;

public record SecondaryEffect(string Name,string Trigger,double? Damage=null,DamageType? Type=null,double Radius=0,
    double? DamagePerTick=null,double? ExtraStacks=null,double? Chance=null,string Note="",string DamageLabel="기대 피해");
public record ProcSource(DamageType Type,double ModdedBaseDamage,double DamagePerTick,double RemainingTicks,
    double ElementMultiplier=1,double FactionMultiplier=1);
public record AfflictionResult(double Basis,int AddedStacks,ProcSource[] Added,string Reason);

public static class MeleeSecondaryEngine
{
    private static readonly DamageType[] Elements=[DamageType.Heat,DamageType.Cold,DamageType.Electricity,DamageType.Toxin,DamageType.Blast,DamageType.Radiation,DamageType.Gas,DamageType.Magnetic,DamageType.Viral,DamageType.Corrosive];
    public static SecondaryEffect[] Describe(Calculation c)
    {
        if(c.Melee is not {} m)return [];
        var effects=new List<SecondaryEffect>();double V(string key)=>m.Effects.GetValueOrDefault(key);
        var expectedCritical=MeleeStealth.Mean(c);
        if(V("InvigoratedHealth")>0)effects.Add(new("히루도 · 인비거레이티드","치명타 명중",ExtraStacks:1,Note:"기본 최대 체력 +5% · 최대5중첩 · 15초, 치명타 시 전체 지속 갱신 · 체력 모드와 가산 · 시간별 교전에서 실제 중첩을 추적합니다. 무기를 든 동안 이동 속도 +5%."));
        if(V("TauOnStatus")>0)
        {
            var radiation=c.Damage.GetValueOrDefault(DamageType.Radiation)/Math.Max(1e-12,c.Damage.Values.Sum());
            var forced=m.ForcedProcs.Count(p=>Enum.TryParse<DamageType>(p,out var t)&&t is not (DamageType.Radiation or DamageType.Tau)&&DamageTypeRules.HasStatus(t));
            effects.Add(new("베나토 프라임 · 타우","방사능을 제외한 상태 발생",ExtraStacks:(c.StatusChance*(1-radiation)+forced)*m.Projectiles,Note:"이번 공격의 기대 추가 중첩 · 최대 10중첩 · 인플루언스로 전파되지 않음 · 다음 공격에 미치는 상태 확률 증가는 자동 누적하지 않습니다."));
        }
        if(V("Influence")>0)
        {
            foreach(var type in Elements)
            {
                var chance=c.Damage.GetValueOrDefault(type)/Math.Max(1e-12,c.Damage.Values.Sum())*c.StatusChance+(m.ForcedProcs.Contains(type.ToString())?1:0);
                if(chance<=0)continue;
                effects.Add(new("인플루언스 · "+Labels.Damage(type),"발동 중 해당 원소 상태 1회 발생",c.Damage.GetValueOrDefault(type)*expectedCritical*m.FactionMultiplier,type,V("InfluenceRadius"),m.StatusDamage.SingleOrDefault(s=>s.Type==type)?.DamagePerTick*m.FactionMultiplier,Note:$"주변 대상 1명당 · 원래 대상에게는 중복 피해 없음 · 원래 대상 CO 기준 · 현재 조건 기대 발생 {chance:0.###}/투사체"));
            }
            effects.Add(new("인플루언스 발동","직접 근접 전기 상태",Radius:V("InfluenceRadius"),Chance:.2,Note:$"{V("InfluenceDuration"):0}초 · 지속 중 재발동으로 갱신되지 않음"));
        }
        if(V("Afflictions")>0)
        {
            var proc=m.ForcedProcs.FirstOrDefault(p=>p is "Ragdoll" or "Knockdown" or "Lifted");
            var eligible=proc is not null&&m.Setup.Overguard==0&&((m.Setup.Statuses?.GetValueOrDefault(DamageType.Cold)??0)<10||proc=="Ragdoll");
            effects.Add(new("어플릭션즈","넉다운·래그돌·공중 띄우기",ExtraStacks:eligible?V("Afflictions")*(proc=="Lifted"?2:1):0,Note:eligible?"기존 상태 및 이 타격의 상태에 추가. 피해 원천별 MBD를 보존해 계산하며, 중첩 수만으로 피해를 확정할 수 없습니다.":"현재 공격/오버가드/동결 조건에서는 발동하지 않습니다."));
        }
        if(V("VortexChance")>0)effects.Add(new("보텍스","자성 상태 대상 근접 처치",Radius:V("VortexRadius"),Chance:V("VortexChance"),Note:"군중 제어 · 직접 피해 보너스 없음"));
        if(V("ArmorPerKill")>0)effects.Add(new("포티피케이션","근접 처치",Note:$"처치당 방어력 +{V("ArmorPerKill"):0} · 10초"));
        if(V("ShieldRestoreOnKill")>0)effects.Add(new("어시밀레이션","버프 중 강공격 직접 처치",Note:$"최대 실드의 {V("ShieldRestoreOnKill"):P0} 회복 · 오버실드 생성 없음 · 상태 처치 제외"));
        if(V("CareenColdStacks")>0)
        {
            var seconds=6*(1+(m.Setup.Conditions?.GetValueOrDefault("UniversalStatusDuration")??0)/100);
            effects.Add(new("밀리 커린 · 구르기","구르기로 대상 통과",Radius:V("FreezeRadius"),ExtraStacks:V("CareenColdStacks"),Note:$"냉기 {seconds:0.##}초 · 재사용 2초 · 무기 상태 지속 모드 제외 · 외부 범용 지속 보너스 적용 · 오버가드 대상은 동결 불가"));
            effects.Add(new("밀리 커린 · 피해","완전히 동결된 대상 공격",Note:"발동 시 최종 피해 곱연산에 반영 · 10중첩 이전의 둔화 상태에는 피해 보너스 없음"));
        }
        if(V("ExodiaForceDamage")>0&&m.Brackets is {} b)
        {
            var unconditioned=b with {AdditiveConditionOverload=0,ConditionMultiplier=1,ComboMultiplier=1,HeavyAttackDamage=0,WeaponDamageMultiplier=1,RangeFalloff=1,FollowThroughMultiplier=1};
            var elemental=c.Damage.Values.Sum()/Math.Max(1e-12,m.ModdedBaseDamage);
            effects.Add(new("엑소디아 포스","직접 근접 상태 발생",unconditioned.ModdedBaseDamage*elemental*V("ExodiaForceDamage")*(1+(m.EffectiveCombo-1)/4),Radius:6,Chance:.5,Note:"플레이어 중심 · 치명타/상태 발생 없음 · 6m에서 피해 0"));
        }
        if(V("HuntChance")>0)effects.Add(new("엑소디아 헌트","슬램",Radius:V("HuntRadius"),Chance:.5,Note:"끌어당기기·마무리 개방"));
        if(V("EnergyOnHeavyKill")>0)effects.Add(new("엑소디아 브레이브","강공격 직접 처치",Note:$"초당 에너지 {V("EnergyOnHeavyKill"):0.##} · 4초 · 최대 3중첩"));
        if(V("LifeSteal")>0)effects.Add(new("엑소디아 마이트","마무리 후 버프 발동",Chance:.5,Note:$"8초간 근접 흡혈 {V("LifeSteal"):P1} · 현재 타격 회복 기대값에 반영"));
        double? killedHealth=m.Setup.Conditions?.TryGetValue("KilledTargetHealth",out var h)==true&&h>0?h:null;
        if(V("KillExplosion")>0)effects.Add(new("벌컨 블리츠","근접 처치",(V("KillExplosion")+V("KillHealthExplosion")*killedHealth)*m.FactionMultiplier,DamageType.Blast,V("KillExplosionRadius"),Note:"처치 대상 최대 체력 입력 필요 · 선형 감쇠 · 시야 필요"));
        if(V("GroundFinisherExplosion")>0)effects.Add(new("엔트로피 디토네이션","지상 마무리 처치",V("GroundFinisherExplosion")+V("GroundFinisherHealthExplosion")*killedHealth,DamageType.Blast,V("GroundFinisherExplosionRadius"),Note:"처치 대상 최대 체력 입력 필요 · 시야 필요"));
        return effects.ToArray();
    }
    public static AfflictionResult Afflict(Calculation c,IReadOnlyList<ProcSource> existing,string crowdControl)
    {
        var m=c.Melee??throw new ArgumentException("확장 계산 결과가 필요합니다.");var stacks=(int)m.Effects.GetValueOrDefault("Afflictions");
        if(stacks==0||crowdControl is not ("Knockdown" or "Ragdoll" or "Lifted")||m.Setup.Overguard>0)return new(0,0,[],"발동 조건 미충족");
        if((m.Setup.Statuses?.GetValueOrDefault(DamageType.Cold)??0)>=10&&crowdControl!="Ragdoll")return new(0,0,[],"동결은 래그돌만 허용");
        foreach(var p in existing)if(!double.IsFinite(p.ModdedBaseDamage)||p.ModdedBaseDamage<0||!double.IsFinite(p.DamagePerTick)||p.DamagePerTick<0)throw new ArgumentException("상태 피해 원천 수치 오류");
        if(crowdControl=="Lifted")stacks*=2;
        var basis=existing.Where(p=>p.Type is DamageType.Slash or DamageType.Heat or DamageType.Toxin or DamageType.Electricity or DamageType.Gas).GroupBy(p=>p.Type).Sum(g=>g.Average(p=>p.ModdedBaseDamage))*m.FactionMultiplier;
        var result=new List<ProcSource>();
        foreach(var type in existing.Select(p=>p.Type).Where(t=>t is DamageType.Slash or DamageType.Heat or DamageType.Toxin or DamageType.Electricity or DamageType.Gas or DamageType.Blast).Distinct())
        {
            var coefficient=type==DamageType.Slash?.35:type==DamageType.Blast?.3:.5;
            var element=type is DamageType.Toxin or DamageType.Electricity?1+m.ElementBonuses.GetValueOrDefault(type):1;
            var faction=m.FactionMultiplier;
            if(type==DamageType.Heat){var first=existing.First(p=>p.Type==type);element=first.ElementMultiplier;faction=first.FactionMultiplier;}
            var tick=basis*coefficient*faction*m.StatusDamageMultiplier*element*(1+m.Effects.GetValueOrDefault("StatusVulnerability"));
            var duration=m.StatusDamage.SingleOrDefault(s=>s.Type==type)?.Ticks??6;
            for(var n=0;n<stacks;n++)result.Add(new(type,basis*m.StatusDamageMultiplier,tick,duration,element,faction));
        }
        return new(basis,stacks,result.ToArray(),"위키 MBD 평균 → 유형별 평균 합 → 팩션 → 새 상태 브라켓");
    }
    public static double HarmonyDetonation(IReadOnlyList<ProcSource> existing)=>existing.Where(p=>p.Type is DamageType.Slash or DamageType.Heat or DamageType.Toxin or DamageType.Electricity or DamageType.Gas).Sum(p=>p.DamagePerTick*Math.Max(0,p.RemainingTicks));
}
