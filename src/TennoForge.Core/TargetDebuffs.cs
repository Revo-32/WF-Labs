namespace TennoForge.Core;

// Remaining is an explicitly supplied remaining lifetime, not an invented cast
// time or an assumption about propagation distance / cast animation.
public record TargetDebuff(string Code,int Rank=3,double Strength=1,double Remaining=10,int Group=1,bool Allied=false);
public record TargetDebuffDefinition(string Code,string Name,string Source,bool StrengthScaled);
public static class TargetDebuffs
{
    public const string Source="https://wiki.warframe.com/w/Damage_Vulnerability?oldid=2813807";
    public static readonly TargetDebuffDefinition[] Definitions=[
        new("Ensnare","인스네어","https://wiki.warframe.com/w/Ensnare?oldid=2816106",false),
        new("Petrify","페트리파이","https://wiki.warframe.com/w/Petrify?oldid=2815645",false),
        new("Strangledome","스트랭글돔","https://wiki.warframe.com/w/Strangledome?oldid=2816108",false),
        new("SplinterStorm","스플린터 스톰","https://wiki.warframe.com/w/Splinter_Storm?oldid=2816060",true),
        new("MassVitrify","매스 비트리파이","https://wiki.warframe.com/w/Mass_Vitrify?oldid=2816062",true)];
    public static void Validate(IEnumerable<TargetDebuff>? debuffs)
    {
        var list=debuffs?.ToArray()??[];if(list.Select(d=>d.Code).Distinct().Count()!=list.Length)throw new ArgumentException("같은 대상 약화 효과는 중복 지정하지 않습니다.");
        foreach(var d in list)
        {
            if(!Definitions.Any(v=>v.Code==d.Code))throw new ArgumentException("대상 약화 효과 식별자 오류");
            MeleeEngine.Range(d.Rank,0,3,"약화 어빌리티 랭크");MeleeEngine.Range(d.Strength,.01,100,"약화 어빌리티 위력");MeleeEngine.Range(d.Remaining,0,300,"시작 약화 잔여시간");
            MeleeEngine.Range(d.Group,1,32,"같은 돔 번호");
        }
    }
    public static bool Active(MeleeSetup setup,string code)=>setup.Debuffs?.Any(d=>d.Code==code&&d.Remaining>0)==true;
    public static bool ControlApplies(MeleeSetup setup)=>setup.Overguard<=0&&(setup.Conditions?.GetValueOrDefault("ControlImmune")??0)<=0;
    public static int? Dome(MeleeSetup setup)=>!ControlApplies(setup)?null:setup.Debuffs?.FirstOrDefault(d=>d.Code=="Strangledome"&&d.Remaining>0)?.Group;
    public static double WhipclawMultiplier(MeleeSetup setup)=>Active(setup,"Ensnare")&&ControlApplies(setup)?2:1;
    public static double Incoming(MeleeSetup setup,bool whipclaw=false)
    {
        Validate(setup.Debuffs);double additive=0,separate=1;
        foreach(var d in setup.Debuffs??[])
        {
            if(d.Remaining<=0)continue;
            switch(d.Code)
            {
                // Petrify belongs to the explicitly additive vulnerability
                // family, independently of Pressure Point and CO.
                case "Petrify" when ControlApplies(setup):additive+=.5;break;
                case "Strangledome" when !whipclaw&&ControlApplies(setup):separate*=2;break;
                case "SplinterStorm":separate*=1+new[]{.15,.2,.25,.35}[d.Rank]*d.Strength;break;
                case "MassVitrify" when ControlApplies(setup):separate*=1+new[]{.2,.3,.4,.5}[d.Rank]*d.Strength;break;
            }
        }
        return (1+additive)*separate;
    }
    public static TargetDebuff[] AtTime(IEnumerable<TargetDebuff>? initial,double time)=>(initial??[]).Where(d=>d.Remaining>time).Select(d=>d with {Remaining=d.Remaining-time}).ToArray();
}
