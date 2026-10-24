using System.Reflection;
using System.Text.Json;
namespace TennoForge.Core;
public record RangedEvolution(string Id,int Stage,string Name,Dictionary<string,double> Effects,string Condition,RangedEvolutionTrigger[]? Triggers=null);
public record RangedIncarnon(string WeaponId,int[] Forms,double ChargeGain,WikiSource? Source,RangedEvolution[] Perks,double SpoolStep=0,double SpoolFloor=0,bool ChargeOnBody=false,Dictionary<int,double>? ChargeCapacity=null);
public static class RangedIncarnons
{
    public static readonly RangedIncarnon[] Rules=Load();
    private static RangedIncarnon[] Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-incarnons.json")!;return JsonSerializer.Deserialize<RangedIncarnon[]>(s,Catalog.Json)!.Select(RangedBalance4413.Upgrade).ToArray();}
    public static RangedIncarnon? Get(string id)=>Rules.SingleOrDefault(r=>r.WeaponId==id);
    public static JsonElement Normalize(ReferenceWeapon weapon,JsonElement attack,int index)
    {
        if(Get(weapon.Id)?.ChargeCapacity?.TryGetValue(index,out var capacity)!=true)return attack;
        var node=System.Text.Json.Nodes.JsonNode.Parse(attack.GetRawText())!;node["IncarnonCharges"]=capacity;
        return JsonSerializer.SerializeToElement(node);
    }
    public static bool IsForm(ReferenceWeapon w,int index)=>RangedVectis.Applies(w)&&index>0||RangedStug.Applies(w)&&index is 3 or 4 or 6||Get(w.Id)?.Forms.Contains(index)==true||RangedAttackLinks.Rules.Any(l=>l.WeaponId==w.Id&&l.Child==index&&Get(w.Id)?.Forms.Contains(l.Parent)==true);
    public static void Apply(RangedBuild b,ReferenceWeapon w,Dictionary<string,double> sums)
    {
        var chosen=b.Evolutions??[];var rule=Get(w.Id);if(chosen.Length==0)return;
        if(rule is null)throw new ArgumentException("인카논 진화가 없는 무기입니다.");
        var perks=chosen.Select(id=>rule.Perks.SingleOrDefault(p=>p.Id==id)??throw new ArgumentException("무기와 진화 식별자가 다릅니다.")).ToArray();
        if(perks.GroupBy(p=>p.Stage).Any(g=>g.Count()>1))throw new ArgumentException("각 진화 단계에서 하나만 선택하세요.");
        var c=b.Conditions??new();var form=IsForm(w,b.AttackIndex);
        void Add(string key,double v)=>sums[key]=sums.GetValueOrDefault(key)+v;
        foreach(var p in perks)
        {
            var enabled=p.Condition switch{""=>true,"Aiming"=>c.Aiming,"Normal"=>!form,"FewerThan3Statuses"=>(c.Statuses?.Count(x=>x.Value>0)??0)+c.HiddenStatusCount<3,"NormalGlideSlide"=>!form&&(c.AimGlide||c.Sliding),"GlideSlide"=>c.AimGlide||c.Sliding,"NormalHeadshot6"=>!form&&c.RecentHeadshot&&c.HeadshotAge<6,"EmptyReload6"=>!form&&c.RecentReload&&c.ReloadAge<6&&c.RoundsReloaded>=RangedEngine.N(w.Raw,"Magazine"),_=>throw new CombatRuleUnavailableException("진화 조건 연결 필요: "+p.Condition)};
            if(!enabled&&p.Condition=="Normal"&&p.Effects.Keys.All(k=>k is "BaseAmmoCapacity" or "BaseAmmoAdd"))enabled=true;
            if(enabled)foreach(var pair in p.Effects)
            {
                if(RangedStug.Applies(w)&&b.AttackIndex==4&&pair.Key=="ProjectileSpeed")continue;
                if(pair.Key=="InnateCO"&&RangedEngine.S(RangedEngine.Attacks(w)[b.AttackIndex],"ShotType")=="AoE")continue;
                if(pair.Key=="BaseStatusChance"&&form&&p.Effects.ContainsKey("FormBaseStatusChance"))continue;
                if(pair.Key=="FormBaseStatusChance"){if(form)Add("BaseStatusChance",pair.Value);continue;}
                Add(pair.Key,pair.Value);
            }
        }
        double V(string key)=>sums.GetValueOrDefault(key);
        if(c.StalkerSet){Add("Multishot",V("StalkerMultishot"));Add("ExtraMultishotDamage",V("StalkerExtraMultishotDamage"));}
        if(V("ExtraMultishotDamage")>0)sums["ExtraMultishotAmmo"]=1;
        if(form&&V("FormMultishotBonus")>0)sums["ExtraMultishotDamage"]=0;
        if(!form)sums["FormMultishotBonus"]=0;
        if(!c.PrimaryEquipped){Add("BaseDamage",V("NoPrimaryBaseDamage"));if(!form)Add("BaseMagazine",V("NoPrimaryBaseMagazine"));}
        if(c.Channeling)foreach(var key in new[]{"BaseDamage","AmmoEfficiency","PunchThrough","Multishot","FireRate","Accuracy"})Add(key,V("Channel"+key));
        if(c.Overshields)Add("BaseDamage",V("OvershieldsBaseDamage"));
        if(c.MaxEnergy>700){Add("BaseCriticalDamage",V("EnergyBaseCriticalDamage"));Add("BlastRadius",V("EnergyBlastRadius"));}
        if(c.PlayerArmor>450){Add("PunchThrough",V("ArmorPunchThrough"));Add("Multishot",V("ArmorMultishot"));}
        if(c.SprintSpeed>=1.2)foreach(var key in new[]{"ProjectileSpeed","FireRate","InnateCO"})
            if(key!="InnateCO"||RangedEngine.S(RangedEngine.Attacks(w)[b.AttackIndex],"ShotType")!="AoE")Add(key,V("Sprint"+key));
        if(c.TargetUndamaged&&c.TargetHealthFraction==1){Add("BaseCriticalChance",V("UndamagedBaseCriticalChance"));Add("BaseCriticalDamage",V("UndamagedBaseCriticalDamage"));}
        if(c.RecentReload&&c.RoundsReloaded>0&&c.EmptyReloadStacks>0)Add("ReloadSpeed",V("EmptyReloadSpeed"));
        if(!form&&c.FinalBurst)Add("BaseMultishot",V("FinalBurstBaseMultishot"));
        if(c.FinalShot)Add("BaseMultishot",V("FinalShotBaseMultishot"));
        if(c.NearestOtherEnemyDistance>=V("IsolatedMultishotRadius")&&b.Distance>=V("IsolatedMultishotRadius"))Add("Multishot",V("IsolatedMultishot"));
        if(c.EnemiesWithinSixMeters>=V("CrowdMinimum"))Add("AmmoEfficiency",V("CrowdAmmoEfficiency"));
        var fired=Math.Min(c.FiringStacks,V("FiringStackLimit"));
        Add("BaseMultishot",V("FiringBaseMultishot")*fired);Add("Multishot",V("FiringMultishot")*fired);
        var emptyReloads=Math.Min(c.EmptyReloadStacks,V("EmptyReloadStackLimit"));
        if(!form)Add("BaseMagazine",V("EmptyReloadBaseMagazine")*emptyReloads);
        Add("BaseCriticalDamage",V("EmptyReloadBaseCriticalDamage")*emptyReloads);Add("BaseDamage",V("EmptyReloadBaseDamage")*emptyReloads);
        Add("FireRate",sums.GetValueOrDefault("MountingMomentum")*Math.Min(99,c.MomentumStacks));
        Add("ReloadSpeed",sums.GetValueOrDefault("HeadshotReloadStacks")*Math.Max(0,Math.Min(3,c.EvolutionHeadshotStacks)-(int)Math.Floor(c.EvolutionHeadshotAge/12)));
        Add("Damage",sums.GetValueOrDefault("OverwhelmingAttrition")*Math.Max(0,Math.Min(3,c.AttritionStacks)-(int)Math.Floor(c.AttritionAge/10)));
        if(sums.GetValueOrDefault("DualModeChamber")>0)Add(c.DualModePunchThrough?"PunchThrough":"ProjectileSpeed",c.DualModePunchThrough?4:1);
        if(c.LingeringSeconds>0)Add("HeadshotMultiplier",sums.GetValueOrDefault("LingeringHeadshot"));
        if(c.PunchThroughBuffSeconds>0)Add("AmmoEfficiency",sums.GetValueOrDefault("PunchThroughAmmoBuff"));
    }
}
