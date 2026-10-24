namespace TennoForge.Core;
public record RangedOrbShot(int Shot,int Attack=-1,double? BaseValue=null,double RadialDistance=0,bool? HitscanContinues=null);
public static class RangedOrbPickups
{
    public static double Amount(RangedOrbShot orb,bool health,RangedResult source)
    {
        var value=orb.BaseValue??(health?50:25);return value*(1+source.Modifiers!.GetValueOrDefault(health?"HealthOrbShotBonus":"EnergyOrbShotBonus"));
    }
    public static void Validate(RangedBuild b,ReferenceWeapon w)
    {
        var rows=b.Conditions?.OrbShots??[];var attacks=RangedEngine.Attacks(w);
        if(rows.Length>1000||rows.Any(r=>r.Shot<1||r.Shot>100000||r.Attack< -1||r.Attack>=attacks.Length||r.BaseValue is {} v&&(!double.IsFinite(v)||v<=0)||!double.IsFinite(r.RadialDistance)||r.RadialDistance<0))throw new ArgumentException("오브 사격 번호·공격·회복량·거리 오류");
        if(rows.GroupBy(r=>(r.Shot,r.Attack<0?b.AttackIndex:r.Attack)).Any(g=>g.Count()>1))throw new ArgumentException("같은 발사·공격의 오브 회수를 중복 예약할 수 없습니다.");
    }
}
