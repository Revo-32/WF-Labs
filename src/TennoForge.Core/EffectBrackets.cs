namespace TennoForge.Core;

/// <summary>Semantic effect keys are compiled from reviewed records, never from display text.</summary>
public static class EffectBrackets
{
    public static string Name(string effect)=>effect switch
    {
        "Damage"=>"기본 피해 가산",
        "HeavyDamage"=>"강공격 기본 피해 가산",
        "ConditionOverload"=>"공격별 CO 규칙",
        "BaseDamage"=>"모딩 전 기본 피해 추가",
        "StoredDamageFraction"=>"기본 피해 모드 이후 추가",
        "UniversalDamage"=>"범용 무기 피해 · 공격별 적용",
        "ThrowWeaponDamage"=>"투척 무기 피해 · 별도 곱",
        "SlamDamage"=>"슬램 피해 · 별도 곱",
        "TennokaiDamage"=>"텐노카이 시 기본 피해 가산 · 프레셔와 합산",
        "StatusDamage"=>"상태 피해 가산",
        "StatusVulnerability"=>"대상 상태 피해 취약성 · 별도 곱",
        "StatusFinalMultiplier"=>"상태 피해 최종 배수",
        "EnergyChannel"=>"치명타 적용 이후 추가",
        "FinalDamage"=>"최종 피해 배수",
        "BaseCritical"=>"모딩 전 기본 치확 추가",
        "CriticalChance"=>"치확 모드 가산 · 강공격 2배",
        "PassiveCriticalChance" or "ArcaneCriticalChance"=>"치확 가산 · 강공격 2배 제외",
        "FlatCritical"=>"모딩 후 고정 치확 추가",
        "BloodRush"=>"콤보별 치확 가산",
        "CriticalDamage" or "TennokaiCriticalDamage"=>"치명타 피해 가산",
        "BaseCriticalDamage" or "IncarnonCriticalDamage"=>"모딩 전 기본 치명타 배수 추가",
        "FlatCriticalDamage"=>"모딩 후 치명타 배수 추가",
        "BaseStatus"=>"모딩 전 기본 상태 확률 추가",
        "FlatStatus"=>"모딩 후 고정 상태 확률 추가",
        "PostStanceStatus"=>"스탠스 상태 배율 이후 고정 확률 추가",
        "StatusChance" or "TennokaiStatus" or "WeepingWounds"=>"상태 확률 가산",
        "AttackSpeed"=>"공격 속도 가산",
        "FinisherSpeed"=>"마무리 공속 가산 · +50% 상한",
        "SpeedMultiplier"=>"공격 속도 별도 곱",
        "ImpactDamage" or "PunctureDamage" or "SlashDamage"=>"해당 물리 피해 가산 · 상태 기반 제외",
        "VsGrineer" or "VsCorpus" or "VsInfested" or "VsOrokin" or "VsSentient" or "VsTheMurmur"=>"해당 팩션 피해 · 별도 곱",
        "Heat" or "Cold" or "Electricity" or "Toxin"=>"기본 원소 가산 · 슬롯 순서 조합",
        "Blast" or "Radiation" or "Gas" or "Magnetic" or "Viral" or "Corrosive"=>"복합 원소 가산",
        "FinisherDamage"=>"마무리 피해 가산",
        _=>"보조 · 조건부 효과 ("+effect+")"
    };
}
