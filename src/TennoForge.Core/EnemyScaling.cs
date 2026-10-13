using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public enum EnemyDifficulty { Normal, SteelPath, Icebind }
// Level is always the level displayed above the enemy, including Steel Path.
public record EnemySelection(string EnemyId,int Level=200,EnemyDifficulty Difficulty=EnemyDifficulty.Normal,int SquadSize=6,string DataVersion=EnemyCatalog.Version,bool Eximus=false);
public record EnemyDefinition(string Id,string Name,string EnglishName,string Faction,string ScalingFaction,int BaseLevel,double Health,double Armor,double Shields,WikiSource Source,string WikiUrl,double? EximusHealth=null,double? EximusShields=null,double? EximusArmor=null,double? EximusOverguard=null)
{
    public string SelectionLabel=>Name;
    public bool SupportsEximus=>EximusHealth is >0;
}
public record EnemyStats(double Health,double Armor,double Shields,double BaseArmor,double UncappedArmor,double StatMultiplier,string[] Notes,double Overguard=0);
public record EnemyCatalog(string RetrievedAt,string Scope,EnemyDefinition[] Enemies,WikiSource[] Sources)
{
    public const string Version="wiki-enemies-0.6.3";
    private static readonly Lazy<EnemyCatalog> Instance=new(()=>
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.enemy-catalog.json")??throw new InvalidOperationException("적 자료가 없습니다.");
        return JsonSerializer.Deserialize<EnemyCatalog>(stream,Catalog.Json)??throw new InvalidOperationException("적 자료 형식 오류");
    });
    public static EnemyCatalog Load()=>Instance.Value;
    public EnemyDefinition Get(string id)=>Enemies.SingleOrDefault(e=>e.Id==id)??throw new ArgumentException("지원하지 않는 적 식별자입니다.");
}

