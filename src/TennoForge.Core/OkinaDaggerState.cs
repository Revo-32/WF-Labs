using System.Text.Json;

namespace TennoForge.Core;

public record OkinaDaggerFlight(double Launch,double Impact,int Dagger=1,int Target=1,double? Travel=null);
public sealed class OkinaDaggerState
{
    public const string WeaponId="/Lotus/Weapons/Tenno/Melee/Swords/TennoSai/TennoSais";
    public const string PrimeId="/Lotus/Weapons/Tenno/Melee/Swords/PrimeOkina/PrimeOkina";
    public const string Source="https://wiki.warframe.com/w/Okina_Incarnon_Genesis?oldid=2797886";
    public const double SeekRange=15,Speed=18,Radius=4;
    private readonly Dictionary<int,(double Created,bool Launched)> active=[];
    private int sequence;
    public int Count=>active.Count;
    public static bool Applies(string id)=>IncarnonEngine.ForWeapon(id)?.Code=="Okina";
    public int? Kill(double time,bool daggerDirect)
    {
        if(daggerDirect||active.Count==6)return null;
        var id=++sequence;active[id]=(time,false);return id;
    }
    public void Launch(OkinaDaggerFlight flight,double targetDistance)
    {
        if(!active.TryGetValue(flight.Dagger,out var d))throw new ArgumentException("아직 생성되지 않았거나 이미 소모된 오키나 단검입니다.");
        if(d.Launched)throw new ArgumentException("같은 오키나 단검을 두 번 발사할 수 없습니다.");
        if(flight.Launch<=d.Created)throw new ArgumentException("오키나 단검은 생성 후 궤도를 돈 뒤 발사됩니다. 발사 시각을 생성 이후로 지정하세요.");
        if(targetDistance>SeekRange)throw new ArgumentException("오키나 유도 대상은 플레이어15m 이내여야 합니다.");
        active[flight.Dagger]=(d.Created,true);
    }
    public void Impact(int id)
    {
        if(!active.TryGetValue(id,out var d)||!d.Launched)throw new ArgumentException("발사하지 않은 오키나 단검의 명중입니다.");
        active.Remove(id);
    }
    public static void Validate(OkinaDaggerFlight f,int targetCount)
    {
        MeleeEngine.Range(f.Launch,0,300,"오키나 발사 시각");MeleeEngine.Range(f.Impact,f.Launch,300,"오키나 명중 시각");MeleeEngine.Range(f.Dagger,1,10000,"오키나 단검 번호");MeleeEngine.Range(f.Target,1,targetCount,"오키나 대상 번호");
        if(f.Travel is {} travel){MeleeEngine.Range(travel,0,10000,"오키나 실제 비행 거리");if(f.Impact+1e-9<f.Launch+travel/Speed)throw new ArgumentException("오키나 명중 시각이 지정한 비행 거리÷18m/s보다 빠릅니다.");}
    }
    public static Calculation Calculate(Catalog catalog,Build build,bool explosion=false,double distance=0,bool useInitialComboFloor=true)
    {
        if(!Applies(build.WeaponId)||build.Melee?.Incarnon!=true)throw new ArgumentException("인카논 변신 중인 오키나가 필요합니다.");
        var name=explosion?"Spectral Dagger Explosion":"Spectral Dagger";var profile=MeleeRuleCatalog.Load().Weapon(build.WeaponId);
        var raw=profile.Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")==name);
        var policy=MeleeEngine.AttackPolicy(build.WeaponId,name) with {HitCombo=false,FollowThrough=false,Tennokai=false};
        return MeleeEngine.CalculateAttack(catalog,build with {Stance=null,Scenario=build.Scenario with {MotionMultiplier=1},Melee=build.Melee with {Attack=name,TargetIndex=1,Distance=distance,Tennokai=false,Finisher=null}},useInitialComboFloor:useInitialComboFloor,component:new(raw,policy,NoEnergyChannel:true));
    }
    public static string PayloadSignature(Catalog catalog,Build b)
    {
        var m=b.Melee!;var neutral=b with {Scenario=b.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},Arcanes=(b.Arcanes??new()) with {Frozen=false},Melee=m with {Statuses=[],Debuffs=[],Faction="Neutral",Shields=0,Overguard=0,HeatArmorStrip=0,Conditions=(m.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary()}};
        var c=Calculate(catalog,neutral,useInitialComboFloor:false);return JsonSerializer.Serialize(new {c.Damage,c.ExpectedHit,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
    }
}
