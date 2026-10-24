using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;

// The Wiki gives an approximate lifetime, not a firing/rotation clock or explosion radius.
// Geometry and clock values below are explicit scenarios, never claimed as game constants.
public record AzimaScenario(double? LandingSeconds=null,double CenterX=0,double CenterY=0,double? UnmoddedLifetime=null,double? VolleyRate=null,double? FirstVolley=null,double? RotationSpeed=null,double InitialAngle=0,double? HitRadius=null,double? ExplosionRadius=null,double? ExplosionFalloff=null,bool? InfiniteConsumesMagazine=null);
public static class RangedAzima
{
    public const string Id="wiki:secondary/Azima";
    public static bool Applies(ReferenceWeapon w)=>w.Id==Id;
    public static JsonElement[] Append(ReferenceWeapon w,JsonElement[] attacks)
    {
        if(!Applies(w))return attacks;
        var expiry=JsonNode.Parse(attacks[1].GetRawText())!;expiry["ShotType"]="AoE";expiry["AmmoCost"]=1;
        var launch=JsonNode.Parse(attacks[0].GetRawText())!;launch["AttackIndex"]=3;launch["AttackName"]="터렛 투척 · 탄창 전체";launch["Damage"]=new JsonObject{["Blast"]=0};launch["CarrierProjectile"]=1;launch["Trigger"]="Auto";
        return [attacks[0],JsonSerializer.SerializeToElement(expiry),JsonSerializer.SerializeToElement(launch)];
    }
    public static void Validate(AzimaScenario? s,bool complete=false)
    {
        if(s is null){if(complete)throw new CombatRuleUnavailableException("아지마 터렛의 착지·회전·명중 시나리오를 지정하세요.");return;}
        var nonnegative=new[]{s.LandingSeconds,s.FirstVolley,s.HitRadius,s.ExplosionRadius};
        if(nonnegative.Any(v=>v is {} x&&(!double.IsFinite(x)||x<0||x>1000))||new[]{s.CenterX,s.CenterY,s.InitialAngle}.Any(x=>!double.IsFinite(x)||Math.Abs(x)>10000)||s.RotationSpeed is {} rotation&&(!double.IsFinite(rotation)||Math.Abs(rotation)>10000)||s.UnmoddedLifetime is {} life&&(!double.IsFinite(life)||life<=0||life>300)||s.VolleyRate is {} rate&&(!double.IsFinite(rate)||rate<=0||rate>1000)||s.ExplosionFalloff is {} fall&&(!double.IsFinite(fall)||fall<0||fall>1))throw new ArgumentException("아지마 시간·회전·범위 시나리오 오류");
        if(complete&&(s.LandingSeconds is null||s.UnmoddedLifetime is null||s.VolleyRate is null||s.FirstVolley is null||s.RotationSpeed is null||s.HitRadius is null||s.ExplosionRadius is null||s.ExplosionFalloff is null))throw new CombatRuleUnavailableException("아지마는 약 10초의 기본 지속과 4방향 회전만 확인됩니다. 정확한 착지·발사 주기·회전·명중 크기·폭발 범위를 지정하세요.");
        if(s.VolleyRate is {} r&&s.FirstVolley is {} first&&first>1/r)throw new ArgumentException("첫 발사는 한 발사 주기 이내로 입력하세요.");
    }
    public static int RayHits(AzimaScenario s,double elapsed,double x,double y,double range)
    {
        var dx=x-s.CenterX;var dy=y-s.CenterY;int hits=0;
        for(int ray=0;ray<4;ray++)
        {
            var angle=(s.InitialAngle+s.RotationSpeed!.Value*elapsed+ray*90)*Math.PI/180;var along=dx*Math.Cos(angle)+dy*Math.Sin(angle);var perpendicular=Math.Abs(-dx*Math.Sin(angle)+dy*Math.Cos(angle));
            if(along>=0&&along<=range&&perpendicular<=s.HitRadius!.Value+1e-9)hits++;
        }
        return hits;
    }
}
public sealed class RangedAzimaDisc
{
    public int Shot {get;}
    public double Start {get;}
    public double Next {get;private set;}
    public bool Ended {get;private set;}
    private readonly AzimaScenario scenario;
    private readonly double cap;
    private double remaining,phase,previous,rate;
    public RangedAzimaDisc(int shot,double launch,double loaded,double efficiency,double fireMultiplier,AzimaScenario scenario)
    {
        RangedAzima.Validate(scenario,true);if(loaded<1||!double.IsFinite(loaded)||fireMultiplier<=0)throw new ArgumentException("아지마 투척에는 탄창에 1발 이상 필요합니다.");
        this.scenario=scenario;Shot=shot;Start=launch+scenario.LandingSeconds!.Value;cap=Start+30;previous=Start;
        remaining=loaded/75*scenario.UnmoddedLifetime!.Value/(efficiency>=1?1:1-Math.Clamp(efficiency,0,.75));
        phase=1-scenario.FirstVolley!.Value*scenario.VolleyRate!.Value;rate=fireMultiplier;Schedule();
    }
    // Integrate ammunition and firing phase with the previous rate, then use the new live rate.
    public void Refresh(double now,double fireMultiplier)
    {
        if(Ended)return;if(fireMultiplier<=0||!double.IsFinite(fireMultiplier))throw new ArgumentException("아지마 발사 속도 오류");
        if(now>previous){var elapsed=now-previous;remaining-=elapsed*rate;phase+=elapsed*scenario.VolleyRate!.Value*rate;previous=now;}
        rate=fireMultiplier;Schedule();
    }
    private void Schedule()=>Next=Ended?double.PositiveInfinity:Math.Min(cap,previous+Math.Min(Math.Max(0,remaining)/rate,Math.Max(0,1-phase)/(scenario.VolleyRate!.Value*rate)));
    public bool Advance(double now)
    {
        Refresh(now,rate);
        if(now>=cap-1e-9||remaining<=1e-9){Ended=true;Next=double.PositiveInfinity;return false;}
        phase=Math.Max(0,phase-1);Schedule();return true;
    }
}
