namespace TennoForge.Core;

// Positions are user-specified world coordinates relative to target 1 (0,0,0).
// They determine secondary-effect radii, not a fabricated melee swing trajectory.
public record CombatEnemy(string Id,double X,double Y=0,double Z=0,EnemySelection? Selection=null,
    Target? Defense=null,double? Shields=null,double? Overguard=null,string? Faction=null,
    Dictionary<DamageType,int>? Statuses=null,TargetDebuff[]? Debuffs=null);
public record CombatEnemyResult(string Id,double DirectDamage,double StatusDamage,double Health,double Shields,
    double Overguard,double StatusTypes,TtkResult Ttk,SampleEstimate Damage,double? DisarmFraction=null);
public record CombatSightBlock(int First,int Second);

public static class CombatScene
{
    public static Build ForEnemy(Build build,CombatEnemy enemy)
    {
        if(string.IsNullOrWhiteSpace(enemy.Id))throw new ArgumentException("주변 적 식별자가 비어 있습니다.");
        foreach(var value in new[]{enemy.X,enemy.Y,enemy.Z})MeleeEngine.Range(value,-10000,10000,"주변 적 좌표");
        var result=enemy.Selection is {} selection?EnemyScaling.Apply(build,selection):build;
        var m=result.Melee??throw new ArgumentException("근접 설정 필요");
        return result with {Scenario=result.Scenario with {Enemy=enemy.Defense??result.Scenario.Enemy,ExistingStatusTypes=0},
            Melee=m with {Shields=enemy.Shields??m.Shields,Overguard=enemy.Overguard??m.Overguard,
                Faction=enemy.Faction??m.Faction,Statuses=enemy.Statuses??[],HeatArmorStrip=0,Debuffs=enemy.Debuffs??[],
                Conditions=(m.Conditions??[]).Where(p=>!EnemyConditionKeys.Contains(p.Key)).ToDictionary()},
            Arcanes=result.Arcanes is {} a?a with {Frozen=false}:null};
    }
    internal static readonly HashSet<string> EnemyConditionKeys=["FinisherExposed","ControlImmune","BurningHateApplied","Impaled","Undamaged","StealthCooldown","Blinded","Unalerted","Lifted","Knockdown","Microwave","Ragdoll"];
    internal static Build[] Validate(Build build)
    {
        var enemies=build.Encounter?.NearbyEnemies??[];
        if(enemies.Length>32)throw new ArgumentException("주변 적은 최대32명입니다.");
        if(enemies.Select(e=>e.Id).Distinct().Count()!=enemies.Length||enemies.Any(e=>e.Id=="primary"))throw new ArgumentException("대상 식별자가 중복되었습니다.");
        var order=build.Encounter?.TargetOrder??[0];
        if(order.Length is <1 or >256||order.Any(i=>i<0||i>enemies.Length))throw new ArgumentException("공격 순서는 존재하는 대상 번호로 지정하세요.");
        if((build.Encounter?.BlockedSight??[]).Any(p=>p.First< -1||p.Second< -1||p.First>enemies.Length||p.Second>enemies.Length||p.First==p.Second))throw new ArgumentException("시야 차단은 서로 다른 두 대상 또는 플레이어(-1)로 지정하세요.");
        return new[]{build}.Concat(enemies.Select(e=>ForEnemy(build,e))).ToArray();
    }
    public static bool HasSight(EncounterSetup? setup,int a,int b)=>(setup?.BlockedSight??[]).All(p=>!(p.First==a&&p.Second==b||p.First==b&&p.Second==a));
    internal static double PlayerDistance(Build build,CombatEnemy? enemy)
        =>Math.Sqrt(Math.Pow(enemy?.X??0,2)+Math.Pow((enemy?.Y??0)+(build.Melee?.Distance??0),2)+Math.Pow(enemy?.Z??0,2));
    internal static double Distance(CombatEnemy? a,CombatEnemy? b)
        =>Math.Sqrt(Math.Pow((a?.X??0)-(b?.X??0),2)+Math.Pow((a?.Y??0)-(b?.Y??0),2)+Math.Pow((a?.Z??0)-(b?.Z??0),2));
}
