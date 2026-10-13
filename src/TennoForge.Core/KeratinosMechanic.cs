namespace TennoForge.Core;

public static class KeratinosMechanic
{
    public const string WeaponId="/Lotus/Weapons/Infested/Melee/InfTransformClaw/InfTransformClawsWeapon";
    public const string Source="https://wiki.warframe.com/w/Keratinos?oldid=2804544";
    public static Stance Resolve(Stance stance)
    {
        var original=stance.Combos.Single(c=>c.Key=="Heavy");
        var article=original with {Key="Heavy Article",Name="강공격 · 본문7타",ActionOverride="Heavy",
            Hits=[new(1,1,3,1,1,1,1,[],""),new(1,1,3,1,1,1,1,["Bleed"],""),new(2,5,1,1,1,1,1,["Bleed"],"")],
            ResolutionNote="미검증: 케라티노스 본문은6회전 타격+돌진1타, 스탠스 표는추가3배 타격이 포함된8타입니다. 현재는 본문의7타·강제 베기 설명을 사용합니다. "+Source};
        var table=original with {Name="강공격 · 스탠스 표8타",ResolutionNote="미검증: 케라티노스 본문7타와 스탠스 표8타가 상충합니다. 현재는 표의8타·강제 상태를 사용합니다. "+Source};
        return stance with {Combos=stance.Combos.Select(c=>c.Key=="Heavy"?table:c).Append(article).ToArray()};
    }
}
