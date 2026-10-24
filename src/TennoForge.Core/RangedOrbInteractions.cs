namespace TennoForge.Core;
public record RangedOrbInteraction(double Time,int Orb,string Kind,double X=0,double Y=0,double TravelSeconds=0,Dictionary<DamageType,double>? Packet=null,double ElementalBasis=0,double Coefficient=1);
public static class RangedOrbInteractions
{
    public static void Validate(RangedOrbInteraction[]? rows)
    {
        if(rows is null)return;
        if(rows.Length>10000||rows.Any(r=>!double.IsFinite(r.Time)||r.Time<0||r.Time>300||r.Orb<0||r.Orb>100000||r.Kind is not ("Absorb" or "Bounce" or "MeleePush")||new[]{r.X,r.Y}.Any(x=>!double.IsFinite(x)||Math.Abs(x)>10000)||!double.IsFinite(r.TravelSeconds)||r.TravelSeconds<0||r.TravelSeconds>300||!double.IsFinite(r.ElementalBasis)||r.ElementalBasis<0||!double.IsFinite(r.Coefficient)||r.Coefficient<0||r.Packet?.Any(p=>!Enum.IsDefined(p.Key)||!double.IsFinite(p.Value)||p.Value<0)==true))throw new ArgumentException("감염 구체 외부 사격·이동 사건 입력 오류");
        foreach(var r in rows.Where(r=>r.Kind=="Absorb"))if(r.Packet is null||r.Packet.Values.Sum()<=0)throw new ArgumentException("흡수할 외부 탄환의 비치명·팩션 적용 전 피해를 입력하세요.");
    }
}
