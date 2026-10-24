namespace TennoForge.Core;

public record HemaReload(double Rounds,double HealthCost,double CriticalBonus);
public static class RangedHema
{
    public static bool Applies(ReferenceWeapon w)=>RangedEngine.S(w.Raw,"Family")=="Hema";
    public static HemaReload Reload(double maximum,double health,double capacity,double missing,double vigorPerTwenty,bool host,string partialRounding="Unspecified")
    {
        if(new[]{maximum,health,capacity,missing,vigorPerTwenty}.Any(v=>!double.IsFinite(v)||v<0)||maximum<=0||capacity<=0||health>maximum||missing>capacity)throw new ArgumentException("헤마 재장전 입력 오류");
        var vigor=vigorPerTwenty>0;
        if((vigor?health<22:health<=3)||missing==0)return new(0,0,0);
        var unit=maximum*(vigor?.5:.05)/capacity;
        var requested=missing*unit;var available=Math.Max(0,health-2);var rounds=missing;
        if(requested>available+1e-9)
        {
            if(!vigor)throw new CombatRuleUnavailableException("기본 헤마의 체력 부족 재장전에서 탄수·최저 체력 처리 수치가 원문에 없습니다.");
            rounds=available/unit;
            if(Math.Abs(rounds-Math.Round(rounds))>1e-9)
                rounds=partialRounding switch{"Down"=>Math.Floor(rounds),"Up"=>Math.Ceiling(rounds),_=>throw new CombatRuleUnavailableException("네크로패직 비거의 체력 부족 부분 재장전은 소수 탄수 반올림 규칙이 원문에 없습니다. 부분 탄수 처리 시나리오를 선택하세요.")};
        }
        var drain=Math.Min(available,rounds*unit);
        return new(rounds,drain,vigor&&host?Math.Min(3.6,Math.Floor(drain/20+1e-9)*vigorPerTwenty):0);
    }
}
