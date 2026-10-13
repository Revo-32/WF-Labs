namespace TennoForge.Core;

public static class MeleeTargetModel
{
    public static double EffectiveArmor(double armor,MeleeSetup setup)
    {
        MeleeEngine.Range(armor,0,2700,"대상 방어력");
        var statuses=setup.Statuses??[];var corrosive=statuses.GetValueOrDefault(DamageType.Corrosive);
        var heatStrip=setup.HeatArmorStrip??(statuses.GetValueOrDefault(DamageType.Heat)>0?.5:0);
        MeleeEngine.Range(heatStrip,0,.5,"화염 방어력 감소");
        return armor*(corrosive>0?Math.Max(0,1-(.2+.06*corrosive)):1)*(1-heatStrip);
    }
    public static double Multiplier(DamageType type,double armor,MeleeSetup setup)
    {
        var statuses=setup.Statuses??[];
        var magnetic=statuses.GetValueOrDefault(DamageType.Magnetic)>0?1.75+.25*statuses[DamageType.Magnetic]:1;
        var viral=statuses.GetValueOrDefault(DamageType.Viral)>0?1.75+.25*statuses[DamageType.Viral]:1;
        if(setup.Overguard>0)return magnetic*(type==DamageType.Void?1.5:1);
        var profile=MeleeRuleCatalog.Load().EnemyProfiles.Single(p=>p.Id==setup.Faction);
        return profile.Multiplier(type)*(setup.Shields>0&&!DamageTypeRules.BypassesShields(type)?magnetic:(DamageTypeRules.BypassesArmor(type)?1:1-DamageEngine.EnemyArmorReduction(EffectiveArmor(armor,setup)))*viral);
    }
    public static double ExpectedHitAtArmor(Calculation c,double armor)
    {
        var m=c.Melee??throw new ArgumentException("확장 계산 결과가 필요합니다.");
        var direct=c.Damage.Sum(p=>p.Value*Multiplier(p.Key,armor,m.Setup))*MeleeStealth.Expected(c)*m.Projectiles;
        var statuses=m.Setup.Statuses??[];var viral=statuses.GetValueOrDefault(DamageType.Viral)>0?1.75+.25*statuses[DamageType.Viral]:1;
        return direct*m.IncomingDamageMultiplier+(m.ExtraDamage>0?m.ExtraDamage*EnergyChannelState.TargetMultiplier(m.Setup,DamageEngine.EnemyArmorReduction(EffectiveArmor(armor,m.Setup)),m.IncomingDamageMultiplier):0);
    }
}
