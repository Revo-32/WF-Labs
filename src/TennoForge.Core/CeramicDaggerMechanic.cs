using System.Text.Json;

namespace TennoForge.Core;

public record CeramicDaggerContact(double Launch,double Impact,int Volley=1,int Dagger=1,int Target=1,bool Surface=false,double? Travel=null);
public static class CeramicDaggerMechanic
{
    public const string WeaponId="/Lotus/Weapons/Tenno/Melee/Dagger/CeramicDagger";
    public const string Source="https://wiki.warframe.com/w/Ceramic_Dagger_Incarnon_Genesis?oldid=2773637";
    public const double Speed=60,Radius=4;
    public static void Validate(CeramicDaggerContact[] contacts,int targets)
    {
        if(contacts.Length>256)throw new ArgumentException("세라믹 단검 접촉은 최대256회입니다.");
        foreach(var f in contacts)
        {
            MeleeEngine.Range(f.Launch,0,300,"세라믹 발사 시각");MeleeEngine.Range(f.Impact,f.Launch,300,"세라믹 명중 시각");MeleeEngine.Range(f.Volley,1,10000,"세라믹 발사 묶음");MeleeEngine.Range(f.Dagger,1,2,"묶음 안의 단검 번호");MeleeEngine.Range(f.Target,1,targets,"세라믹 대상 번호");
            if(f.Travel is {} travel){MeleeEngine.Range(travel,0,10000,"세라믹 비행 거리");if(f.Impact+1e-9<f.Launch+travel/Speed)throw new ArgumentException("세라믹 명중 시각이 지정한 비행 거리÷60m/s보다 빠릅니다.");}
        }
        foreach(var group in contacts.GroupBy(c=>c.Volley))
        {
            if(group.Select(c=>c.Launch).Distinct().Count()>1)throw new ArgumentException("같은 발사 묶음은 하나의 강공격 발사 시각을 공유합니다.");
            if(group.GroupBy(c=>c.Dagger).Any(g=>g.Count()>1))throw new ArgumentException("같은 발사 묶음에서 같은 단검을 두 번 명중시킬 수 없습니다.");
        }
    }
    public static Calculation Calculate(Catalog catalog,Build build,int dagger=1,bool explosion=false,double distance=0,bool useInitialComboFloor=true)
    {
        if(build.WeaponId!=WeaponId||build.Melee?.Incarnon!=true)throw new ArgumentException("인카논 변신 중인 세라믹 대거가 필요합니다.");
        MeleeEngine.Range(dagger,1,2,"세라믹 단검 번호");
        var name=explosion?"Spectral Dagger Explosion":"Spectral Dagger";var profile=MeleeRuleCatalog.Load().Weapon(WeaponId);
        var raw=profile.Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")==name);
        var values=new Dictionary<string,double>(build.Melee.Conditions??[]);
        if(dagger==2||explosion)values["FirstQuickMelee"]=0;
        var input=build with {Stance=null,Scenario=build.Scenario with {MotionMultiplier=1},Melee=build.Melee with {Attack=name,Conditions=values,TargetIndex=1,Distance=distance,Finisher=null}};
        return MeleeEngine.CalculateAttack(catalog,input,useInitialComboFloor:useInitialComboFloor,component:new(raw,MeleeEngine.AttackPolicy(WeaponId,name) with {FollowThrough=false},NoEnergyChannel:true));
    }
    internal static Build OnTarget(Build source,Build receiver)
    {
        var m=receiver.Melee!;var conditions=(source.Melee!.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary();
        foreach(var pair in m.Conditions??[])if(CombatScene.EnemyConditionKeys.Contains(pair.Key))conditions[pair.Key]=pair.Value;
        return source with {Scenario=source.Scenario with {Enemy=receiver.Scenario.Enemy,ExistingStatusTypes=receiver.Scenario.ExistingStatusTypes},Arcanes=source.Arcanes is {} a?a with {Frozen=receiver.Arcanes?.Frozen??false}:receiver.Arcanes,
            Melee=source.Melee with {Statuses=m.Statuses,Faction=m.Faction,Shields=m.Shields,Overguard=m.Overguard,HeatArmorStrip=m.HeatArmorStrip,Debuffs=m.Debuffs,Conditions=conditions}};
    }
    internal static Build WithCastResources(Build current,Build launched)
    {
        var values=new Dictionary<string,double>(current.Melee!.Conditions??[]);
        foreach(var key in new[]{"FirstQuickMelee","AnimosityStacks","TruthFlame"})values[key]=launched.Melee!.Conditions?.GetValueOrDefault(key)??0;
        return current with {Scenario=current.Scenario with {Combo=launched.Scenario.Combo},Melee=current.Melee with {Conditions=values,Tennokai=launched.Melee!.Tennokai}};
    }
    internal static string PayloadSignature(Catalog catalog,Build build)
    {
        var m=build.Melee!;var neutral=build with {Scenario=build.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},Arcanes=(build.Arcanes??new()) with {Frozen=false},Melee=m with {Statuses=[],Debuffs=[],Faction="Neutral",Shields=0,Overguard=0,HeatArmorStrip=0,Conditions=(m.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary()}};
        var c=Calculate(catalog,neutral,useInitialComboFloor:false);return JsonSerializer.Serialize(new {c.Damage,c.ExpectedHit,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
    }
}
