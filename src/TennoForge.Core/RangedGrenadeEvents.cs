namespace TennoForge.Core;
public record RangedGrenadeEvent(double Time,int Shot,string Kind,double X=0,double Y=0,int? Target=null);
public static class RangedGrenadeEvents
{
    public static void Validate(RangedGrenadeEvent[]? rows,int targets)
    {
        if(rows is null)return;
        if(rows.Length>10000||rows.Any(r=>!double.IsFinite(r.Time)||r.Time<0||r.Time>300||r.Shot<1||r.Shot>100000||r.Kind is not ("Position" or "Land" or "Attach" or "Water" or "Tar")||new[]{r.X,r.Y}.Any(x=>!double.IsFinite(x)||Math.Abs(x)>10000)||r.Target is {} t&&(t<0||t>=targets)||r.Kind=="Attach"&&r.Target is null))throw new ArgumentException("펜타 이동·부착·접촉 사건 입력 오류");
    }
}
