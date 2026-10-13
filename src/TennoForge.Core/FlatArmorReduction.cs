namespace TennoForge.Core;

public static class FlatArmorReduction
{
    public const string Source="https://wiki.warframe.com/w/Enemy_Level_Scaling?oldid=2795886";
    public static bool HasScaling(Build target,out EnemyStats stats)
    {
        stats=null!;
        if(target.EnemySelection is not {} selection)return false;
        stats=EnemyScaling.Calculate(selection);
        return Math.Abs(stats.Armor-(target.Scenario.Enemy?.Armor??0))<1e-6&&Math.Abs(stats.BaseArmor-(target.Encounter?.BaseArmor??0))<1e-6;
    }
    public static double Remaining(Build target,double removed)
    {
        var initial=target.Scenario.Enemy?.Armor??0;
        if(initial<=0||removed<=0)return initial;
        var baseArmor=target.Encounter?.BaseArmor??throw new CombatRuleUnavailableException("기본 방어력 감소 계산에는 대상의 기본 방어력이 필요합니다.");
        var fraction=Math.Max(0,1-removed/baseArmor);if(fraction==0)return 0;
        if(HasScaling(target,out var stats))
        {
            if(stats.UncappedArmor<200)throw new CombatRuleUnavailableException("초기 하한200으로 올린 방어력에서 기본 방어력 감소의 성장 계수 처리 방식은 원문 미기재입니다. 레벨 성장 수치가200 이상인 대상은 계산합니다. "+Source);
            // Flat base reduction precedes level growth; the upper cap is on
            // the resulting scaled armor. The minimum is INITIAL ONLY.
            return EnemyScaling.ArmorAfterFlatReduction(target.EnemySelection!,removed);
        }
        if(initial is 200 or 2700)throw new CombatRuleUnavailableException("직접 입력한 상·하한 방어력에서는 한계 적용 전 성장 수치를 알 수 없습니다. 팩션·적·레벨을 선택하세요.");
        return initial*fraction;
    }
    public static bool ShatteringApplies(string weaponClass,string attack,AttackRule policy)
        =>policy.Code!="NepheriFireball"&&!(weaponClass=="Glaive"&&attack.StartsWith("Charged Throw",StringComparison.Ordinal));
}
