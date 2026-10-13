namespace TennoForge.Core;

public record ModDrag(string ModId, int? SourceSlot = null);
public static class ModEditing
{
    public static ModSlot?[] Drop(Catalog catalog, ModSlot?[] input, ModDrag drag, int target)
    {
        if (input.Length != 8 || target is < 0 or > 7) throw new ArgumentException("올바른 슬롯을 선택하세요.");
        var mod = catalog.Mods.SingleOrDefault(m => m.Id == drag.ModId) ?? throw new ArgumentException("지원하지 않는 모드입니다.");
        var result = (ModSlot?[])input.Clone();
        if (drag.SourceSlot is { } source)
        {
            if (source is < 0 or > 7 || input[source]?.ModId != drag.ModId) throw new ArgumentException("드래그한 모드가 변경되었습니다. 다시 선택하세요.");
            (result[target], result[source]) = (result[source], result[target]);
            return result;
        }
        var existing = Array.FindIndex(input, s => s?.ModId == drag.ModId);
        if (existing >= 0) { (result[target], result[existing]) = (result[existing], result[target]); return result; }
        if (input.Where((s,i)=>i!=target && s is not null).Any(s=>catalog.Mods.Single(m=>m.Id==s!.ModId).Family==mod.Family))
            throw new ArgumentException($"{mod.Name}: 같은 계열 모드가 다른 슬롯에 있습니다.");
        result[target] = new(mod.Id,mod.MaxRank);
        return result;
    }
    public static ModSlot?[] Remove(ModSlot?[] input, ModDrag drag)
    {
        if (input.Length != 8 || drag.SourceSlot is not { } i || i is < 0 or > 7 || input[i]?.ModId!=drag.ModId)
            throw new ArgumentException("장착된 모드를 보관함으로 옮겨 해제하세요.");
        var result=(ModSlot?[])input.Clone(); result[i]=null;return result;
    }
}
