namespace TennoForge.Core;

/// <summary>Charge ammunition is a damage multiplier, never the number of homing bomblets.</summary>
public static class RangedPhantasma
{
    public static bool Applies(ReferenceWeapon w)=>w.Id is "wiki:primary/Phantasma" or "wiki:primary/Phantasma Prime";
    public static int Rounds(ReferenceWeapon w,RangedConditions c,Dictionary<string,double> effects)
    {
        if(c.LoadedAmmo is {} loadedValue&&(!double.IsFinite(loadedValue)||loadedValue<0||loadedValue>10000))throw new ArgumentException("장전 탄약 범위 오류");
        if(c.PhantasmaChargeAmmo is <0 or >10000)throw new ArgumentException("팬타즈마 충전 탄약은 0(완충)~10000발입니다.");
        var capacity=Math.Max(1,(int)Math.Round(((RangedEngine.N(w.Raw,"Magazine")??11)+effects.GetValueOrDefault("BaseMagazine"))*(1+effects.GetValueOrDefault("Magazine")),MidpointRounding.AwayFromZero)+(int)Math.Round(effects.GetValueOrDefault("FlatMagazine"),MidpointRounding.AwayFromZero));
        // Zero loaded rounds occurs while scheduling a reload; use the next full magazine.
        var loaded=c.LoadedAmmo is >0?Math.Min(capacity,(int)Math.Floor(c.LoadedAmmo.Value)):capacity;
        return Math.Max(1,Math.Min(loaded,c.PhantasmaChargeAmmo==0?capacity:c.PhantasmaChargeAmmo));
    }
    public static double Scale(ReferenceWeapon w,RangedConditions c,Dictionary<string,double> effects)
    {
        var reference=c.PhantasmaReferenceAmmo??(c.AllowApproximations?2:throw new CombatRuleUnavailableException("팬타즈마 피해 표의 기준 충전 탄약이 명시되지 않았습니다. 근사 계산을 켜거나 기준 탄약을 지정하세요."));
        if(!double.IsFinite(reference)||reference<=0||reference>10000)throw new ArgumentException("팬타즈마 기준 탄약은 0 초과 10000 이하입니다.");
        if(c.PhantasmaChargeSnapshot is <1 or >10000)throw new ArgumentException("팬타즈마 발사 시점 탄약 오류");
        return (c.PhantasmaChargeSnapshot??Rounds(w,c,effects))/reference;
    }
    public static string Assumption=>"근사 · 팬타즈마: 피해 표를 2발 충전 기준으로 놓고 소비 탄약에 비례시킵니다. 잔탄에 따른 자동 추가 소비·피해 -1 버그의 일반식은 미기재여서 재현하지 않습니다. DPS·상태 피해·TTK에 영향이 있습니다.";
}