public static class EnemyScaling
{
    // The Wiki specifies binary32, including cancellation at the endpoints.
    // Keep f1 + (f2 - f1) * smoothstep; do not replace with a weighted sum.
    internal static float Curve(int difference,float c1,float e1,float c2,float e2,float lower=70,float upper=80)
    {
        var q=(float)difference;
        var f1=1f+c1*MathF.Pow(q,e1);var f2=1f+c2*MathF.Pow(q,e2);
        var t=Math.Clamp((q-lower)/(upper-lower),0f,1f);var s=t*t*(3f-2f*t);
        var change=(f2-f1)*s;return f1+change;
    }
    public static EnemyStats Calculate(EnemySelection selection)
    {
        if(selection.DataVersion is not (EnemyCatalog.Version or "wiki-enemies-0.6.2"))throw new ArgumentException("적 자료 버전이 다릅니다.");
        var enemy=EnemyCatalog.Load().Get(selection.EnemyId);
        if(selection.Eximus&&!enemy.SupportsEximus)throw new ArgumentException("이 적은 위키 엑시무스 기본 수치가 연결되지 않았습니다.");
        if(selection.Level<enemy.BaseLevel||selection.Level>9999)throw new ArgumentException($"적 레벨은 {enemy.BaseLevel}~9999 사이여야 합니다.");
        if(!Enum.IsDefined(selection.Difficulty)||selection.SquadSize is <1 or >6)throw new ArgumentException("적 난이도 또는 분대 인원이 올바르지 않습니다.");
        if(selection.Difficulty==EnemyDifficulty.Icebind&&selection.Level<999)throw new ArgumentException("빙주령의 적 레벨은 999 이상입니다.");
        if(selection.Difficulty==EnemyDifficulty.Icebind&&enemy.Faction is not ("Grineer" or "KuvaGrineer" or "Corpus" or "CorpusAmalgam" or "Infested" or "InfestedDeimos" or "Orokin" or "TheMurmur"))throw new ArgumentException("이 팩션은 빙주령의 등장 팩션이 아닙니다.");
        var q=selection.Level-enemy.BaseLevel;
        var health=enemy.ScalingFaction switch
        {
            "Grineer" or "Scaldra"=>Curve(q,.015f,2.12f,10.7332f,.72f),
            "Corpus"=>Curve(q,.015f,2.12f,13.4165f,.55f),
            "Infested"=>Curve(q,.0225f,2.12f,16.0998f,.72f),
            "Corrupted" or "Anarchs"=>Curve(q,.015f,2.1f,10.7332f,.685f),
            "Murmur"=>Curve(q,.015f,2f,10.7332f,.5f),
            "Techrot"=>Curve(q,.02f,2.12f,15.0998f,.7f),
            _=>throw new ArgumentException("검증되지 않은 팩션 체력 곡선입니다.")
        };
        var shields=enemy.ScalingFaction switch
        {
            "Corpus"=>Curve(q,.02f,1.76f,2f,.76f),
            "Techrot"=>Curve(q,.02f,1.76f,3.5f,.76f),
            "Grineer"=>Curve(q,.02f,1.75f,1.6f,.75f),
            _=>Curve(q,.02f,1.75f,2f,.75f)
        };
        var multiplier=selection.Difficulty switch {EnemyDifficulty.SteelPath=>2.5f,EnemyDifficulty.Icebind=>3f+selection.SquadSize,_=>1f};
        var baseHealth=(float)(selection.Eximus?enemy.EximusHealth??enemy.Health:enemy.Health);
        var baseShield=(float)(selection.Eximus?enemy.EximusShields??enemy.Shields:enemy.Shields);
        var baseArmor=(float)(selection.Eximus?enemy.EximusArmor??enemy.Armor:enemy.Armor);
        if(selection.Eximus)
        {
            // Wiki infobox revision 2816353 uses level minus the unit's base level.
            var factor=EximusBaseFactor(q);var coefficient=baseShield>0||baseArmor>0?.25f:.375f;
            baseHealth=MathF.Max(baseHealth*1.1f,coefficient*(baseHealth+900f)*factor);
            baseShield*=MathF.Max(1.1f,factor);
        }
        // Wiki gadget 2816353: Icebind modifies BASE armor, then level scaling,
        // then the ordinary 200..2700 cap. This is not a post-cap multiplier.
        baseArmor*=selection.Difficulty==EnemyDifficulty.Icebind?multiplier:1f;
        var rawArmor=baseArmor*Curve(q,.005f,1.75f,.4f,.75f);
        // Enemy Level Scaling explicitly uses current level - 1 for Overguard,
        // independent of the enemy's base level and mission H/S/A multipliers.
        var overguard=selection.Eximus?(float)(enemy.EximusOverguard??12)*Curve(selection.Level-1,.0015f,4,260,.9f,45,50):0;
        var notes=new List<string>{"적 머리 위에 표시되는 레벨 기준 · 강철의 길 +100레벨을 중복 적용하지 않음","몸통 기준 · 엑시무스 고유 오라·특수 피해 감소 제외"};
        if(selection.Eximus)notes.Add("엑시무스 전용 체력·실드 보정 + 레벨별 오버가드. 오버가드에 강철의 길·빙주령 배율을 곱하지 않음");
        if(selection.Difficulty==EnemyDifficulty.Icebind)notes.Add("빙주령 강화: 체력·실드·기본 방어력 +"+(200+100*selection.SquadSize)+"%. 레벨 성장 후 방어력 200~2,700 제한 · 위키 계산 코드 rev.2816353 · 특수 결속 제외");
        return new(baseHealth*multiplier*health,baseArmor>0?Math.Clamp(rawArmor,200f,2700f):0,baseShield*multiplier*shields,baseArmor,rawArmor,multiplier,notes.ToArray(),overguard);
    }
    public static double ArmorAfterFlatReduction(EnemySelection selection,double removed)
    {
        var stats=Calculate(selection);var baseArmor=Math.Max(0,(float)stats.BaseArmor-(float)removed);
        if(baseArmor==0)return 0;
        var q=selection.Level-EnemyCatalog.Load().Get(selection.EnemyId).BaseLevel;
        return MathF.Min(2700f,baseArmor*Curve(q,.005f,1.75f,.4f,.75f));
    }
    private static float EximusBaseFactor(int q)=>q switch
    {
        <=15=>1,<=25=>1+.025f*(q-15),<=35=>1.25f+.125f*(q-25),
        <=50=>2.5f+2f/15f*(q-35),<=100=>4.5f+.03f*(q-50),_=>6
    };
    public static Build Apply(Build build,EnemySelection selection)
    {
        var enemy=EnemyCatalog.Load().Get(selection.EnemyId);var stats=Calculate(selection);
        return build with {EnemySelection=selection with {DataVersion=EnemyCatalog.Version},Scenario=build.Scenario with {Enemy=new(stats.Health,stats.Armor)},Melee=(build.Melee??new()) with {Faction=enemy.Faction,Shields=stats.Shields,Overguard=stats.Overguard},Encounter=(build.Encounter??new()) with {BaseArmor=stats.BaseArmor>0?stats.BaseArmor:null}};
    }
}
