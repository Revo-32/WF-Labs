namespace TennoForge.Core;
public record CannonadeFalloff(double Distance,double Multiplier);
public record CannonadeScenario(double? OutputMultiplier=null,CannonadeFalloff[]? Falloff=null,bool? RadialTriggers=null,bool FixedNoncriticalPacket=false);
public static class RangedCannonade
{
    public static void Validate(CannonadeScenario? c)
    {
        if(c is null)return;
        if(c.OutputMultiplier is {} m&&(!double.IsFinite(m)||m<0))throw new ArgumentException("캐노네이드 출력 배율 오류");
        if(c.Falloff is {} p&&(p.Length<2||p[0].Distance!=0||p[^1].Distance!=10||p.Any(v=>!double.IsFinite(v.Distance)||!double.IsFinite(v.Multiplier)||v.Distance<0||v.Distance>10||v.Multiplier<0||v.Multiplier>1)||p.Zip(p.Skip(1)).Any(v=>v.First.Distance>=v.Second.Distance||v.First.Multiplier<v.Second.Multiplier)))throw new ArgumentException("캐노네이드 감쇠는 0~10m의 증가하는 거리·감소하는 배율이어야 합니다.");
    }
    public static double Output(CannonadeScenario? c,double distance)
    {
        if(distance>10)return 0;Validate(c);
        if(c is null||!c.FixedNoncriticalPacket||c.OutputMultiplier is null)throw new CombatRuleUnavailableException("캐노네이드의 모드·치명·상태 브라켓은 원문에 없습니다. 고정 비치명·추가 상태 없음 시나리오와 최종 출력 배율을 지정하세요.");
        var points=c.Falloff??throw new CombatRuleUnavailableException("캐노네이드의 10m 거리 감쇠 곡선은 원문에 없습니다. 거리별 배율 시나리오를 지정하세요.");
        for(int i=1;i<points.Length;i++)if(distance<=points[i].Distance){var lo=points[i-1];var hi=points[i];return 250*c.OutputMultiplier.Value*(lo.Multiplier+(hi.Multiplier-lo.Multiplier)*(distance-lo.Distance)/(hi.Distance-lo.Distance));}
        return 0;
    }
}
