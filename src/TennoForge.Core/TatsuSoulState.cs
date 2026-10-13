namespace TennoForge.Core;

/// <summary>Wiki: kills store souls, each gain refreshes the shared 120 s timer.</summary>
public sealed class TatsuSoulState
{
    public const string WeaponId="/Lotus/Weapons/Tenno/Melee/Swords/TnTwoHandedKatana/TnTwoHandedKatana";
    public const string PrimeId="/Lotus/Weapons/Tenno/Melee/Swords/PrimeTatsu/PrimeTatsuWeapon";
    public const string Source="https://wiki.warframe.com/w/Tatsu_Prime?oldid=2789292";
    public static bool Applies(string id)=>id is WeaponId or PrimeId;
    public int Capacity {get;}
    public int Charges {get;private set;}
    public double End {get;private set;}=double.NegativeInfinity;
    private readonly Queue<double> launched=[];
    public int InFlight=>launched.Count;
    public TatsuSoulState(bool revenant=false,int charges=0,double remaining=120)
    {
        Capacity=revenant?9:5;
        if(charges<0||charges>Capacity||!double.IsFinite(remaining)||remaining<0||remaining>120)throw new ArgumentException("소울 스웜: 저장량은 일반5/레버넌트9 이하, 잔여 시간은0~120초입니다.");
        Charges=remaining>0?charges:0;if(Charges>0)End=remaining;
    }
    public void Advance(double time){if(time>=End){Charges=0;End=double.NegativeInfinity;}}
    public void Kill(double time){Advance(time);Charges=Math.Min(Capacity,Charges+1);End=time+120;}
    public int Release(double time)
    {
        Advance(time);var count=Charges;Charges=0;End=double.NegativeInfinity;
        for(var i=0;i<count;i++)launched.Enqueue(time);
        return count;
    }
    public void Impact(double time,double distance)
    {
        if(launched.Count==0)throw new ArgumentException("명중시킬 소울 스웜이 없습니다. 먼저 저장된 소울을 슬라이드로 발사하세요.");
        if(!double.IsFinite(distance)||distance<0||distance>30)throw new ArgumentException("소울 스웜의 유도 대상은 발사 지점30m 이내입니다.");
        if(time+1e-9<launched.Peek()+distance/20)throw new ArgumentException("소울 스웜 명중 시각은 발사 후 직선 최단 비행 시간(거리÷20m/s)보다 빠를 수 없습니다.");
        launched.Dequeue();
    }
}
