namespace TennoForge.Core;

/// <summary>Pinned Damage/Calculation: unquantized seeds, binary32 operations, one accumulator per tick group.</summary>
public static class RangedStatusMath
{
    public static double MagneticBreakTick(double maximumPool,int stacks,double faction=1,double statusDamage=1,double electricity=1)
    {
        if(stacks is <1 or >10||new[]{maximumPool,faction,statusDamage,electricity}.Any(v=>!double.IsFinite(v)||v<0))throw new ArgumentException("자성 방어층 파괴 입력 오류");
        return maximumPool*.03*stacks/6*Math.Pow(faction*statusDamage,2)*electricity;
    }
    public static double Duration(DamageType type)=>type switch
    {
        DamageType.Puncture=>10,DamageType.Radiation=>12,DamageType.Corrosive=>8,DamageType.Void=>3,DamageType.Tau=>8,DamageType.Blast=>1.5,_=>6
    };
    public static double Tick(DamageType type,IEnumerable<double> seeds,double element,double faction,double statusBonus,bool weaponGenerated=true)
    {
        if(type is not (DamageType.Heat or DamageType.Electricity or DamageType.Toxin or DamageType.Gas or DamageType.Slash))return 0;
        float sum=weaponGenerated?1:0;foreach(var seed in seeds)sum+=(float)seed;
        sum*=type==DamageType.Slash?.35f:.5f;sum*=(float)element;sum*=(float)faction;sum*=(float)(1+statusBonus);return sum;
    }
}

/// <summary>Damage packets cross defenses in order; each layer consumes only its share of the packet.</summary>
public sealed class RangedTargetState
{
    public double Health {get;private set;}
    public double Shields {get;private set;}
    public double Overguard {get;private set;}
    public double Armor {get;private set;}
    public double LastDamageBeforeHealthCap {get;private set;}
    public double LastConsumedOutput {get;private set;}
    private readonly double initialArmor,initialShields;
    private double permanentStrip;
    public string Faction {get;}
    private double shieldGateUntil=-1;
    public RangedTargetState(double health,double shields,double overguard,double armor,string faction)
    {
        if(new[]{health,shields,overguard,armor}.Any(v=>!double.IsFinite(v)||v<0)||armor>2700)throw new ArgumentException("대상 방어 수치 오류");
        Health=health;Shields=shields;Overguard=overguard;Armor=armor;initialArmor=armor;initialShields=shields;Faction=faction;
    }
    public void StripBaseArmor(double fraction)
    {
        if(!double.IsFinite(fraction)||fraction<0||fraction>1)throw new ArgumentException("방어력 제거 비율 오류");
        permanentStrip=Math.Min(1,permanentStrip+fraction);Armor=initialArmor*(1-permanentStrip);
    }
    public void StripBaseShields(double fraction)
    {
        if(!double.IsFinite(fraction)||fraction<0||fraction>1)throw new ArgumentException("실드 제거 비율 오류");
        Shields=Math.Max(0,Shields-initialShields*fraction);
    }
    public void ExternalKill(){Health=Shields=Overguard=0;}
    public double Hit(IReadOnlyDictionary<DamageType,double> damage,double multiplier,double time,Dictionary<DamageType,int> statuses,double heatStrip=0,bool statusTick=false,bool radial=false,bool weakpoint=false,double overguardMultiplier=1)
    {
        var before=Health+Shields+Overguard;
        var magnetic=statuses.GetValueOrDefault(DamageType.Magnetic)>0?1.75+.25*Math.Min(10,statuses[DamageType.Magnetic]):1;
        var viral=statuses.GetValueOrDefault(DamageType.Viral)>0?1.75+.25*Math.Min(10,statuses[DamageType.Viral]):1;
        var packet=damage.ToDictionary(p=>p.Key,p=>Math.Max(0,p.Value*multiplier));
        var original=packet.Values.Sum();var remainingOutput=new Dictionary<DamageType,double>(packet);
        if(Overguard>0)
        {
            var total=packet.Sum(p=>p.Value*magnetic*(p.Key==DamageType.Void?1.5:1)*overguardMultiplier);
            var used=total>0?Math.Min(1,Overguard/total):1;Overguard=Math.Max(0,Overguard-total);
            foreach(var key in packet.Keys.ToArray()){packet[key]*=1-used;remainingOutput[key]*=1-used;}
        }
        var profile=MeleeRuleCatalog.Load().EnemyProfiles.Single(p=>p.Id==Faction);
        foreach(var key in packet.Keys.ToArray())packet[key]*=profile.Multiplier(key);
        if(Shields>0)
        {
            var total=packet.Where(p=>!DamageTypeRules.BypassesShields(p.Key)).Sum(p=>p.Value)*magnetic;
            var used=total>0?Math.Min(1,Shields/total):1;
            if(total>=Shields)shieldGateUntil=time+.1;
            Shields=Math.Max(0,Shields-total);
            foreach(var key in packet.Keys.Where(k=>!DamageTypeRules.BypassesShields(k)).ToArray()){packet[key]*=1-used;remainingOutput[key]*=1-used;}
        }
        var effective=MeleeTargetModel.EffectiveArmor(Armor,new(Statuses:statuses,HeatArmorStrip:heatStrip));
        var healthDamage=packet.Sum(p=>p.Value*viral*(DamageTypeRules.BypassesArmor(p.Key)?1:1-DamageEngine.EnemyArmorReduction(effective))*(!DamageTypeRules.BypassesShields(p.Key)&&!statusTick&&!weakpoint&&time<shieldGateUntil?(radial?0:.05):1));
        var eligibleOutput=remainingOutput.Where(p=>!radial||statusTick||weakpoint||time>=shieldGateUntil||DamageTypeRules.BypassesShields(p.Key)).Sum(p=>p.Value);
        LastConsumedOutput=original-remainingOutput.Values.Sum()+(healthDamage>0?eligibleOutput*Math.Min(1,Health/healthDamage):0);
        LastDamageBeforeHealthCap=before-Health-Shields-Overguard+healthDamage;
        Health=Math.Max(0,Health-healthDamage);return before-Health-Shields-Overguard;
    }
}
