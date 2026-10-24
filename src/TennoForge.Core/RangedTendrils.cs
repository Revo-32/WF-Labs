namespace TennoForge.Core;
public record RangedTendrilRoute(int Target,double? AcquisitionSeconds=null,bool Miss=false,bool AutoTarget=true);
public static class RangedTendrils
{
    public static bool Applies(ReferenceWeapon w)=>RangedKitguns.Get(w.Id)?.Chamber=="Vermisplicer";
    public static void Validate(RangedConditions c)
    {
        if(c.TendrilAcquisitionSeconds is {} delay&&(!double.IsFinite(delay)||delay<0||delay>300)||c.TendrilRoutes is {} routes&&(routes.Length!=5||routes.Any(r=>r.Target<0||r.Target>=c.GroupCount||r.AcquisitionSeconds is {} d&&(!double.IsFinite(d)||d<0||d>300))))throw new ArgumentException("버미스플라이서 촉수 대상·획득 시간 오류");
    }
    public static double Delay(double? local,RangedConditions c)=>local??c.TendrilAcquisitionSeconds??throw new CombatRuleUnavailableException("버미스플라이서의 표적 획득 지연은 위키에 수치가 없습니다. 조준 지연을 지정하세요. 0은 이미 표적을 획득한 시나리오입니다.");
    public static int? ChainOrigin(IReadOnlyList<int> direct,IReadOnlyList<bool> alive,IReadOnlyList<double> positions,double radius)
    {
        var excluded=direct.ToHashSet();
        foreach(var origin in direct.Reverse())if(Enumerable.Range(0,alive.Count).Any(i=>alive[i]&&!excluded.Contains(i)&&Math.Abs(positions[i]-positions[origin])<=radius+1e-9))return origin;
        return null;
    }
}
