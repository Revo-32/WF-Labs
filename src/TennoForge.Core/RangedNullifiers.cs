namespace TennoForge.Core;
public record NullifierScenario(string Kind="None",double InitialRadius=1,double? CollapseRadius=null,double? FrameSeconds=null,bool NoRegrowth=false,double? RecoveryDelay=null,double? GrowthPerSecond=null,bool? RadialBlocked=null);
public static class RangedNullifiers
{
    public static bool Neutralizable(string kind)=>kind is "Crewman" or "Corrupted" or "Capture";
    public static string EnemyKind(string id)=>id switch
    {
        "/Lotus/Types/Enemies/Corpus/Spaceman/AIWeek/NullifySpacemanAgent"=>"Crewman",
        "/Lotus/Types/Enemies/Orokin/OrokinNullifySpacemanAgent"=>"Corrupted",
        "/Lotus/Types/Enemies/Corpus/CorpusShipRemastered/ShipCrewmanNullifierAgent"=>"Crewman",
        _=>"None"
    };
    public static double ShrinkDamage(double noncriticalDamage,bool magnetic)=>Math.Clamp(noncriticalDamage,100,400)*(magnetic?3:1);
    public static void Validate(NullifierScenario? c)
    {
        if(c is null)return;
        if(c.Kind is not ("None" or "Crewman" or "Corrupted" or "Capture" or "TerraOsprey" or "Bursa")||!double.IsFinite(c.InitialRadius)||c.InitialRadius is <0 or >1||c.CollapseRadius is {} threshold&&(!double.IsFinite(threshold)||threshold<=0||threshold>=1)||c.FrameSeconds is {} frame&&(!double.IsFinite(frame)||frame<=0||frame>1)||c.RecoveryDelay is {} delay&&(!double.IsFinite(delay)||delay<0||delay>300)||c.GrowthPerSecond is {} growth&&(!double.IsFinite(growth)||growth<=0||growth>100))throw new ArgumentException("널리파이어 막 시나리오 범위 오류");
    }
}
public sealed class RangedNullifierField(NullifierScenario scenario)
{
    public double Radius{get;private set;}=scenario.InitialRadius;
    public bool Destroyed{get;private set;}
    public bool Active=>!Destroyed&&Radius>0;
    public double Next{get;private set;}=double.PositiveInfinity;
    private double pending;private bool magnetic;private double regrowAt=double.PositiveInfinity,last=0;
    public void Destroy(){Destroyed=true;Radius=0;pending=0;Next=double.PositiveInfinity;regrowAt=double.PositiveInfinity;}
    public void Hit(double time,double damage,bool hasMagnetic)
    {
        if(!Active)return;RangedNullifiers.Validate(scenario);
        var frame=scenario.FrameSeconds??throw new CombatRuleUnavailableException("널리파이어 막의 프레임별 합산을 위해 프레임 시간을 지정하세요.");
        _=scenario.CollapseRadius??throw new CombatRuleUnavailableException("막이 사라지는 반경 경계는 위키에 없습니다. 붕괴 반경 비율을 지정하세요.");
        if(!scenario.NoRegrowth&&(scenario.RecoveryDelay is null||scenario.GrowthPerSecond is null))throw new CombatRuleUnavailableException("막의 정확한 재생 지연·속도는 위키에 없습니다. 재생 시나리오 또는 교전 중 재생 제외를 선택하세요.");
        pending+=damage;magnetic|=hasMagnetic;Next=(Math.Floor((time+1e-10)/frame)+1)*frame;
    }
    public double Advance(double time)
    {
        if(Destroyed)return 0;
        if(time>regrowAt&&scenario.GrowthPerSecond is {} growth)Radius=Math.Min(1,Radius+(time-Math.Max(last,regrowAt))*growth);
        last=time;
        if(time+1e-9<Next)return 0;
        if(pending>0)
        {
            var damage=RangedNullifiers.ShrinkDamage(pending,magnetic);Radius*=Math.Max(0,1-.06*damage/100);pending=0;magnetic=false;
            if(Radius<=scenario.CollapseRadius)Radius=0;
            regrowAt=scenario.NoRegrowth?double.PositiveInfinity:time+scenario.RecoveryDelay!.Value;
            Next=double.IsFinite(regrowAt)?regrowAt+scenario.FrameSeconds!.Value:double.PositiveInfinity;return damage;
        }
        Next=Radius>=1?double.PositiveInfinity:time+scenario.FrameSeconds!.Value;return 0;
    }
}
