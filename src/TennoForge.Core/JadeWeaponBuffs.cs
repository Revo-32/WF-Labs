namespace TennoForge.Core;

/// <summary>Buff receipt events, independent of the weapon that delivered them.</summary>
public sealed class JadeWeaponBuffs
{
    public const string HarmonyId="/Lotus/Weapons/Tenno/Melee/Scythe/TnChoirScythe/TnChoirScythe";
    public const string HarmonySource="https://wiki.warframe.com/w/Harmony?oldid=2789911";
    public const string EvensongSource="https://wiki.warframe.com/w/Evensong?oldid=2789951";
    public const string CantareSource="https://wiki.warframe.com/w/Cantare?oldid=2797776";
    private readonly Dictionary<string,double> ends=[];
    private double ensembleEnd=double.NegativeInfinity;
    private bool ensembleRetriggered;
    public static bool IsReceipt(string kind)=>kind is "HarmonicResonance" or "EvensongDevotion" or "CantareChorale" or "JadeEnsemble";
    public bool Receive(double time,string kind)
    {
        if(!IsReceipt(kind))throw new ArgumentException("제이드 무기 버프 종류 오류");
        if(kind=="JadeEnsemble") {ensembleEnd=time+60;ensembleRetriggered=false;ends.Clear();return true;}
        if(ensembleEnd>time)
        {
            // A ten-second encounter starting without the buff cannot reach this
            // uncertain expiry. Do not invent its refresh behavior for future horizons.
            ensembleRetriggered=true;return false;
        }
        ends[kind]=time+30;
        if(new[]{"HarmonicResonance","EvensongDevotion","CantareChorale"}.All(k=>ends.GetValueOrDefault(k,double.NegativeInfinity)>time))
        {ensembleEnd=time+60;ensembleRetriggered=false;ends.Clear();return true;}
        return false;
    }
    public double StatusDurationBonus(double time)
    {
        if(ensembleEnd>time)return .6;
        if(ensembleRetriggered)throw new CombatRuleUnavailableException("제이드 앙상블 유지 중 재발동 후 만료·갱신 규칙은 고정 원문에 미기재입니다. 앙상블의 확인된 60초 이후에는 새 발동 사건이 필요합니다.");
        return ends.GetValueOrDefault("HarmonicResonance",double.NegativeInfinity)>time?.3:0;
    }
    public double NextExpiry(double time)=>new[]{ensembleEnd}.Concat(ends.Values).Where(x=>x>time+1e-9).DefaultIfEmpty(double.PositiveInfinity).Min();
}
