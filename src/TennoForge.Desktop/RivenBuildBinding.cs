using TennoForge.Core;
namespace TennoForge.Desktop;
// Range/splice drafts are reusable assumptions. Only an explicit comparison/equip action binds them to a build.
internal static class RivenBuildBinding
{
    internal static RangedRivenRoll Bind(RangedRivenRoll roll,RangedBuild build)
    {
        var source=RangedEngine.Weapons.Single(w=>w.Id==roll.WeaponId);
        var target=RangedEngine.Weapons.Single(w=>w.Id==build.WeaponId);
        if(RangedEngine.ModCategory(source)!=RangedEngine.ModCategory(target))throw new ArgumentException("리벤 종류와 현재 모딩 무기가 다릅니다. ‘모딩으로’에서 같은 종류의 무기를 선택하세요.");
        return roll with{WeaponId=build.WeaponId};
    }
}
