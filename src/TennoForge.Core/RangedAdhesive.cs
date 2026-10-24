namespace TennoForge.Core;
/// <summary>Weapon-specific adhesive fuses. No obsolete pre-19.10 damage bonus.</summary>
public static class RangedAdhesive
{
    public static bool Relevant(ReferenceWeapon w,int component)=>w.Id=="wiki:primary/Tenet Ferrox"?component==2:component==0;
    public static bool Equipped(RangedBuild b)=>b.Slots.Append(b.Exilus).OfType<ModSlot>().Any(s=>RangedEngine.ModCatalog.Mods.Single(m=>m.Id==s.ModId).Effects.ContainsKey("AdhesiveChance"));
    public static double Delay(ReferenceWeapon w,RangedBuild b,int component,double sinceLaunch)
    {
        if(!Relevant(w,component))return 0;var c=b.Conditions??new();
        if(w.Id=="wiki:primary/Tonkor")return Math.Max(0,c.AdhesiveTonkorFuse-sinceLaunch);
        if(w.Id=="wiki:primary/Zarr")return 3;
        if(RangedPentaAugments.Applies(w))return RangedPentaAugments.Has(b,"PentaNapalm")?c.AdhesiveNapalmFuse??throw new CombatRuleUnavailableException("부착 네이팜의 신관은 모드 문서 4초·네이팜 문서 약 5초가 다릅니다. 사용할 시간을 선택하세요."):0;
        if(w.Id is "wiki:primary/Torid" or "wiki:primary/Tenet Ferrox")return c.AdhesiveFieldDelay??throw new CombatRuleUnavailableException("애드히시브의 일반 신관은 4초지만 이 무기의 지속 장판 생성 지연은 원문에 없습니다. 장판 지연 시나리오를 지정하세요.");
        return 4;
    }
    public static void Validate(RangedConditions c)
    {
        if(new[]{c.AdhesiveNapalmFuse??0,c.AdhesiveFieldDelay??0,c.AdhesiveTonkorFuse}.Any(v=>!double.IsFinite(v)||v<0||v>300))throw new ArgumentException("부착 신관 시간은 0~300초의 유한한 값이어야 합니다.");
    }
}
