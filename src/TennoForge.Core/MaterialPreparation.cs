namespace TennoForge.Core;

public enum MaterialOrder { Preparation, Missing, Name }

// Preparation order is an app planning convention, not an official rarity or
// a measured farming-time estimate. Rules use game IDs, never localized names.
// Evidence and the distinction between facts and ordering are documented in
// docs/MASTERY_MATERIALS_2026-10-31.md.
public sealed record MaterialPreparation(int Priority,string Group,string Reason)
{
    private const string Misc="/Lotus/Types/Items/MiscItems/";
    public static bool IsEquipmentComponent(string id)=>
        id.StartsWith("/Lotus/Types/Recipes/WarframeRecipes/",StringComparison.Ordinal)||
        id.StartsWith("/Lotus/Types/Recipes/Weapons/WeaponParts/",StringComparison.Ordinal)||
        id.StartsWith("/Lotus/Types/Gameplay/InfestedMicroplanet/Resources/Mechs/",StringComparison.Ordinal)||
        id.StartsWith("/Lotus/Weapons/",StringComparison.Ordinal)||
        id=="/Lotus/Types/Keys/BardQuest/BardQuestSequencerItem";

    public static bool IsFinishedResource(InventoryItem item)=>!item.Masterable&&!IsEquipmentComponent(item.Id)&&
        item.Category is "Resources" or "Misc";

    public static MaterialPreparation For(InventoryItem? item,bool hasRecipe=false)
    {
        if(item is null)return new(80,"기타 재료","자료에서 분류를 확인하지 못한 재료입니다. 수량 합계에는 포함합니다.");
        string id=item.Id;
        if(id is "/Lotus/Types/Items/Research/EnergyComponent" or "/Lotus/Types/Items/Research/BioComponent" or "/Lotus/Types/Items/Research/ChemComponent")
            return new(0,"가공 재료","연구 완성 재료입니다. 제작 또는 침공 등의 보상으로 준비하며 완성품 수량으로 집계합니다.");
        if(id is Misc+"Forma" or Misc+"FormaAura" or Misc+"FormaUmbra" or Misc+"FormaStance" or Misc+"OrokinCatalyst" or Misc+"OrokinReactor" or Misc+"UtilityUnlocker" or Misc+"WeaponUtilityUnlocker")
            return new(0,"가공 재료","설계도 제작·교환·구매 등으로 준비하는 완성 재료입니다. 장비 제작과 추가 랭작에 필요한 수량을 포함합니다.");
        if(id==Misc+"Alertium")return new(20,"희귀 재화","나이트웨이브 교환 등으로 확보하는 니테인 추출물입니다. 일반 수집 재료보다 먼저 확인하도록 배치합니다.");
        if(id is "/Lotus/Types/Items/Deimos/SeriglassShard" or "/Lotus/Types/Gameplay/Duviri/Resource/DuviriDragonDropItem" or Misc+"SentientFragmentLootItem" or Misc+"PrimeBucks" or Misc+"MemoryCryptoFragment" or Misc+"NarmerBountyResource")
            return new(20,"희귀 재화","특정 콘텐츠의 보상·교환 등 별도 획득 경로를 확인해야 하는 재료입니다.");
        if(id is Misc+"ArgonCrystal")return new(20,"희귀 재화","보관 중 수량이 감소하는 아르곤 크리스탈입니다. 제작 직전에 확보하는 편이 좋으며 현재 보유량은 마지막 동기화 기준입니다.");
        if(id is Misc+"OrokinCell" or Misc+"NeuralSensor" or Misc+"Neurode" or Misc+"ControlModule" or Misc+"Gallium" or Misc+"Morphic" or Misc+"Tellurium" or
            "/Lotus/Types/Gameplay/Zariman/Resources/ZarimanMiscItemB" or "/Lotus/Types/Gameplay/EntratiLab/Resources/EntratiLanthornBundle" or
            "/Lotus/Types/Gameplay/Tau/Resources/TwelveResourceRareItem" or "/Lotus/Types/Gameplay/1999Wf/Resources/1999ResourceRareA")
            return new(20,"희귀 재화","위키의 희귀 자원 분류를 반영합니다. 선택적 자원 제작법으로 자동 분해하지 않고 이 재료 자체의 필요량을 표시합니다.");
        if(id is "/Lotus/Types/Game/CatbrowPet/CatbrowGeneticSignature" or "/Lotus/Types/Gameplay/InfestedMicroplanet/Resources/EntratiWispItem"||
            id.StartsWith("/Lotus/Types/Gameplay/Venus/Resources/CorpusWidget",StringComparison.Ordinal)||
            id.StartsWith("/Lotus/Types/Gameplay/Venus/Resources/Arachnoid",StringComparison.Ordinal))
            return new(20,"희귀 재화","특정 대상·활동에서 확보하는 재료입니다. 일반 자원보다 먼저 준비 여부를 확인하도록 배치합니다.");
        if(item.Masterable||IsEquipmentComponent(id)||item.Category is "Components" or "Parts")
            return new(30,"부품·선행 장비","제작에 필요한 부품 또는 선행 장비입니다. 보유 장비 보존 설정을 적용하고 같은 재고를 중복 차감하지 않습니다.");
        if(id.StartsWith("/Lotus/Types/Items/Gems/",StringComparison.Ordinal)||id.StartsWith("/Lotus/Types/Items/Fish/",StringComparison.Ordinal)||id.StartsWith("/Lotus/Types/Items/Plants/",StringComparison.Ordinal))
            return hasRecipe?new(10,"오픈월드 가공 재료","채광 등의 원물을 정제한 재료입니다. 합금·보석은 완성품 기준으로 표시하며 원물과 이중 합산하지 않습니다."):
                new(40,"일반 재화","채광·낚시·식물 채집 등의 원물·수집 재료입니다. 가공된 완성 재료와 구분합니다.");
        if(id is Misc+"AlloyPlate" or Misc+"Ferrite" or Misc+"Nanospores" or Misc+"Salvage")
            return new(40,"일반 재화","위키의 일반 자원 분류입니다. 희귀·전용 재료 아래에서 전체 필요량과 부족량을 확인할 수 있습니다.");
        if(id is Misc+"Circuits" or Misc+"Plastids" or Misc+"PolymerBundle" or Misc+"Rubedo" or Misc+"Cryotic" or Misc+"OxiumAlloy")
            return new(40,"일반 재화","일반 미션 등의 수집 재료입니다. 준비 난도 분류와 실제 필요 수량을 함께 확인하세요.");
        return new(40,"일반 재화","지역·활동별 수집 재료입니다. 같은 분류 안에서는 부족 여부와 이름순으로 표시합니다.");
    }

    public static MasteryMaterial[] Sort(IEnumerable<MasteryMaterial> rows,MaterialOrder order=MaterialOrder.Preparation)=>order switch
    {
        MaterialOrder.Name=>rows.OrderBy(r=>r.Name,StringComparer.Ordinal).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray(),
        MaterialOrder.Missing=>rows.OrderByDescending(r=>r.Missing).ThenBy(r=>r.PreparationPriority).ThenBy(r=>r.Name,StringComparer.Ordinal).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray(),
        _=>rows.OrderBy(r=>r.PreparationPriority).ThenByDescending(r=>r.Missing>0).ThenBy(r=>r.Name,StringComparer.Ordinal).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray()
    };
}
