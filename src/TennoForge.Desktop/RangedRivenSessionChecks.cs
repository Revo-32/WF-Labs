using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;
internal sealed partial class RangedRivenEditor
{
    internal static object CheckFileUx()
    {
        var checks=new List<string>();
        foreach(var name in new[]{"Braton","Strun","Lato"})
        {
            var weapon=RangedEngine.Weapons.Single(w=>w.EnglishName==name);
            var shape=new RangedRivenRoll(weapon.Id,8,[new("Damage",0),new("StatusChance",0)],RivenBalance.Version);
            var roll=shape with{Traits=shape.Traits.Select(t=>{var range=RangedRivens.Range(shape,t);return t with{Value=Math.Round((range.Minimum+range.Maximum)/2,1)};}).ToArray()};
            var state=RangedRivenWorkshop.CycleExample(new(roll),new(17));
            var document=RangedRivenWorkbench.ParseDocument(JsonSerializer.Serialize(new RangedRivenDocument(state,roll),Catalog.Json));
            var editor=new RangedRivenEditor(weapon,document.Workshop.Current,_=>{},()=>{},inline:true);editor.Import(document);
            var saved=editor.Export();
            if(saved.Workshop.Current.BaseDrain is not null||saved.Workshop.Current.Polarity is not null||saved.Workshop.Pending is null||saved.Baseline is null||editor.apply.IsEnabled)throw new InvalidOperationException("선택적 장착 정보 파일 복원 오류");
            checks.Add(name+" 장착 정보 없는 미채택 작업·비교 기준 복원");
            var raw=RangedRivenWorkbench.ParseDocument(JsonSerializer.Serialize(roll with{BaseDrain=10,Polarity="Madurai"},Catalog.Json));
            if(!raw.Workshop.Current.Traits.SequenceEqual(roll.Traits))throw new InvalidOperationException("기존 리벤 파일 호환 오류");checks.Add(name+" 기존 단일 리벤 파일 호환");
            try{RangedRivenWorkbench.ParseDocument(JsonSerializer.Serialize(document with{Baseline=roll with{Version="old"}},Catalog.Json));throw new InvalidOperationException("잘못된 비교 기준 파일을 허용함");}catch(ArgumentException){}
            checks.Add(name+" 현재 편집기를 교체하기 전 잘못된 기준 거부");
        }
        return new{passed=true,checks};
    }
    internal void PrepareRecoveryCheck(bool withPending)
    {
        if(withPending)SetWorkshop(RangedRivenWorkshop.CycleExample(ReadSession(),new(7)));
        else{session=null;rows[0].Value.Text="입력 중";}
    }
    internal async Task<object> CheckWorkshopAsync()
    {
        var checks=new List<string>();void Check(string name,bool pass){if(!pass)throw new InvalidOperationException(name);checks.Add(name);}
        samples.ItemsSource=new[]{32,256,1024,4096};samples.SelectedIndex=0;
        var original=ReadWorkshop();baseline=original;sections.Select(3);RefreshWorkshopControls();
        var noMetadata=original with{BaseDrain=null,Polarity=null};
        var imported=RangedRivenWorkbench.ParseDocument(JsonSerializer.Serialize(new RangedRivenDocument(new(noMetadata),noMetadata),Catalog.Json));
        var optionalEditor=new RangedRivenEditor(weapon,imported.Workshop.Current,_=>{},()=>{},inline:true);optionalEditor.Import(imported);
        Check("장착 정보 없는 파일의 편집기 복원",optionalEditor.ReadWorkshop().BaseDrain is null&&optionalEditor.ReadWorkshop().Polarity is null);
        var legacy=RangedRivenWorkbench.ParseDocument(JsonSerializer.Serialize(original,Catalog.Json));Check("기존 단일 리벤 파일 호환",legacy.Workshop.Current.Traits.SequenceEqual(original.Traits));
        baselineButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        locked.SelectedIndex=1;cycleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("순환 버튼 · 미채택 결과 · 고정 옵션 비용",session?.Pending is not null&&session.KuvaSpent==1800);
        Check("미채택 중 입력 잠금",!rows[0].Value.IsEnabled&&!rank.IsEnabled&&!apply.IsEnabled);
        var saved=JsonSerializer.Deserialize<RangedRivenDocument>(JsonSerializer.Serialize(Export(),Catalog.Json),Catalog.Json)!;
        Import(saved);var draft=CaptureDraft();RestoreDraft(draft);Check("저장 복원 · 고정과 미채택 결과",session?.Pending is not null&&session.Locked==saved.Workshop.Locked);
        await CompareAsync();Check("미채택 후보 DPS·TTK·그래프",comparisonResults.Children.OfType<RivenCombatPlot>().Any()&&comparisonStatus.Text.Contains("비교 완료"));
        int resultCount=comparisonResults.Children.Count;sections.Select(0);sections.Select(3);Check("탭 이동은 비교 보존",comparisonResults.Children.Count==resultCount);
        rows[0].Value.Text="잘못된 값";Check("입력 변경은 오래된 비교 제거",comparisonResults.Children.Count==0);Import(saved);
        SetWorkshop(RangedRivenWorkshop.Resolve(session!,false));Check("현재 옵션 유지 · 비용은 보존",session!.KuvaSpent==1800&&session.Pending is null&&rows[0].Value.IsEnabled);
        var shape=original with{Traits=[new("Damage",0),new("StatusChance",0)]};shape=shape with{Traits=shape.Traits.Select(t=>{var r=RangedRivens.Range(shape,t);return t with{Value=Math.Round((r.Minimum+r.Maximum)/2,1)};}).ToArray()};
        SetWorkshop(new(shape));sections.Select(1);var splice=splices.Children.OfType<Button>().FirstOrDefault();Check("접합 실행 버튼 표시",splice is not null);splice!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check("접합 결과와 소비 기록",session!.Current.Traits.Any(t=>t.Spliced)&&session.SplicersSpent==1);
        preserve.IsChecked=true;cycleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check("접합 유지 순환",session.Pending!.Traits.Any(t=>t.Spliced));SetWorkshop(RangedRivenWorkshop.Resolve(session,true));
        SetWorkshop(new(original));baseline=original;RefreshWorkshopControls();await CompareAsync();Check("같은 리벤 비교 완료",comparisonResults.Children.OfType<RivenCombatPlot>().Any());
        return new{passed=checks.Count,checks};
    }
}
