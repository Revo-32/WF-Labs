using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;

public record RangedCubeWaypoint(double AfterLaunch,double Forward,double Side);
public record RangedCubeRoute(double StopX,double StopY,double StopSeconds=.6,RangedCollision[]? Contacts=null,double? DamagedAfterLaunch=null,RangedCubeWaypoint[]? Waypoints=null);
public record RangedCubeEmission(int Component,int Shot,int Target,double X,double Y,int Cube,bool Pierced=false,bool Headshot=false);
public static class RangedQuanta
{
    // Pinned Quanta/Quanta Vandal/Tenet Quanta pages in ranged-sources manifest.
    public static bool Applies(ReferenceWeapon w)=>w.Id is "wiki:primary/Quanta" or "wiki:primary/Quanta Vandal" or "wiki:primary/Tenet Quanta";
    public static JsonElement Normalize(ReferenceWeapon w,JsonElement a,int index)
    {
        if(!Applies(w))return a;
        var n=JsonNode.Parse(a.GetRawText())!;
        if(index>=1)n["Trigger"]="Semi-Auto";
        if(index>=2)n["ShotType"]="AoE";
        return JsonSerializer.SerializeToElement(n);
    }
    public static void Validate(RangedConditions c)
    {
        if(c.CubeHitRadius is {} radius&&(!double.IsFinite(radius)||radius<0||radius>10))throw new ArgumentException("큐브 충돌 반경은 0~10m의 시나리오 값이어야 합니다.");
        if(c.CubeLifetimeRule is not ("Unspecified" or "AfterStop" or "AfterLaunch"))throw new ArgumentException("큐브 수명 기준 오류");
        if(c.CubeRoutes is {Length:>100})throw new ArgumentException("큐브 경로는 최대 100개입니다.");
        foreach(var r in c.CubeRoutes??[])
        {
            if(new[]{r.StopX,r.StopY,r.StopSeconds}.Any(x=>!double.IsFinite(x))||r.StopSeconds<=0||r.StopSeconds>300||r.DamagedAfterLaunch is {} trigger&&(!double.IsFinite(trigger)||trigger<0||trigger>300))throw new ArgumentException("큐브 경로의 좌표·정지 시간·외부 기폭 시간을 확인하세요.");
            var previous=0d;foreach(var point in r.Waypoints??[]){if(new[]{point.AfterLaunch,point.Forward,point.Side}.Any(v=>!double.IsFinite(v))||point.AfterLaunch<=previous||point.AfterLaunch>=r.StopSeconds)throw new ArgumentException("큐브 경유점은 발사와 정지 사이의 오름차순 시각이어야 합니다.");previous=point.AfterLaunch;}
            if((r.Contacts??[]).Any(x=>!double.IsFinite(x.AfterImpact)||x.AfterImpact<0||x.AfterImpact>r.StopSeconds||x.Target<0||x.Target>=c.GroupCount))throw new ArgumentException("큐브 충돌은 발사 후 정지 시각 이내이며 대상 번호는 배치한 적 범위여야 합니다.");
        }
    }
    public static RangedCubeRoute[] Routes(RangedBuild b,RangedResult result,int projectiles,int firstTarget)
    {
        var c=b.Conditions??new();Validate(c);
        if(c.CubeRoutes is {} paths)
        {
            if(paths.Length<projectiles)throw new CombatRuleUnavailableException("실제로 발사되는 각 큐브의 경로가 필요합니다. 멀티샷 큐브끼리 충돌하여 흩어지는 궤적은 위키에 수치가 없습니다.");
            return paths.Take(projectiles).ToArray();
        }
        if(projectiles>1&&!c.AllowApproximations)throw new CombatRuleUnavailableException("멀티샷 큐브는 서로 충돌해 흩어질 수 있습니다. 큐브 설정에서 발사체별 경로를 지정하세요.");
        var speed=25*(1+result.Modifiers!.GetValueOrDefault("ProjectileSpeed"));
        var range=15*(1+result.Modifiers!.GetValueOrDefault("ProjectileSpeed"));
        var y=c.TargetsInLine?0:firstTarget*c.GroupSpacing;var distance=Math.Sqrt(b.Distance*b.Distance+y*y);var forward=distance>0?b.Distance/distance:1;var side=distance>0?y/distance:0;
        var contacts=new List<RangedCollision>();
        if(distance<=range)
        {
            if(result.Modifiers!.GetValueOrDefault("PunchThrough")>0&&!c.AllowApproximations)throw new CombatRuleUnavailableException("관통 후 큐브의 감속·최종 정지 위치는 고정되지 않습니다. 큐브의 경로와 몸통 두께를 지정하세요.");
            contacts.Add(new(distance/speed,firstTarget,Headshot:c.Headshot));
        }
        return Enumerable.Range(0,projectiles).Select(_=>new RangedCubeRoute(range*forward,range*side,.6,contacts.ToArray())).ToArray();
    }
}

