namespace TennoForge.Core;

/// <summary>Each fan slam has two radial contacts. The second heavy-slam
/// contact uses normal-slam stats, not a second copy of the heavy component.</summary>
public static class WarfanSlam
{
    public const string Gunsen="/Lotus/Weapons/Tenno/Melee/Warfan/WarfanWeapon";
    public const string GunsenPrime="/Lotus/Weapons/Tenno/Melee/Warfan/PrimeGunsen/PrimeGunsenWeapon";
    public const string Vericres="/Lotus/Weapons/Tenno/Melee/Warfan/TnMoonWarfan/MoonWarfanWeapon";
    public static bool Applies(string id)=>id is Gunsen or GunsenPrime or Vericres or WeaponStanceMechanic.Arum or WeaponStanceMechanic.Quassus or WeaponStanceMechanic.QuassusPrime;
    public static bool Active(WeaponActionInput input)=>input.Mode is "Slam" or "HeavySlam";
    public static string Attack(WeaponActionInput input,int hit=1)=>input.Mode=="HeavySlam"&&hit==1?"Heavy Slam Attack":"Slam Attack";
    public static string Source(string id)=>MeleeRuleCatalog.Load().Weapon(id==WeaponStanceMechanic.QuassusPrime?WeaponStanceMechanic.Quassus:id).Source!.Url;
    public static Stance Resolve(string id,Stance stance)
    {
        StanceCombo Row(bool heavy)=>new(heavy?"Heavy Slam":"Slam",heavy?"강슬램 · 2타":"슬램 · 2타",null,
            [new(1,1,1,1,1,1,1,[],"",AttackOverride:heavy?"Heavy Slam Attack":"Slam Attack"),new(1,1,1,1,1,1,1,[],"",AttackOverride:"Slam Attack")],[],
            AttackOverride:heavy?"Heavy Slam Attack":"Slam Attack",ActionOverride:heavy?"Heavy":"Slam",
            ResolutionNote:"워팬 슬램은2타이며 강슬램의 둘째 타격은 일반 슬램 수치입니다. 각 반경·감쇠·강제 상태를 따로 적용합니다. 접촉 시각은 위키 미기재입니다. "+Source(id));
        return stance with {Combos=stance.Combos.Where(c=>c.Key is not ("Slam" or "Heavy Slam")).Concat([Row(false),Row(true)]).ToArray()};
    }
    public static WeaponActionEvent[] Schedule(string id,WeaponActionInput input,int targets)
    {
        if(!Applies(id)||!Active(input))throw new ArgumentException("워팬 슬램 입력은 워팬 전용입니다.");
        var contacts=input.Contacts??[];
        if(contacts.Length>2)throw new ArgumentException("워팬 슬램은 한 입력에서 최대2타입니다. 각 타격은 반경 안의 모든 대상에 적용합니다.");
        foreach(var c in contacts)
        {
            MeleeEngine.Range(c.Time,input.Start,300,"슬램 명중 시각");MeleeEngine.Range(c.Target,1,targets,"슬램 중심 대상");MeleeEngine.Range(c.SlamHit,1,2,"슬램 타격 번호");
            if(c.Blade||c.Surface||c.Travel!=0||c.Launched is not null)throw new ArgumentException("슬램에는 투사체/칼날 비행 경로를 지정할 수 없습니다.");
        }
        if(contacts.Select(c=>c.SlamHit).Distinct().Count()!=contacts.Length)throw new ArgumentException("같은 슬램 타격을 중복 지정하지 마세요.");
        if(contacts.Length==2&&contacts.Single(c=>c.SlamHit==2).Time<=contacts.Single(c=>c.SlamHit==1).Time)throw new ArgumentException("두 번째 슬램은 첫 번째보다 나중에 명중해야 합니다. 위키에 없는 간격은 입력한 시나리오를 사용합니다.");
        return contacts.Select(c=>new WeaponActionEvent(c.Time,1,input,c)).Prepend(new(input.Start,0,input)).ToArray();
    }
    public static Calculation Calculate(Catalog catalog,Build build,WeaponActionInput input,WeaponActionContact? contact,double distance)
    {
        var attack=Attack(input,contact?.SlamHit??1);
        return MeleeEngine.CalculateAttack(catalog,build with {Stance=null,Scenario=build.Scenario with {MotionMultiplier=1},Melee=build.Melee! with {Attack=attack,Distance=distance,TargetIndex=1,Finisher=null,Tennokai=false}},useInitialComboFloor:false);
    }
}
