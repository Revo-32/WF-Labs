using System.Globalization;

namespace TennoForge.Core;

public enum ModSort { Name, Cost, Polarity }

public static class ModLibrary
{
    private static readonly StringComparer Korean = StringComparer.Create(CultureInfo.GetCultureInfo("ko-KR"), false);
    public static string PolarityName(string polarity) => polarity switch
    {
        "AP_ATTACK" => "마두라이", "AP_DEFENSE" => "바자린", "AP_TACTIC" => "나라몬",
        "AP_POWER"=>"제누릭","AP_WARD"=>"유나이루","AP_PRECEPT"=>"펜자가","AP_UMBRA"=>"움브라",
        "None" or "" => "없음", _ => polarity
    };
    public static IEnumerable<Mod> Sort(IEnumerable<Mod> mods, ModSort sort, bool descending = false)
    {
        // Library cards always show max-rank cost. Slot costs use the equipped rank.
        IOrderedEnumerable<Mod> sorted = sort switch
        {
            ModSort.Cost => descending ? mods.OrderByDescending(m => m.BaseDrain + m.MaxRank) : mods.OrderBy(m => m.BaseDrain + m.MaxRank),
            ModSort.Polarity => descending ? mods.OrderByDescending(m => PolarityName(m.Polarity), Korean) : mods.OrderBy(m => PolarityName(m.Polarity), Korean),
            _ => descending ? mods.OrderByDescending(m => m.Name, Korean) : mods.OrderBy(m => m.Name, Korean)
        };
        return sorted.ThenBy(m => m.Name, Korean).ThenBy(m => m.Id, StringComparer.Ordinal);
    }
}