/// <summary>Explicit trajectory scenarios; no guessed bounce physics or automatic multishot overlap.</summary>
public sealed class RangedQuantaState
{
    private sealed class Cube
    {
        public bool Pierced;public required RangedCubeRoute Route;public int Id,Shot,Contact;public double Spawn,End,PunchThrough;public string Lifetime="Unspecified";
        public (double X,double Y) Position(double time)
        {
            var age=Math.Max(0,time-Spawn);if(age>=Route.StopSeconds)return(Route.StopX,Route.StopY);
            var previous=new RangedCubeWaypoint(0,0,0);
            foreach(var next in (Route.Waypoints??[]).Append(new(Route.StopSeconds,Route.StopX,Route.StopY)))
            {if(age<=next.AfterLaunch){var t=(age-previous.AfterLaunch)/(next.AfterLaunch-previous.AfterLaunch);return(previous.Forward+(next.Forward-previous.Forward)*t,previous.Side+(next.Side-previous.Side)*t);}previous=next;}
            return(Route.StopX,Route.StopY);
        }
        public double Next=>Math.Min(End,Math.Min(Route.DamagedAfterLaunch is {} trigger?Spawn+trigger:double.PositiveInfinity,Contact<(Route.Contacts?.Length??0)?Spawn+Route.Contacts![Contact].AfterImpact:double.PositiveInfinity));
    }
    private readonly List<Cube> cubes=[];private int serial;
    public int Count=>cubes.Count;
    public double Next=>cubes.Select(c=>c.Next).DefaultIfEmpty(double.PositiveInfinity).Min();
    public int? HitBeam(double time,double endX,double endY,double hitRadius)
    {
        if(new[]{time,endX,endY,hitRadius}.Any(v=>!double.IsFinite(v))||time<0||hitRadius<0)throw new ArgumentException("큐브 빔 교차 입력 오류");
        var length=endX*endX+endY*endY;if(length<=0)return null;
        var hit=cubes.Where(c=>c.Spawn<=time&&c.End>time).Select(c=>{var p=c.Position(time);var along=(p.X*endX+p.Y*endY)/length;var t=Math.Clamp(along,0,1);var distance=Math.Pow(p.X-t*endX,2)+Math.Pow(p.Y-t*endY,2);return(Cube:c,Along:along,Distance:distance);}).Where(h=>h.Along>=0&&h.Along<=1&&h.Distance<=hitRadius*hitRadius+1e-9).OrderBy(h=>h.Along).ThenBy(h=>h.Cube.Id).FirstOrDefault();
        if(hit.Cube is null)return null;
        hit.Cube.Route=hit.Cube.Route with{DamagedAfterLaunch=time-hit.Cube.Spawn};return hit.Cube.Id;
    }
    public void Launch(double time,int shot,RangedCubeRoute[] routes,double penetration,string lifetime)
    {
        if(!double.IsFinite(time)||time<0||!double.IsFinite(penetration)||penetration<0)throw new ArgumentException("큐브 생성 조건 오류");
        RangedQuanta.Validate(new(CubeLifetimeRule:lifetime,CubeRoutes:routes,GroupCount:32));
        foreach(var path in routes)cubes.Add(new(){Id=++serial,Shot=shot,Spawn=time,Route=path with{Contacts=(path.Contacts??[]).OrderBy(x=>x.AfterImpact).ToArray()},PunchThrough=penetration,Lifetime=lifetime,End=time+(lifetime=="AfterLaunch"?9:path.StopSeconds+8)});
    }
    public RangedCubeEmission[] Advance(double time,Func<int,bool> alive,double? thickness,Func<int,double> blastRadius)
    {
        var emissions=new List<RangedCubeEmission>();
        void Explode(Cube cube,int component,int target)
        {
            if(!cubes.Remove(cube))return;
            var p=cube.Position(time);emissions.Add(new(component,cube.Shot,target,p.X,p.Y,cube.Id));
            var radius=blastRadius(component);
            foreach(var other in cubes.ToArray())
            {var q=other.Position(time);if(Math.Pow(q.X-p.X,2)+Math.Pow(q.Y-p.Y,2)<=radius*radius+1e-9)Explode(other,3,target);}
        }
        foreach(var cube in cubes.ToArray())
        {
            if(!cubes.Contains(cube))continue;
            if(cube.Route.DamagedAfterLaunch is {} trigger&&cube.Spawn+trigger<=time+1e-9){Explode(cube,3,0);continue;}
            if(cube.End<=time+1e-9)
            {
                if(cube.Lifetime=="Unspecified")throw new CombatRuleUnavailableException("큐브 수명은 위키에서 발사 후 9초와 정지 후 8초로 설명됩니다. 큐브 설정에서 기준을 선택하세요.");
                Explode(cube,2,0);continue;
            }
            while(cubes.Contains(cube)&&cube.Contact<cube.Route.Contacts!.Length&&cube.Spawn+cube.Route.Contacts[cube.Contact].AfterImpact<=time+1e-9)
            {
                var contact=cube.Route.Contacts[cube.Contact++];if(contact.Surface||!alive(contact.Target))continue;
                var p=cube.Position(time);emissions.Add(new(1,cube.Shot,contact.Target,p.X,p.Y,cube.Id,cube.Pierced,contact.Headshot));
                if(cube.PunchThrough>0)
                {
                    var width=thickness??throw new CombatRuleUnavailableException("큐브가 적을 통과할 수 있는지 판정할 몸통 두께를 지정하세요.");
                    if(cube.PunchThrough>=width){cube.PunchThrough-=width;cube.Pierced=true;continue;}
                }
                Explode(cube,2,contact.Target);
            }
        }
        return emissions.ToArray();
    }
}
