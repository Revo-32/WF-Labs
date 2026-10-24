namespace TennoForge.Core;
public static class RangedVoltageTraps
{
    public static void Validate(RangedConditions c)
    {
        if(c.VoltageTrapLanding is {} landing&&(!double.IsFinite(landing)||landing<0||landing>300)||c.VoltageTrapTargets is {} cap&&(cap<1||cap>32)||c.VoltageTrapDamage is {} damage&&(!double.IsFinite(damage)||damage<6||damage>7)||c.VoltageTrapPhase is {} phase&&(!double.IsFinite(phase)||phase<0||phase>1d/60))throw new ArgumentException("볼티지 시퀀스: 낙하 시간·대상 수·틱 피해·위상 입력 오류");
    }
    public static (double Landing,int Targets,double Damage,double Phase) Parameters(RangedConditions c)
    {
        Validate(c);
        if(c.VoltageTrapLanding is not {} landing||c.VoltageTrapTargets is not {} cap||c.VoltageTrapDamage is not {} damage||c.VoltageTrapPhase is not {} phase)throw new CombatRuleUnavailableException("볼티지 시퀀스 원문에는 4m·초당 60틱·틱당 6~7 피해·상확 100%만 있습니다. 낙하 시간, 최대 대상 수, 선택한 틱 피해와 첫 틱 위상을 지정하세요.");
        return(landing,cap,damage,phase);
    }
    public static RangedResult Source(RangedResult weapon,double damage)=>weapon with{Damage=new(){[DamageType.Electricity]=damage},ArsenalDamage=new(){[DamageType.Electricity]=damage},BaseDamage=damage,BaseFactor=1,FactionFactor=1,CoFactor=1,StatusBaseFactor=1,CriticalChance=0,CriticalMultiplier=1,StatusChance=1,Multishot=1,Modifiers=[],ExpectedShot=damage,BurstDps=null,SustainedDps=null};
}
