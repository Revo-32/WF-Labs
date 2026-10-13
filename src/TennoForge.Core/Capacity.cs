using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public record WeaponCapacityDefault(string[] SlotPolarities,string StanceSlotPolarity);
public record StanceCapacityMod(string ModId,string Polarity,int MaxRank,int Bonus);
public record CapacityCatalog(string Version,Dictionary<string,WeaponCapacityDefault> WeaponDefaults,Dictionary<string,StanceCapacityMod> StanceMods)
{
    public static CapacityCatalog Load()
    {using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.capacity-data.json")??throw new InvalidOperationException("수용량 자료가 없습니다.");return JsonSerializer.Deserialize<CapacityCatalog>(stream,Catalog.Json)??throw new InvalidOperationException("수용량 자료 형식 오류");}
    public CapacitySetup Default(string weaponId,string? stanceId)
    {var exalted=ExaltedRules.Find(weaponId);var preset=ZawCatalog.IsZaw(weaponId)?ZawCatalog.Capacity(weaponId):exalted is not null?ExaltedRules.Capacity(weaponId):WeaponDefaults[weaponId];stanceId=exalted?.Stance??stanceId;return new(30,30,true,stanceId is not null,stanceId,preset.StanceSlotPolarity,(string[])preset.SlotPolarities.Clone());}
}
public record CapacitySetup(int WeaponRank,int MasteryRank,bool Catalyst,bool StanceEquipped,string? StanceId,string StanceSlotPolarity,string[] SlotPolarities);
public record CapacityResult(int BaseCapacity,int StanceBonus,int Total,int Used,int[] SlotCosts,int ExilusCost=0)
{
    public int Remaining=>Total-Used;
    public bool Fits=>Remaining>=0;
}
public static class CapacityEngine
{
    private static readonly Lazy<StanceCatalog> Stances=new(StanceCatalog.Load);
    private static readonly Lazy<CapacityCatalog> Metadata=new(CapacityCatalog.Load);
    public const string RulesVersion="wiki-capacity-0.4.0";
    public static readonly string[] ModPolarities=["None","AP_ATTACK","AP_DEFENSE","AP_TACTIC","AP_POWER","AP_WARD","AP_PRECEPT","AP_UMBRA"];
    public static readonly string[] OrdinaryPolarities=[..ModPolarities,"Omni"];
    public static CapacityResult Calculate(Catalog catalog,Build build)=>Calculate(catalog,Metadata.Value,build);
    public static int SlotCost(Mod mod,int rank,string slotPolarity)
    {
        if(rank<0||rank>mod.MaxRank)throw new ArgumentException("모드 랭크 범위 오류");
        if(!OrdinaryPolarities.Contains(slotPolarity))throw new ArgumentException("지원하지 않는 슬롯 극성입니다.");
        var cost=mod.BaseDrain+rank;
        if(slotPolarity=="None"||mod.Polarity=="None")return cost;
        // Omni Forma rev.2815619: Umbra is neutral, never mismatched.
        if(slotPolarity=="Omni")return mod.Polarity=="AP_UMBRA"?cost:(int)Math.Ceiling(cost/2d);
        return slotPolarity==mod.Polarity?(int)Math.Ceiling(cost/2d):(int)Math.Ceiling(cost*1.25);
    }
    public static CapacityResult Calculate(Catalog catalog,CapacityCatalog metadata,Build build)
    {
        var setup=build.Capacity??throw new ArgumentException("수용량 설정이 없습니다.");
        var weapon=catalog.Weapons.SingleOrDefault(w=>w.Id==build.WeaponId)??throw new ArgumentException("지원하지 않는 무기입니다.");
        var exalted=ExaltedRules.Find(build.WeaponId);
        if(exalted is not null&&(!setup.Catalyst||setup.StanceSlotPolarity!="AP_POWER"||exalted.Stance is {} fixedStance&&(!setup.StanceEquipped||setup.StanceId!=fixedStance)))throw new ArgumentException("익절티드 무기는 내장 카탈리스트와 고정 제누릭 스탠스를 사용합니다.");
        if(setup.WeaponRank<0||setup.WeaponRank>weapon.MaxRank||setup.MasteryRank is <0 or >30)throw new ArgumentException($"무기 랭크는 0~{weapon.MaxRank}, 기본 용량용 마스터리 랭크는 0~30입니다.");
        if(setup.SlotPolarities is null||setup.SlotPolarities.Length!=8||build.Slots is null||build.Slots.Length!=8)throw new ArgumentException("수용량 설정은 일반 슬롯 8개가 필요합니다.");
        if(setup.SlotPolarities.Any(p=>!OrdinaryPolarities.Contains(p)))throw new ArgumentException("지원하지 않는 일반 슬롯 극성입니다.");
        if(!OrdinaryPolarities.Contains(setup.StanceSlotPolarity)&&setup.StanceSlotPolarity!="Universal")throw new ArgumentException("지원하지 않는 스탠스 슬롯 극성입니다.");
        if(exalted is null&&!ZawCatalog.IsZaw(build.WeaponId)&&!metadata.WeaponDefaults.ContainsKey(build.WeaponId))throw new ArgumentException("수용량 자료가 없는 무기입니다.");
        if(setup.StanceId is {} stanceId)
        {
            if(!metadata.StanceMods.ContainsKey(stanceId))throw new ArgumentException("지원하지 않는 수용량 스탠스입니다.");
            var selectedWeapon=catalog.Weapons.SingleOrDefault(w=>w.Id==build.WeaponId)??throw new ArgumentException("지원하지 않는 무기입니다.");
            if(!Stances.Value.ForWeapon(selectedWeapon).Any(s=>s.Id==stanceId))throw new ArgumentException("무기와 수용량 스탠스가 호환되지 않습니다.");
        }
        var bonus=0;
        if(setup.StanceEquipped)
        {
            if(setup.StanceId is null||!metadata.StanceMods.TryGetValue(setup.StanceId,out var mod))throw new ArgumentException("수용량에 사용할 스탠스 자료가 없습니다.");
            if(build.Stance is {} attack&&attack.Id!=setup.StanceId)throw new ArgumentException("공격 스탠스와 수용량 스탠스가 다릅니다.");
            bonus=setup.StanceSlotPolarity is "Universal" or "Omni"||setup.StanceSlotPolarity==mod.Polarity?mod.Bonus*2:setup.StanceSlotPolarity=="None"?mod.Bonus:mod.Bonus*4/5;
        }
        else if(build.Stance is {} selected&&metadata.StanceMods.TryGetValue(selected.Id,out var selectedMod)&&selectedMod.ModId.Length>0)throw new ArgumentException("스탠스 콤보 계산에는 스탠스 장착이 필요합니다.");
        if(exalted is {Pseudo:false})bonus=10; // Update38.5: fixed Exalted stances grant +10.
        var costs=build.Slots.Select((slot,index)=>slot is null?0:SlotCost(ResolvedMod(catalog.Mods.SingleOrDefault(m=>m.Id==slot.ModId)??throw new ArgumentException("지원하지 않는 모드입니다."),build.Riven),slot.Rank,setup.SlotPolarities[index])).ToArray();
        var baseCapacity=Math.Max(setup.WeaponRank,setup.MasteryRank)*(setup.Catalyst?2:1);
        var exilusCost=0;
        if(build.Melee?.Exilus is {} exilus)
        {
            var mod=catalog.Mods.SingleOrDefault(m=>m.Id==exilus.ModId)??throw new ArgumentException("엑실러스 모드 식별자 오류");
            var reference=ReferenceCatalog.Load().Mods.Single(m=>m.Id==mod.Id);
            if(!reference.Raw.Flag("IsExilus"))throw new ArgumentException("엑실러스 슬롯에 장착할 수 없는 모드입니다.");
            exilusCost=SlotCost(mod,exilus.Rank,build.Melee.ExilusPolarity);
        }
        return new(baseCapacity,bonus,baseCapacity+bonus,costs.Sum()+exilusCost,costs,exilusCost);
    }
    public static Mod ResolvedMod(Mod mod,RivenRoll? riven)
    {
        if(riven is null||mod.Id!=RivenValues.ModId(riven.WeaponId))return mod;
        if(riven.BaseDrain is not >=0 or >30||riven.Polarity is null||!ModPolarities.Contains(riven.Polarity))throw new ArgumentException("리벤의 0랭크 코스트와 극성을 입력하세요.");
        return mod with {BaseDrain=riven.BaseDrain.Value,Polarity=riven.Polarity};
    }
}
