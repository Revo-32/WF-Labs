using System.Text.Json;

namespace TennoForge.Core;

public enum GlaiveContactKind { Bounce, PunchThrough, Return }
public record GlaivePoint(double X=0,double Y=0,double Z=0)
{
    internal CombatEnemy Position=>new("glaive-position",X,Y,Z);
}
public record GlaiveContact(double Time,int Target=1,GlaiveContactKind Kind=GlaiveContactKind.Bounce,GlaivePoint? Surface=null,double? Thickness=null);
public record GlaiveDetonation(double Time,GlaivePoint? Position=null,int CenterTarget=1);
public record GlaiveFlight(double Launch,double Returned,int Throw=1,bool Charged=true,bool Airborne=false,
    double? BufferedAt=null,double? RecallAt=null,GlaiveContact[]? Contacts=null,GlaiveDetonation? Detonation=null,double? FirstLegDistance=null,int Hologram=0);

/// <summary>Explicit flight events; no fabricated homing path, wind-up or catch time.</summary>
public sealed class GlaiveFlightState
{
    public const string Source=GlaiveCriticalRules.Source;
    public const string PowerThrowId="/Lotus/Upgrades/Mods/Melee/WeaponGlaivePowerthrowMod";
    public const string PowerSource="https://wiki.warframe.com/w/Power_Throw?oldid=2787704";
    public const string ReturnSource="https://wiki.warframe.com/w/Glaive?oldid=2798798";
    public const string KestrelPrimeId="/Lotus/Weapons/Tenno/Melee/Glaives/PrimeKestrel/PrimeKestrel";
    public const string KestrelPrimeSource="https://wiki.warframe.com/w/Kestrel_Prime?oldid=2815891";
    public static int BaseBounces(string weaponId)=>weaponId==KestrelPrimeId?8:weaponId==WolfSledgeFlight.WeaponId?2:3;
    // Weapon article characteristics specify innate body penetration for both
    // throw strengths; some attack infobox rows only repeat it on charged throw.
    public static double InnatePunchThrough(string weaponId)=>weaponId switch
    {
        "/Lotus/Weapons/Infested/Melee/Glaives/PunctureGlaive/PunctureGlaiveWeapon" or
        "/Lotus/Weapons/Corpus/Melee/Glaive/CrpGlaive/CrpGlaive" or
        "/Lotus/Weapons/Tenno/Melee/Glaives/LightGlaive/LightGlaiveWeapon" or
        "/Lotus/Weapons/Tenno/Melee/Glaives/PrimeGlaive/PrimeGlaiveWeapon" or
        "/Lotus/Weapons/Grineer/Melee/GrnBoomerang/GrnBoomerang" or
        "/Lotus/Weapons/Grineer/Melee/GrnBoomerang/HalikarWraithWeapon" or
        "/Lotus/Weapons/Tenno/Melee/Glaives/Boomerang/BoomerangWeapon" or KestrelPrimeId=>1,
        _=>0
    };
    private GlaiveFlight? last;
    private int bounces;
    private double returnAt=double.PositiveInfinity;
    public int Consecutive {get;private set;}
    public bool InFlight(double time)=>last is not null&&time>=last.Launch&&time<last.Returned;
    public static bool Applies(string id)=>id==WolfSledgeFlight.WeaponId||MeleeRuleCatalog.Load().Weapon(id).Raw.Text("Class")=="Glaive";
    public static string Attack(GlaiveFlight f,bool explosion=false,string? weaponId=null)=>(f.Charged&&weaponId!=WolfSledgeFlight.WeaponId?"Charged Throw":"Throw")+(explosion?" Recall Explosion":"");
    public static void Validate(GlaiveFlight[] flights,int targets,string weaponId)
    {
        if(flights.Length==0)return;
        if(!Applies(weaponId))throw new ArgumentException("투척 비행은 글레이브 또는 울프 슬레지에서 사용합니다.");
        if(flights.Length>64||flights.Select(f=>f.Throw).Distinct().Count()!=flights.Length)throw new ArgumentException("글레이브 투척 번호는 중복 없이 최대64개입니다.");
        foreach(var f in flights)
        {
            MeleeEngine.Range(f.Hologram,0,4,"투척 공격 주체");if(f.Hologram>0&&weaponId!=WolfSledgeFlight.WeaponId&&f.Detonation is not null)throw new ArgumentException("미라지 분신은 글레이브를 수동 기폭하지 않습니다. "+HallOfMirrorsRules.Source);
            MeleeEngine.Range(f.Throw,1,10000,"투척 번호");MeleeEngine.Range(f.Launch,0,300,"투척 발사");MeleeEngine.Range(f.Returned,f.Launch,300,"회수 완료");
            if(f.Returned<=f.Launch)throw new ArgumentException("회수 완료는 발사 이후여야 합니다.");
            if(weaponId==OrviusHoverState.WeaponId&&!f.Charged)throw new ArgumentException("오르비우스 일반 투척은 매달기 투척에서 설정하세요.");
            if(f.BufferedAt is {} buffered)MeleeEngine.Range(buffered,0,f.Launch,"다음 투척 입력");
            if(f.RecallAt is {} recall)MeleeEngine.Range(recall,f.Launch,f.Returned,"귀환 시작");
            if(f.FirstLegDistance is {} travel)MeleeEngine.Range(travel,0,10000,"첫 접촉까지 이동 거리");
            if(f.Contacts is {Length:>256})throw new ArgumentException("한 투척의 접촉은 최대256개입니다.");
            foreach(var c in f.Contacts??[])
            {
                MeleeEngine.Range(c.Time,f.Launch,f.Returned,"투척 접촉 시각");if(c.Time>=f.Returned)throw new ArgumentException("접촉은 회수 완료보다 먼저 일어나야 합니다.");
                if(!Enum.IsDefined(c.Kind))throw new ArgumentException("투척 접촉 종류 오류");
                if(c.Surface is {} p){ValidatePoint(p);if(c.Kind!=GlaiveContactKind.Bounce)throw new ArgumentException("지형 접촉은 반사로 설정하세요. 파워 스로우는 지형을 관통하지 않습니다.");}
                else MeleeEngine.Range(c.Target,1,targets,"접촉 대상");
                if(c.Thickness is {} width)MeleeEngine.Range(width,.001,10000,"관통 두께");
            }
            if(f.Detonation is {} d)
            {
                MeleeEngine.Range(d.Time,f.Launch,f.Returned,"수동 기폭");if(d.Time>=f.Returned)throw new ArgumentException("수동 기폭은 회수 완료보다 먼저 일어나야 합니다.");
                if(d.Position is {} p)ValidatePoint(p);else MeleeEngine.Range(d.CenterTarget,1,targets,"기폭 중심 대상");
            }
        }
        foreach(var actor in flights.GroupBy(f=>f.Hologram)){var state=new GlaiveFlightState();foreach(var f in actor.OrderBy(f=>f.Launch))state.Launch(f);}
    }
    private static void ValidatePoint(GlaivePoint p){foreach(var n in new[]{p.X,p.Y,p.Z})MeleeEngine.Range(n,-10000,10000,"기폭/지형 좌표");}
    public void Launch(GlaiveFlight f)
    {
        if(last is not null&&f.Launch<last.Returned)throw new ArgumentException("이전 글레이브 회수 후에 다음 투척을 시작하세요.");
        if(f.BufferedAt is {} buffered)
        {
            if(last is null||buffered<last.Launch||buffered>=last.Returned)throw new ArgumentException("연속 투척은 이전 발사부터 회수 직전 사이에 입력해야 합니다.");
            Consecutive=Math.Min(3,Consecutive+1);
        }
        else Consecutive=0;
        last=f;bounces=0;returnAt=f.RecallAt??double.PositiveInfinity;
    }
    public void Detonate(double time){if(!InFlight(time))throw new ArgumentException("비행 중에만 기폭할 수 있습니다.");returnAt=Math.Min(returnAt,time);}
    public void Contact(GlaiveContact contact,int bounceLimit,double punchThrough,bool disabled)
    {
        if(!InFlight(contact.Time))throw new ArgumentException("접촉 시점에 글레이브가 비행 중이 아닙니다.");
        if(contact.Kind==GlaiveContactKind.Return)
        {if(contact.Time<returnAt)throw new ArgumentException("귀환 명중 전에 귀환 시작 또는 수동 기폭 시각을 지정하세요.");return;}
        if(contact.Time>=returnAt)throw new ArgumentException("귀환이 시작된 뒤의 명중은 ‘귀환 관통’으로 설정하세요.");
        if(contact.Kind==GlaiveContactKind.PunchThrough)
        {
            if(disabled||punchThrough<=0)throw new ArgumentException("현재 투척은 전진 두께 관통이 불가능합니다. 볼래틸은 전진 관통을 비활성화합니다.");
            if(contact.Thickness is {} width&&width>punchThrough+1e-9)throw new ArgumentException("입력한 몸체 두께가 글레이브 관통 거리를 초과합니다.");
            return;
        }
        bounces++;if(bounces>=Math.Max(1,bounceLimit))returnAt=contact.Time;
    }
    internal static JsonElement Raw(Build b,string name)=>MeleeRuleCatalog.Load().Weapon(b.WeaponId).Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")==name);
    public static Calculation Calculate(Catalog catalog,Build b,string attack,bool useInitialComboFloor=true)
    {
        if(!Applies(b.WeaponId))throw new ArgumentException("글레이브 또는 울프 슬레지가 필요합니다.");
        var input=b with {Stance=null,Scenario=b.Scenario with {MotionMultiplier=1},Melee=b.Melee! with {Attack=attack,TargetIndex=1,Finisher=null}};
        return MeleeEngine.CalculateAttack(catalog,input,useInitialComboFloor:useInitialComboFloor,component:new(Raw(b,attack),MeleeEngine.AttackPolicy(b.WeaponId,attack) with {FollowThrough=false}));
    }
    public static double SpeedMultiplier(Build b,Calculation c)
    {
        var power=b.Slots.SingleOrDefault(s=>s?.ModId==PowerThrowId);
        if(power is {Rank:<5})throw new CombatRuleUnavailableException("파워 스로우 투사체 속도는 최대 랭크+60%만 공식 자료에 명시되어 있습니다. 낮은 랭크의 첫 비행 시간은 확정하지 않습니다.");
        return 1+c.Melee!.Effects.GetValueOrDefault("ProjectileSpeed")+(power is null?0:.6);
    }
    public static void CheckFirstLeg(Build b,GlaiveFlight f,Calculation c)
    {
        if(f.FirstLegDistance is not {} travel)return;
        if(b.WeaponId==WolfSledgeFlight.WeaponId)return; // Range validated; absolute speed is not documented.
        var first=(f.Contacts??[]).Select(x=>x.Time).DefaultIfEmpty(f.Detonation?.Time??f.Returned).Min();
        first=Math.Min(first,f.Detonation?.Time??double.PositiveInfinity);
        var speed=Raw(b,Attack(f,weaponId:b.WeaponId)).Number("ShotSpeed")*SpeedMultiplier(b,c);
        if(speed<=0)throw new CombatRuleUnavailableException("이 투척의 절대 비행 속도는 원문에 없습니다.");
        if(first+1e-9<f.Launch+travel/speed)throw new ArgumentException("첫 접촉/기폭이 투사체 속도로 정한 최단 비행보다 빠릅니다.");
    }
    internal static string Signature(Catalog catalog,Build b,string attack)
    {
        var m=b.Melee!;var neutral=b with {Scenario=b.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},Arcanes=(b.Arcanes??new()) with {Frozen=false},Melee=m with {Faction="Neutral",Statuses=[],Debuffs=[],Shields=0,Overguard=0,HeatArmorStrip=0,Distance=0,Conditions=(m.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary()}};
        var c=Calculate(catalog,neutral,attack,useInitialComboFloor:false);return JsonSerializer.Serialize(new {c.Damage,c.CriticalChance,c.CriticalMultiplier,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
    }
}
