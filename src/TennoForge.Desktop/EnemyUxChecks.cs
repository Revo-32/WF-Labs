using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    public object CheckEnemyUx()
    {
        var saved=ReadBuild();var checks=new List<string>();
        void Yes(bool condition,string name){if(!condition)throw new Exception(name);checks.Add(name);}
        try
        {
            scenarioDrawer!.SetOpen(true,false);UpdateLayout();
            factionChoice.SelectedValue="Corpus";enemyLevel.Text="200";
            Yes(enemyChoice.Items.Cast<EnemyChoice>().Where(c=>c.Enemy is not null).All(c=>c.Enemy!.Faction=="Corpus"),"팩션 선택 → 해당 팩션 적만 표시");
            Yes(ReadBuild().Melee!.Shields>0&&ReadBuild().Scenario.Enemy!.Armor==0&&manualEnemyFields!.Visibility==Visibility.Collapsed,"코퍼스 선택 → 실드·체력 자동 계산, 수치 입력 숨김");
            var normal=ReadBuild().Scenario.Enemy!.Health;enemyDifficulties[EnemyDifficulty.SteelPath].IsChecked=true;
            Yes(Math.Abs(ReadBuild().Scenario.Enemy!.Health/normal-2.5)<.00001&&enemyLevel.Text=="200","강철의 길 선택 → 체력 ×2.5, 표시 레벨 유지");
            enemyEximus.IsChecked=true;var eximus=ReadBuild();var guard=eximus.Melee!.Overguard;
            Yes(eximus.EnemySelection!.Eximus&&guard>0&&eximus.Scenario.Enemy!.Health>normal*2.5&&enemyStats.Text.Contains("오버가드"),"엑시무스 선택 → 전용 체력·실드·오버가드 표시");
            enemyLevel.Text="201";Yes(ReadBuild().Melee!.Overguard!=guard,"레벨 변경 → 오버가드 자동 재계산");enemyLevel.Text="200";
            enemyDifficulties[EnemyDifficulty.Normal].IsChecked=true;Yes(ReadBuild().Melee!.Overguard==guard,"난이도 변경 → 오버가드에 체력 배수를 잘못 곱하지 않음");
            enemyDifficulties[EnemyDifficulty.Icebind].IsChecked=true;
            Yes(enemyLevel.Text=="999"&&enemySquadRow!.Visibility==Visibility.Visible&&enemyModeNote.Text.Contains("800%"),"빙주령 선택 → 최소 999레벨·분대 인원·+800% 표시");
            var six=ReadBuild().Scenario.Enemy!.Health;enemySquad.SelectedItem=3;
            Yes(Math.Abs(ReadBuild().Scenario.Enemy!.Health/six-6d/9)<.00001&&enemyModeNote.Text.Contains("500%"),"분대원 변경 → 실제 강화 배수 갱신");
            var roundtrip=System.Text.Json.JsonSerializer.Deserialize<Build>(System.Text.Json.JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;
            ApplyBuild(saved);ApplyBuild(roundtrip);
            Yes(ReadBuild().EnemySelection==roundtrip.EnemySelection&&ReadBuild().Scenario.Enemy==roundtrip.Scenario.Enemy&&ReadBuild().Melee!.Overguard==roundtrip.Melee!.Overguard,"적 ID·레벨·난이도·인원·엑시무스·오버가드 저장 왕복");
            factionChoice.SelectedValue="Scaldra";
            Yes(selectedEnemyDifficulty==EnemyDifficulty.Normal&&enemyDifficulties[EnemyDifficulty.Icebind].IsEnabled==false,"빙주령 미등장 팩션 → 일반 난이도 전환 및 빙주령 비활성");
            enemyLevel.Text="";Yes(current is null,"레벨 비우기 → 오래된 계산 결과 숨김");enemyLevel.Text="200";
            var manual=saved with {EnemySelection=null,Scenario=saved.Scenario with {Enemy=new(12345,678)},Melee=saved.Melee! with {Faction="Neutral",Shields=321,Overguard=123}};
            ApplyBuild(manual);
            Yes(ReadBuild().EnemySelection is null&&ReadBuild().Scenario.Enemy==manual.Scenario.Enemy&&ReadBuild().Melee!.Shields==321&&ReadBuild().Melee!.Overguard==123&&manualEnemyFields!.Visibility==Visibility.Visible,"기존 직접 입력 빌드 → 수치·실드·오버가드 보존");
            ApplyBuild(saved);UpdateLayout();
            enemyChoice.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0){RoutedEvent=MouseEnterEvent});Yes(equationView.Highlighted.Contains("target"),"선택한 적 호버 → DPS 수식 방어층 항 강조");enemyChoice.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0){RoutedEvent=MouseLeaveEvent});
            foreach(var box in new[]{factionChoice,enemyChoice,comboChoice,stanceCombo,attackMode})
            {
                box.ApplyTemplate();box.UpdateLayout();var arrow=Descendants(box).OfType<Path>().Single(p=>p.Name=="Chevron");
                var center=arrow.TranslatePoint(new Point(arrow.ActualWidth/2,arrow.ActualHeight/2),box);
                Yes(Math.Abs(center.Y-box.ActualHeight/2)<=1&&Math.Abs(center.X-(box.ActualWidth-17))<=1,"선택창 벡터 화살표 중심 정렬 · "+AutomationProperties.GetName(box));
                Yes(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(box)),"선택창 용도 이름 · "+AutomationProperties.GetName(box));
            }
            Yes(encounterButtons.Count==5&&encounterButtons.Values.All(b=>IsPresented(b)&&!string.IsNullOrWhiteSpace(b.Content as string)),"교전 구간 버튼 5개 · 용도와 선택 상태 표시");
            Yes(comboChoice.Text.Contains("콤보")&&!comboChoice.Text.Contains("시작"),"시작 12× 대신 콤보 설명과 별도 레이블");
            var expander=targetStatusExpander!;encounterChoice.SelectedValue=EncounterMode.Primer;expander.IsExpanded=false;UpdateLayout();expander.ApplyTemplate();
            var toggle=(ToggleButton)expander.Template.FindName("Toggle",expander);toggle.IsChecked=true;UpdateLayout();
            var disclosure=Descendants(toggle).OfType<Path>().Single(p=>p.Name=="DisclosureChevron");
            Yes(expander.IsExpanded&&disclosure.RenderTransform is RotateTransform{Angle:90},"상세 영역 펼치기 → 실제 내용과 화살표 방향 동기화");
            toggle.IsChecked=false;Yes(!expander.IsExpanded,"상세 영역 접기 → 헤더 전체 토글 동작");
            return new{passed=true,checks};
        }
        finally{ApplyBuild(saved);scenarioScroller?.ScrollToTop();scenarioDrawer!.SetOpen(false,false);SettleSimulationForPreview();}
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++){var child=VisualTreeHelper.GetChild(node,i);yield return child;foreach(var descendant in Descendants(child))yield return descendant;}
    }
    public void EnemyPreviewForCheck(string mode)
    {
        scenarioDrawer!.SetOpen(mode!="normal",false);UpdateLayout();
        factionChoice.SelectedValue="Grineer";RefreshEnemyChoices(EnemyCatalog.Load().Enemies.Single(e=>e.EnglishName=="Heavy Gunner").Id);enemyLevel.Text="200";enemySquad.SelectedItem=6;
        enemyEximus.IsChecked=mode=="eximus";
        enemyDifficulties[mode=="ice"?EnemyDifficulty.Icebind:mode=="steel"?EnemyDifficulty.SteelPath:EnemyDifficulty.Normal].IsChecked=true;
        encounterChoice.SelectedValue=mode=="primer"?EncounterMode.Primer:EncounterMode.FirstHit;RefreshEnemyTarget();
        if(mode=="primer"){statusButtons[DamageType.Viral].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));targetStatusExpander!.IsExpanded=true;scenarioScroller?.ScrollToVerticalOffset(390);}
        else scenarioScroller?.ScrollToTop();SettleSimulationForPreview();
    }
    public void ScrollEncounterControlsForCheck()
    {
        scenarioDrawer!.SetOpen(true,false);UpdateLayout();var field=(FrameworkElement)encounterButtons[SelectedEncounter].Parent;
        field.BringIntoView();UpdateLayout();
    }
}
