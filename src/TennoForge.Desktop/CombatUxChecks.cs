using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private ModDragPreview? previewForCheck;
    public object CheckCombatUx()
    {
        var saved=ReadBuild();var checks=new List<string>();
        void Yes(bool x,string name){if(!x)throw new Exception(name);checks.Add(name);}
        try
        {
            attackMode.SelectedIndex=1;encounterChoice.SelectedValue=EncounterMode.Primer;
            clearStatusButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));var before=current!.TargetDps;
            statusButtons[DamageType.Slash].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Yes(current!.TargetDps>before&&coState.Text.Contains("컨오 적용"),"상태 버튼 실제 클릭 → CO 적용 및 DPS 증가");
            var singleType=current.TargetDps;targetStatuses[DamageType.Slash].Text="10";
            Yes(Math.Abs(current.TargetDps-singleType)<.00001,"같은 상태 10중첩을 CO 10종으로 중복 계산하지 않음");
            var roundtrip=System.Text.Json.JsonSerializer.Deserialize<Build>(System.Text.Json.JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;
            ApplyBuild(roundtrip);Yes(SelectedEncounter==EncounterMode.Primer&&targetStatuses[DamageType.Slash].Text=="10","교전 조건·프라이머 중첩 저장 복원");
            encounterChoice.SelectedValue=EncounterMode.FirstHit;
            Yes(ReadBuild().Melee!.Statuses!.Count==0&&ReadBuild().Scenario.ExistingStatusTypes==0&&primerControls!.Visibility==Visibility.Collapsed,"첫 타격 선택 → 프라이머 해제 및 관련 입력 숨김");
            encounterChoice.SelectedValue=EncounterMode.FiveSeconds;SettleSimulationForPreview();
            Yes(encounterResult?.AverageDps is not null&&encounterResult.Seconds==5,"5초 선택 → 시간 기반 평균 DPS 표시");
            encounterChoice.SelectedValue=EncounterMode.TenSeconds;SettleSimulationForPreview();
            Yes(encounterResult?.AverageDps is not null&&encounterResult.Seconds==10,"10초 선택 → 시간 기반 평균 DPS 표시");
            Yes(lichFields!.Visibility==Visibility.Collapsed&&incarnonChoice.Visibility==Visibility.Collapsed,"일반 니카나에 없는 리치·인카논 설정 숨김");
            var rackPosition=board.RackView.TranslatePoint(new Point(),root);var equationPosition=equationView.TranslatePoint(new Point(),root);
            scenarioScroller!.ScrollToBottom();scenarioScroller.UpdateLayout();
            Yes(board.RackView.TranslatePoint(new Point(),root)==rackPosition&&equationView.TranslatePoint(new Point(),root)==equationPosition,"상황 설정 독립 스크롤");
            ApplyBuild(saved);UpdateLayout();
            var card=board.SlotForCheck(0);var originalOpacity=card.Opacity;
            using(var preview=ModDragPreview.Start(card)??throw new Exception("드래그 미리보기 레이어 없음"))
            {
                preview.Move(new(600,300));var first=preview.Position;preview.Move(new(740,370));var second=preview.Position;
                Yes(Math.Abs(second.X-first.X-140)<.1&&Math.Abs(second.Y-first.Y-70)<.1,"반투명 카드가 포인터 이동량을 그대로 따름");
                Yes(!preview.IsHitTestVisible&&Math.Abs(preview.Opacity-.76)<.001&&card.Opacity<.5,"드래그 카드 투명도·원본 흐림·드롭 통과");
                Yes(preview.VisibleSnapshotPixels>1000,"드래그 카드의 실제 비트맵이 비어 있지 않음");
                preview.Move(new(-20,-20));Yes(preview.Visibility==Visibility.Hidden,"창 밖 드래그 미리보기 숨김");
            }
            Yes(card.Opacity==originalOpacity,"드래그 종료·취소 시 원본 투명도 복원");
            return new{passed=true,checks};
        }
        finally{ApplyBuild(saved);scenarioScroller?.ScrollToTop();ResetChartsScroll();SettleSimulationForPreview();}
    }
    public void DragPreviewForCheck(bool show)
    {
        previewForCheck?.Dispose();previewForCheck=null;
        if(show){previewForCheck=ModDragPreview.Start(board.SlotForCheck(0));previewForCheck?.Move(new Point(scenarioPanel!.TranslatePoint(new Point(80,160),root).X,300));}
    }
    public object? DragGeometryForCheck(string path)=>previewForCheck?.Inspect(path);
    public object CheckPointerDragFlow()=>board.CheckPointerDragFlow(root);
    public void RemovalPreviewForCheck(bool show)=>board.RemovalPreviewForCheck(show);
    public void EncounterPreviewForCheck(bool show)
    {
        if(show){attackMode.SelectedIndex=1;encounterChoice.SelectedValue=EncounterMode.FiveSeconds;}
        else{encounterChoice.SelectedValue=EncounterMode.FirstHit;attackMode.SelectedIndex=0;}
        SettleSimulationForPreview();scenarioScroller?.ScrollToTop();ResetChartsScroll();
    }
    public void StanceEncounterPreviewForCheck(bool show)
    {
        attackMode.SelectedIndex=0;encounterChoice.SelectedValue=show?EncounterMode.StanceCycle:EncounterMode.FirstHit;
        SettleSimulationForPreview();scenarioScroller?.ScrollToTop();ResetChartsScroll();
    }
}
