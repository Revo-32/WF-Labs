namespace TennoForge.Core;
public record RangedPlacedGrenade(int Shot,int Target,int Count,bool Landed=false,double Expiry=double.PositiveInfinity,bool Stuck=false);
/// <summary>Five active Penta grenades; the final multishot batch may cross the placement cap.</summary>
public sealed class RangedGrenadeRack
{
    private readonly List<RangedPlacedGrenade> grenades=[];
    public int Count=>grenades.Sum(g=>g.Count);
    public double Next=>grenades.Select(g=>g.Expiry).DefaultIfEmpty(double.PositiveInfinity).Min();
    public void Launch(int shot,int target,int count)
    {
        if(Count>=5)throw new InvalidOperationException("펜타는 활성 유탄 5개에서 추가 발사를 멈춥니다.");
        if(count<0||count>100)throw new ArgumentException("유탄 개수 오류");if(count>0)grenades.Add(new(shot,target,count));
    }
    public bool Contains(int shot)=>grenades.Any(g=>g.Shot==shot);
    public RangedPlacedGrenade[] TakeShot(int shot){var result=grenades.Where(g=>g.Shot==shot).ToArray();grenades.RemoveAll(g=>g.Shot==shot);return result;}
    public int Moving(int shot)=>grenades.Where(g=>g.Shot==shot&&!g.Stuck).Sum(g=>g.Count);
    public void RemoveMoving(int shot){grenades.RemoveAll(g=>g.Shot==shot&&!g.Stuck);}
    public void Land(int shot,double time,double? duration,int? target=null,int sticky=0)
    {
        if(!double.IsFinite(time)||time<0||duration is {} d&&(!double.IsFinite(d)||d<0)||sticky<0)throw new ArgumentException("유탄 착지·만료 시간 오류");
        var moving=grenades.Where(g=>g.Shot==shot&&!g.Stuck).ToArray();if(sticky>moving.Sum(g=>g.Count))throw new ArgumentException("부착 유탄 수가 이동 중 유탄보다 많습니다.");
        foreach(var g in moving)
        {
            grenades.Remove(g);var fixedCount=Math.Min(sticky,g.Count);sticky-=fixedCount;
            var landed=g with{Target=target??g.Target,Landed=true,Expiry=Math.Min(g.Expiry,duration is {} life?time+life:double.PositiveInfinity)};
            if(fixedCount>0)grenades.Add(landed with{Count=fixedCount,Stuck=true});
            if(g.Count>fixedCount)grenades.Add(landed with{Count=g.Count-fixedCount});
        }
    }
    public RangedPlacedGrenade[] Detonate(double time,bool manual,Func<int,bool>? positioned=null)
    {
        if(manual&&grenades.Any(g=>!g.Landed&&positioned?.Invoke(g.Shot)!=true))throw new CombatRuleUnavailableException("이동 중인 유탄의 현재 기폭 위치가 없습니다. 지정한 도착 이후에 수동 기폭을 예약하세요.");
        var result=grenades.Where(g=>manual||g.Expiry<=time+1e-9).OrderBy(g=>g.Shot).ToArray();foreach(var g in result)grenades.Remove(g);return result;
    }
}
