namespace TennoForge.Core;

public static class RangedEventLabels
{
    public static readonly string[] Kinds=["PrimaryFire","ArrowPickup","WeaponAffinity","AllyKill","Reload","Dodge","BlockedShot","AlternateFire","Holster","Equip","EquipFromPrimary","PlayerShieldBreak","PlayerOverguardBreak","AbilityCast","EnergySpent","EnergyRestore","AmmoPickup","PrimaryAmmoDrop","SecondaryAmmoDrop","UniversalAmmoDrop"];
    public static string Detail(string value)
    {
        var index=value.IndexOf(" · ",StringComparison.Ordinal);if(index<0)return value;
        var label=value[..index] switch{"HarpoonHit"=>"작살 명중","Hit"=>"명중","Damage"=>"피해","DirectHit"=>"직접 명중","FiveHeadPellets"=>"한 발사에서 펠릿 5개 약점 명중","Headshot"=>"약점 명중","HeadshotKill"=>"약점 처치","WeakpointKillWithDot"=>"약점·상태 처치","Kill"=>"처치","Status"=>"상태 적용","Puncture"=>"관통 상태","TomeKill"=>"책으로 표시한 대상 처치", "TomeAlternateHit"=>"보조 공격 명중","FullBurstHit"=>"점사 전체 명중","FullBurstHeadshot"=>"점사 전체 약점 명중","BurstHeadshot"=>"점사 약점 명중","Equip"=>"무기 꺼내기","Fire"=>"발사","PunchThrough"=>"관통 명중","PlayerDefenseBreak"=>"내 방어층 파괴","EnergySpent200"=>"에너지 200 소모","EximusHeadshot"=>"엑시무스 약점 명중","PrimaryHeadshot"=>"일반 사격 약점 명중","SlashStatusKill"=>"베기 상태 처치","ElectricTargetHit"=>"전기 상태 대상 명중","BlastDetonation"=>"폭발 상태 기폭",_=>value[..index]};
        return label+value[index..];
    }
    public static string Name(string kind)=>kind switch
    {
        "PrimaryFire"=>"일반 사격으로 전환",
        "ArrowPickup"=>"아말감 다이큐 · 직접 접촉한 화살 수",
        "WeaponAffinity"=>"무기 숙련도 획득 · 분배 후",
        "AllyKill"=>"외부 처치 · 대상 번호 (1부터)",
        "Reload"=>"수동 재장전",
        "Dodge"=>"회피 · 구르기",
        "BlockedShot"=>"방패로 적 공격 차단",
        "AlternateFire"=>"보조 공격 사용",
        "PrimaryAmmoDrop"=>"주무기 탄약 상자 · 개수","SecondaryAmmoDrop"=>"보조무기 탄약 상자 · 개수","UniversalAmmoDrop"=>"범용 탄약 상자 · 개수","Holster"=>"무기 수납 · 지속 초","Equip"=>"무기 꺼내기","EquipFromPrimary"=>"주무기에서 전환","PlayerShieldBreak"=>"내 실드 파괴",
        "PlayerOverguardBreak"=>"내 오버가드 파괴","AbilityCast"=>"어빌리티 사용","EnergySpent"=>"에너지 소모","EnergyRestore"=>"에너지 회복","AmmoPickup"=>"예비 탄약 획득 · 발 수",_=>kind
    };
}
