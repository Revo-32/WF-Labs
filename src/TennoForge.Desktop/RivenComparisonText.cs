using TennoForge.Core;

namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    private static string DescribeRivenResult(EncounterResult result,bool includeNotes=true)
    {
        if(result.Unsupported.Length>0)return "계산 범위 확인: "+string.Join(" · ",result.Unsupported);
        string Range(EncounterRange x)=>$"{x.Low:N1}–{x.High:N1}";
        string text;
        if(result.Timeline is {Unsupported.Length:0} timeline)
        {
            text=$"직접 피해 {timeline.DirectDamage:N1} · 상태 피해 {timeline.StatusDamage:N1}";
            if(timeline.Seconds>0)
            {
                string Time(double? value)=>value is {} v?$"{v:N2}초":$"> {timeline.Ttk.Horizon:N1}초";
                text+=$"\n교전 직접 DPS {timeline.DirectDamage/timeline.Seconds:N1}\n직접+상태 평균 DPS {(timeline.DirectDamage+timeline.StatusDamage)/timeline.Seconds:N1}\nTTK 중앙값 {Time(timeline.Ttk.Median)} · P10 {Time(timeline.Ttk.P10)} / P90 {Time(timeline.Ttk.P90)}\n구간 내 처치 {timeline.Ttk.KillFraction:P1} · {timeline.Trials:N0}회 표본";
            }
            else text+=$"\n첫 접촉 처치 {timeline.Ttk.KillFraction:P1} · {timeline.Trials:N0}회 표본\nDPS·TTK는 시간 구간을 선택하면 계산합니다.";
            if(timeline.DamageEstimate?.Approximate95HalfWidth is {} error)text+=$"\n합계 피해 표본 평균의 95% 오차폭 ±{error:N2}";
        }
        else
        {
            text=result.AverageDps is {} d?$"교전 직접 DPS {Range(d)}\n직접 피해 {Range(result.Damage)}":"직접 피해 "+Range(result.Damage)+" · 시간 미확정";
            text+="\nTTK·상태 피해 합계: 이 교전 조건에서는 시간별 계산을 제공하지 않습니다.";
            if(result.Precision?.FirstScenario.Approximate95HalfWidth is {} error)
            {
                text+=$"\n직접 피해 표본 평균의 95% 오차폭 ±{error:N2}";
                if(result.Precision.SecondScenario?.Approximate95HalfWidth is {} other)text+=$" / ±{other:N2}";
            }
        }
        if(includeNotes&&result.Notes.Length>0)text+="\n"+string.Join("\n",result.Notes);
        return text;
    }
}
