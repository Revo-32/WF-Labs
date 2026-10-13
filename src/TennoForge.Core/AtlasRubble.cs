namespace TennoForge.Core;

public record AtlasRubbleSetup(double Armor=0,double Grace=0,bool Nullified=false);
public record RubblePickup(double Time,double Amount=50,double MissingHealth=0,int Target=0);
public sealed class AtlasRubbleState
{
    public const string Source="https://wiki.warframe.com/w/Atlas/Abilities/Passive?oldid=2794675";
    public const string PetrifySource="https://wiki.warframe.com/w/Petrify?oldid=2815645";
    private double now,graceEnd;
    private readonly Dictionary<int,(double Created,double Amount)> drops=[];
    public double Armor {get;private set;}
    public bool Nullified {get;private set;}
    public int Drops {get;private set;}
    public double Healing {get;private set;}
    public bool Heap=>Armor>1400;
    public AtlasRubbleState(AtlasRubbleSetup s)
    {Validate(s);Armor=s.Armor;graceEnd=s.Grace;Nullified=s.Nullified;}
    public static void Validate(AtlasRubbleSetup s)
    {MeleeEngine.Range(s.Armor,0,1500,"러블 방어력");MeleeEngine.Range(s.Grace,0,2,"러블 감소 유예");}
    public static void Validate(RubblePickup[] pickups,int targets)
    {
        if(pickups.Length>512)throw new ArgumentException("러블 습득은 최대512개입니다.");
        foreach(var p in pickups){MeleeEngine.Range(p.Time,0,300,"러블 습득 시각");MeleeEngine.Range(p.Amount,.001,75,"외부 러블 회복량");MeleeEngine.Range(p.MissingHealth,0,1e6,"습득 직전 부족 체력");MeleeEngine.Range(p.Target,0,targets,"러블을 떨어뜨린 대상 번호");}
    }
    public void Advance(double time)
    {
        if(time<now-1e-9)throw new ArgumentException("러블 시간은 되돌릴 수 없습니다.");
        Armor=Math.Max(0,Armor-Math.Max(0,time-Math.Max(now,graceEnd))*(Nullified?10:5));now=time;
    }
    public double NextBoundary(double time)
    {
        Advance(time);if(Armor<=0)return double.PositiveInfinity;
        if(graceEnd>time+1e-9)return graceEnd;
        return time+(Armor>1400?Armor-1400:Armor)/(Nullified?10:5);
    }
    public AtlasRubbleSetup At(double time){Advance(time);return new(Armor,Math.Max(0,graceEnd-time),Nullified);}
    public void SetNullified(double time,bool value){Advance(time);Nullified=value;}
    public void HostMigration(double time){Advance(time);Armor=0;graceEnd=time;}
    public void Drop(double time,int target,bool landslide)
    {if(!drops.TryAdd(target,(time,landslide?75:50)))throw new ArgumentException("동일 대상의 러블을 중복 생성할 수 없습니다.");Drops++;}
    public double Pickup(RubblePickup p)
    {
        Advance(p.Time);var amount=p.Amount;
        if(p.Target>0)
        {
            if(!drops.Remove(p.Target,out var drop))throw new CombatRuleUnavailableException("지정 대상의 러블이 생성되지 않았거나 이미 습득했습니다. 석화 출처·처치 시각을 확인하세요. "+Source);
            if(p.Time>=drop.Created+30)throw new ArgumentException("러블은 생성30초 뒤 사라집니다.");
            amount=drop.Amount;
        }
        if(p.MissingHealth>0){var heal=Math.Min(p.MissingHealth,amount);Healing+=heal;return heal;}
        Armor=Math.Min(1500,Armor+amount);graceEnd=p.Time+2;return 0;
    }
}
