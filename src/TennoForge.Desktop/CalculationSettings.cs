using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private record SampleChoice(int Count,string Name);
    private readonly ComboBox sampleChoice=new(){DisplayMemberPath="Name",SelectedValuePath="Count"};
    private readonly TextBlock samplingPrecision=T("",14);
    private Button cancelCalculation=null!;
    private int SelectedSamples=>sampleChoice.SelectedValue is int n?n:2048;

    private FrameworkElement CreateCalculationSettings()
    {
        foreach(var choice in new[]{new SampleChoice(512,"512회"),new SampleChoice(2048,"2,048회 · 기본"),new SampleChoice(4096,"4,096회")})sampleChoice.Items.Add(choice);
        sampleChoice.SelectedValue=2048;
        sampleChoice.SelectionChanged+=(_,_)=>{foreach(var ranged in rangedPages.Values)ranged.RefreshSampling();Recalculate();};
        var hint=T("표본을 늘리면 통계 오차가 줄어듭니다.\n경량 모드에서도 표본 수는 유지됩니다.",14,"#596B85");hint.TextWrapping=TextWrapping.Wrap;
        return Stack(T("계산",16),Field("전투 표본 수",sampleChoice),hint);
    }

    private void ApplySampleSetting(int count)
    {
        if(count is <1 or >4096)throw new ArgumentException("교전 표본 수는 1~4096이어야 합니다.");
        if(!sampleChoice.Items.Cast<SampleChoice>().Any(c=>c.Count==count))sampleChoice.Items.Add(new SampleChoice(count,$"{count:N0}회 · 저장된 값"));
        sampleChoice.SelectedValue=count;
    }

    private FrameworkElement CreateCalculationHeading()
    {
        var row=new DockPanel();
        cancelCalculation=Button("계산 취소",()=>
        {
            generation++;debounce.Stop();encounterCancellation?.Cancel();
            cancelCalculation.Visibility=Visibility.Collapsed;
            encounterValue.Text="계산 취소됨";encounterScope.Text="입력을 변경하면 다시 계산합니다.";ttkMetric.Text="—";
        });
        cancelCalculation.Visibility=Visibility.Collapsed;cancelCalculation.Margin=new(8,0,0,0);cancelCalculation.VerticalAlignment=VerticalAlignment.Top;
        DockPanel.SetDock(cancelCalculation,Dock.Right);row.Children.Add(cancelCalculation);row.Children.Add(encounterHeading);
        return row;
    }

    private void ShowSamplingPrecision(EncounterResult result)
    {
        samplingPrecision.Text="";
        if(result.Precision is not {} precision)return;
        string Error(SampleEstimate e)=>e.Approximate95HalfWidth is {} half?half==0?"표본 내 변동 0":result.Seconds is >0?$"평균 DPS ±{half/result.Seconds:N1}":$"평균 피해 ±{half:N1}":"오차 추정에는 표본 2회 이상 필요";
        samplingPrecision.Text=$"{precision.FirstScenario.Samples:N0}회 표본 · "+(precision.SecondScenario is {} second?$"두 시나리오 {Error(precision.FirstScenario)} / {Error(second)}":Error(precision.FirstScenario));
        samplingPrecision.TextWrapping=TextWrapping.Wrap;
        samplingPrecision.ToolTip="독립 표본 평균의 정규 근사 95% 신뢰구간입니다. 개별 전투 결과의 분포나 규칙의 정확도를 뜻하지 않습니다. 원문에 없는 타격 시각은 별도의 시나리오 차이로 표시합니다.";
    }
}
