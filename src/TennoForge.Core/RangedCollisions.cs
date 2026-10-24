using System.Reflection;
using System.Text.Json;
namespace TennoForge.Core;

public record RangedCollisionRule(string WeaponId,int[] Attacks,int MaximumContacts,WikiSource Source);
public static class RangedCollisions
{
    public static readonly RangedCollisionRule[] Rules=Load();
    private static RangedCollisionRule[] Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-collisions.json")!;return JsonSerializer.Deserialize<RangedCollisionRule[]>(s,Catalog.Json)!;}
    public static RangedCollisionRule? Get(string id,int attack)=>Rules.SingleOrDefault(r=>r.WeaponId==id&&r.Attacks.Contains(attack));
    public static int MaximumContacts(RangedBuild b,RangedCollisionRule r)=>r.MaximumContacts==0?int.MaxValue:r.MaximumContacts+(int)b.Slots.OfType<ModSlot>().Select(s=>{var m=RangedEngine.ModCatalog.Mods.Single(m=>m.Id==s.ModId);return m.Effects.GetValueOrDefault("AdditionalBounces")*(s.Rank+1)/(m.MaxRank+1);}).Sum();
    public static void Validate(RangedBuild b,bool requireRoot=true)
    {
        var c=b.Conditions??new();var collisions=c.Collisions??[];RangedFlatExplosions.Validate(c);
        if(collisions.Length>0)
        {
            if(RangedPentaAugments.Applies(RangedEngine.Resolve(b))&&!c.ManualGrenades)throw new ArgumentException("펜타의 추가 도탄 경로는 유탄 배치·수동 기폭 조건과 함께 설정하세요.");
            var r=requireRoot?Get(b.WeaponId,b.AttackIndex):Rules.FirstOrDefault(r=>r.WeaponId==b.WeaponId);
            if(r is null)throw new ArgumentException("선택한 공격은 추가 충돌 경로를 사용하지 않습니다.");
            var maximum=MaximumContacts(b,r);if(collisions.Length>=maximum)throw new ArgumentException($"첫 충돌을 포함해 최대 {maximum}회까지 입력하세요.");
            if(c.GlaiveReturnSeconds is {} back&&collisions.Any(x=>x.AfterImpact>=back))throw new ArgumentException("추가 충돌은 글레이브 회수보다 빨라야 합니다.");
            if(collisions.Any(x=>!double.IsFinite(x.AfterImpact)||x.AfterImpact<=0||x.AfterImpact>300||x.Target<0||x.Target>=c.GroupCount))throw new ArgumentException("추가 충돌의 시각 또는 대상 번호를 확인하세요.");
        }
        if(c.FirstImpactSurface&&!RangedFlatExplosions.Equipped(b)&&(requireRoot?Get(b.WeaponId,b.AttackIndex):Rules.FirstOrDefault(r=>r.WeaponId==b.WeaponId)) is null)throw new ArgumentException("첫 지형 충돌은 도탄 경로가 있는 공격에서 선택할 수 있습니다.");
        if(c.FirstImpactSurface&&c.TargetsInLine)throw new ArgumentException("지형에서 반사하는 경로는 추가 충돌 목록으로 지정하세요. 일렬 몸통 관통과 동시에 자동 연결하지 않습니다.");
        if(collisions.Length>0&&RangedEngine.S(RangedEngine.Resolve(b).Raw,"Family")=="Tetra"&&!c.BouncePathConfirmed)throw new CombatRuleUnavailableException("키네틱 리코셰의 반사 후 수명 감소식은 위키에 없습니다. 입력한 각 충돌까지 투사체가 유지되는 경로임을 상황 설정에서 선택하세요.");
        if(c.GlaiveReturnSeconds is {} delay&&(!double.IsFinite(delay)||delay<=0||delay>300))throw new ArgumentException("글레이브 회수 시간은 0초 초과 300초 이하입니다.");
    }
}

public record RangedClusterRule(string WeaponId,int Parent,int Contact,int Explosion,int Count,WikiSource Source);
public static class RangedClusters
{
    public static readonly RangedClusterRule[] Rules=Load();
    private static RangedClusterRule[] Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-clusters.json")!;return JsonSerializer.Deserialize<RangedClusterRule[]>(s,Catalog.Json)!;}
    public static RangedClusterRule? Get(string id)=>Rules.SingleOrDefault(r=>r.WeaponId==id);
    public static void Validate(RangedBuild b,bool encounter=false)
    {
        var rule=Get(b.WeaponId);var c=b.Conditions??new();
        if(rule is null)return;
        if(encounter&&b.AttackIndex==rule.Parent&&c.ClusterLandings is null)throw new CombatRuleUnavailableException("자탄이 떨어질 위치와 시간을 설정하세요. 확산 궤적은 지형에 따라 달라 위키의 피해 수치만으로 명중 수·시각을 정할 수 없습니다.");
        if(c.ClusterLandings is {} paths&&(paths.Length>rule.Count||paths.Any(x=>!double.IsFinite(x.AfterImpact)||x.AfterImpact<0||x.AfterImpact>300||x.Target<0||x.Target>=c.GroupCount)))throw new ArgumentException($"자탄 착탄은 최대 {rule.Count}개이며, 시각은 0초 이상·대상은 배치한 적 범위여야 합니다.");
    }
}
