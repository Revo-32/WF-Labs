namespace TennoForge.Core;
/// <summary>Pinned Mutalist Quanta: one 0.66 factor and 25% flat CC per traversed orb; CD only once.</summary>
public static class RangedOrbBuffs
{
    public static void Validate(RangedConditions c)
    {
        if((c.SimulorOrbs??[]).Length>32||(c.SimulorOrbs??[]).Any(v=>v<1||v>4))throw new ArgumentException("통과한 시뮬러 구체는 1~4중첩, 최대 32개까지 지정하세요.");
        if((c.InfestedOrbs??[]).Length>32||(c.InfestedOrbs??[]).Any(v=>v<1||v>3))throw new ArgumentException("통과한 감염 구체는 1~3중첩, 최대 32개까지 지정하세요.");
    }
    public static double Electricity(IEnumerable<int> stacks)=>stacks.Sum(n=>n switch{1=>1,2=>3.66,3=>5,_=>throw new ArgumentException("구체 중첩 오류")});
    public static double SimulorElectricity(IEnumerable<int> stacks)=>stacks.Sum(n=>n switch{1=>1,2=>3,3=>4,4=>5,_=>throw new ArgumentException("시뮬러 구체 중첩 오류")});
    public static double FinalMultiplier(RangedConditions c)=>Math.Pow(.66,(c.InfestedOrbs??[]).Length+(c.SimulorOrbs??[]).Length);
    public static void AddElement(ReferenceWeapon w,int attack,RangedConditions c,double basis,Dictionary<DamageType,double> damage,Dictionary<string,double> effects)
    {
        var orbs=c.InfestedOrbs??[];var simulor=c.SimulorOrbs??[];if(orbs.Length+simulor.Length==0)return;
        var raw=RangedEngine.Attacks(w)[attack];
        if(RangedEngine.S(raw,"ShotType")=="AoE"&&c.OrbBuffAoe is null)throw new CombatRuleUnavailableException("이 파생 광역 공격이 통과 구체의 버프를 상속하는지는 원문에 없습니다. 광역 상속 시나리오를 선택하세요.");
        if(RangedEngine.S(raw,"ShotType")=="AoE"&&c.OrbBuffAoe==false)return;
        var bonus=Electricity(orbs);var combines=orbs.Length>0&&(w.Category!="secondary"||RangedEngine.S(raw,"ShotType")=="Projectile");
        var target=DamageType.Electricity;
        if(combines)
        {
            var candidates=damage.Where(p=>p.Value>0&&p.Key is DamageType.Heat or DamageType.Cold or DamageType.Toxin or DamageType.Radiation or DamageType.Magnetic or DamageType.Corrosive).Select(p=>p.Key).ToArray();
            if(candidates.Length>1)
            {
                var selected=c.OrbElectricDestination??throw new CombatRuleUnavailableException("구체 전기가 여러 합성 원소 중 어디에 더해지는지는 원문에 없습니다. 추가 전기 원소의 결합 대상을 선택하세요.");
                if(!candidates.Contains(selected))throw new ArgumentException("선택한 구체 결합 원소가 현재 피해에 없습니다.");target=selected;
            }
            else if(candidates.Length==1)target=candidates[0];
            if(target is DamageType.Heat or DamageType.Cold or DamageType.Toxin){var old=damage[target];damage.Remove(target);target=DamageEngine.Combine(target,DamageType.Electricity);damage[target]=damage.GetValueOrDefault(target)+old;}
        }
        damage[target]=damage.GetValueOrDefault(target)+basis*bonus;
        var extra=SimulorElectricity(simulor);if(extra>0)damage[DamageType.Electricity]=damage.GetValueOrDefault(DamageType.Electricity)+basis*extra;
        effects["Electricity"]=effects.GetValueOrDefault("Electricity")+bonus+extra;
        var copies=orbs.Length>0&&simulor.Length>0?(c.MixedOrbCriticalStacks??throw new CombatRuleUnavailableException("감염 구체와 시뮬러를 함께 통과할 때 치피 보너스의 공유 여부는 원문에 없습니다. 혼합 구체 시나리오를 선택하세요."))?2:1:1;
        effects["CriticalDamage"]=effects.GetValueOrDefault("CriticalDamage")+.25*copies;
        effects["FlatCriticalChance"]=effects.GetValueOrDefault("FlatCriticalChance")+.25*(orbs.Length+simulor.Length);
        effects["OrbFinalMultiplier"]=FinalMultiplier(c);
    }
}
