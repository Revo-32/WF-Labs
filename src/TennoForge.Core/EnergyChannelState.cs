namespace TennoForge.Core;

public sealed class EnergyChannelState
{
    public const string Source="https://wiki.warframe.com/w/Energy_Channel?oldid=2587555";
    public double StoredEnergy {get;private set;}
    public EnergyChannelState(double startingEnergy=0)
    {MeleeEngine.Range(startingEnergy,0,100000,"에너지 채널 시작 소모량");StoredEnergy=Math.Min(100,startingEnergy);}
    public void Spend(double actualCost)
    {MeleeEngine.Range(actualCost,0,100000,"어빌리티 실제 에너지 비용");StoredEnergy=Math.Min(100,StoredEnergy+actualCost);}
    public double Consume(){var result=StoredEnergy;StoredEnergy=0;return result;}
    public static double Amount(double storedEnergy,double conversion,IReadOnlyDictionary<DamageType,double> elements)
        =>Math.Min(100,storedEnergy)*conversion*(1+elements.Values.Sum());
    public static double TargetMultiplier(MeleeSetup setup,double armorReduction,double incoming=1)
    {
        var profile=MeleeRuleCatalog.Load().EnemyProfiles.Single(p=>p.Id==setup.Faction);
        if(armorReduction>0||setup.Shields>0||setup.Overguard>0||Enum.GetValues<DamageType>().Any(t=>profile.Multiplier(t)!=1))
            throw new CombatRuleUnavailableException("에너지 채널:저장 피해의 피해 유형과 방어층별 배분은 위키 미기재입니다. 유형에 따라 결과가 달라지지 않는 방어력0·실드/오버가드0·속성 약점 없는 대상에서 저장·한 번 소비·재충전은 계산합니다. "+Source);
        var viral=setup.Statuses?.GetValueOrDefault(DamageType.Viral)??0;
        return (viral>0?1.75+.25*viral:1)*incoming;
    }
    internal Calculation Apply(Calculation c)
    {
        var m=c.Melee!;var amount=m.HitConnects&&m.Policy?.Code!="KorummTacticalSlam"?Amount(StoredEnergy,m.Effects.GetValueOrDefault("EnergyChannel"),m.ElementBonuses):0;
        var delta=amount-m.ExtraDamage;if(delta==0)return c;
        var target=delta*TargetMultiplier(m.Setup,c.ArmorReduction,m.IncomingDamageMultiplier);
        return c with {ExpectedHit=c.ExpectedHit+delta,RawDps=c.RawDps+delta/c.HitInterval,TargetDps=c.TargetDps+target/c.HitInterval,Melee=m with {ExtraDamage=amount}};
    }
}
