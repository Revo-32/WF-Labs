namespace TennoForge.Core;

public record RangedEvolutionState(int Stacks=0,double Age=0);
public record RangedEvolutionTrigger(string Event,Dictionary<string,double> Effects,double Duration=0,int Cap=1,double Chance=1,bool NormalOnly=false,bool DecayOne=false,bool ResetOnReload=false,bool Refreshable=true,Dictionary<string,double>? EffectLimits=null,double[]? RankDurations=null,Dictionary<string,double[]>? RankEffects=null,bool BurstOnly=false,bool ResetOnBodyshot=false,bool ResetOnBlast=false,bool RequiresStalkerSet=false,bool ResetOnMiss=false,double[]? RankChances=null);
public static class RangedEvolutionEvents
{
    public static string DisplayName(RangedBuild build,string triggerId)
    {
        if(triggerId=="external/arcane-pistoleer")return "아케인 피스톨리어";
        var mod=RangedEngine.ModCatalog.Mods.FirstOrDefault(m=>triggerId.StartsWith(m.Id+"/",StringComparison.Ordinal));
        if(mod is not null)return mod.Name;
        return RangedIncarnons.Get(build.WeaponId)?.Perks.FirstOrDefault(p=>triggerId.StartsWith(p.Id+"/",StringComparison.Ordinal))?.Name??"무기 발동 효과";
    }
    public static IEnumerable<(string Id,RangedEvolutionTrigger Rule)> Selected(RangedBuild build)
    {
        var rank=build.Conditions?.PistoleerRank??-1;
        if(rank is < -1 or >5)throw new ArgumentException("아케인 피스톨리어 랭크 오류");
        if(rank>=0)yield return ("external/arcane-pistoleer",new("PistolWeakpointKill",new(){["InfiniteMagazine"]=1},Duration:12,Chance:.1*(rank+1),Refreshable:false));
        var selected=build.Evolutions??[];
        foreach(var perk in RangedIncarnons.Get(build.WeaponId)?.Perks??[])
            if(selected.Contains(perk.Id))
                for(int i=0;i<(perk.Triggers?.Length??0);i++)yield return (perk.Id+"/"+i,perk.Triggers![i]);
        foreach(var slot in build.Slots.Append(build.Exilus).OfType<ModSlot>())
        {
            var mod=RangedEngine.ModCatalog.Mods.SingleOrDefault(m=>m.Id==slot.ModId)??throw new ArgumentException("총기 모드 식별자 없음");
            if(RangedRivens.IsRiven(mod))continue;
            if(slot.Rank<0||slot.Rank>mod.MaxRank)throw new ArgumentException("모드 랭크 범위 오류");
            for(int i=0;i<(mod.Triggers?.Length??0);i++)
            {
                var rule=mod.Triggers![i];var scale=(slot.Rank+1d)/(mod.MaxRank+1);
                yield return (mod.Id+"/"+i,rule with{Effects=rule.Effects.ToDictionary(p=>p.Key,p=>rule.RankEffects?.TryGetValue(p.Key,out var ranks)==true?ranks[slot.Rank]:p.Key=="InfiniteMagazine"?p.Value:p.Value*scale),Chance=rule.RankChances is {} chances?chances[slot.Rank]:rule.Chance,Duration=rule.RankDurations is {} durations?durations[slot.Rank]:rule.Duration});
            }
        }
    }
    public static int Active(RangedEvolutionTrigger rule,RangedEvolutionState state)
    {
        if(state.Stacks<0||state.Age<0||!double.IsFinite(state.Age))throw new ArgumentException("진화 발동 상태 오류");
        var stacks=Math.Min(rule.Cap,state.Stacks);
        if(rule.Duration>0&&state.Age>=rule.Duration)return rule.DecayOne?Math.Max(0,stacks-(int)Math.Floor(state.Age/rule.Duration)):0;
        return stacks;
    }
    public static void Apply(RangedBuild build,Dictionary<string,double> sums,bool form,bool irradiated=false)
    {
        foreach(var (id,rule) in Selected(build))
        {
            if(form&&rule.NormalOnly||rule.RequiresStalkerSet&&build.Conditions?.StalkerSet!=true)continue;
            if(rule.BurstOnly)
            {
                var weapon=RangedEngine.Resolve(build);var attack=RangedEngine.Attacks(weapon)[build.AttackIndex];
                var trigger=RangedEngine.S(attack,"Trigger");if(trigger=="")trigger=RangedEngine.S(weapon.Raw,"Trigger");
                trigger=(RangedTimingCatalog.Data.Weapons.SingleOrDefault(w=>w.Id==weapon.Id)??RangedKitguns.Timing(weapon.Id)).TriggerOverrides.GetValueOrDefault(build.AttackIndex,trigger);
                if(!trigger.StartsWith("Burst",StringComparison.Ordinal)&&trigger!="Auto-Burst")continue;
            }
            var initial=rule.Event=="Equip"&&build.Conditions?.NewlyEquipped==true?new RangedEvolutionState(1,build.Conditions.Elapsed):new();
            var stacks=Active(rule,build.Conditions?.EvolutionStates?.GetValueOrDefault(id)??initial);
            foreach(var pair in rule.Effects)
            {
                var modifier=Math.Min(pair.Value*stacks,rule.EffectLimits?.GetValueOrDefault(pair.Key,double.PositiveInfinity)??double.PositiveInfinity);if(irradiated&&RangedEngine.ModCatalog.Mods.Any(m=>id.StartsWith(m.Id+"/",StringComparison.Ordinal))&&pair.Key is "Heat" or "Cold" or "Electricity" or "Toxin" or "Radiation" or "Magnetic" or "Viral" or "Corrosive" or "Blast" or "Gas")modifier*=.67;
                sums[pair.Key]=sums.GetValueOrDefault(pair.Key)+modifier;
                if(pair.Key=="PassiveToxin")sums["Toxin"]=sums.GetValueOrDefault("Toxin")+pair.Value*stacks;
            }
        }
    }
}
