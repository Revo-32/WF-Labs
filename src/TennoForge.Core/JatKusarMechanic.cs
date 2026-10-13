namespace TennoForge.Core;

public static class JatKusarMechanic
{
    public const string WeaponId="/Lotus/Weapons/Grineer/Melee/GrnKusarigama/GrnKusarigamaWeapon";
    public const string Source="https://wiki.warframe.com/w/Jat_Kusar?oldid=2813919";
    // The source explicitly permits only Seismic Wave for this component.
    // No normal/heavy slam radius, crit chance, or status chance is borrowed.
    public static double ExplosionBase(Calculation c)=>218*(1+c.Melee!.Effects.GetValueOrDefault("SlamDamage"));
    public static Stance Resolve(Stance stance)=>stance with {Combos=stance.Combos.Select(c=>c.Key!="Heavy"?c:c with {
        ResolutionNote="미검증: 잿 쿠사르 본문은 직격 넉다운/폭발 래그돌로 구분하지만 스탠스 표에는 직격 래그돌/띄우기도 있습니다. 현재 칼날 표의 배율/강제 상태를 사용하며218폭발 성분은 따로 표시합니다. "+Source}).ToArray()};
    public static SecondaryEffect Describe(Calculation c)=>new("잿 쿠사르 · 강공격 폭발","플레이어 중심 · 비치명 기본 피해",ExplosionBase(c),DamageType.Blast,
        Note:"218 × (1 + 슬램 피해) · 일반 근접 피해·킬링 블로우·콤보·컨오·팩션 제외\n반경·치명/상태 확률·각 폭발 발생 시점은 원문 미기재. 일반/강슬램의 반경이나 확률로 대체하지 않습니다.\n직격 대상은 넉다운·폭발은 위로 래그돌. 현재 성분을 완성된 폭발 DPS로 표시하지 않습니다. "+Source,DamageLabel:"비치명 기본 피해");
}
