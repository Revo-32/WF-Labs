using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    public object CheckEquipmentUx()
    {
        var saved=ReadBuild();var checks=new List<string>();
        void Yes(bool condition,string name){if(!condition)throw new Exception(name);checks.Add(name);}
        FrameworkElement Open(string kind)
        {
            OpenEquipmentPicker(kind,false);var content=(FrameworkElement)equipmentPicker!.Child;
            content.Measure(new Size(420,600));content.Arrange(new Rect(0,0,420,600));content.UpdateLayout();return content;
        }
        try
        {
            Yes(new[]{exilusSlotButton!,meleeSlotButton!,zawSlotButton!}.All(b=>b.Visibility==Visibility.Visible&&b.ActualWidth>100&&b.ActualHeight>=44),"엑실러스·근접 아케인·조우 슬롯의 동시 배치와 클릭 영역");
            Yes(LayoutDiagnostics.Inspect(equipmentSlots!).Length==0,"추가 슬롯의 제목·장착 이름·랭크 글자 경계");
            encounterChoice.SelectedValue=EncounterMode.FirstHit;
            var content=Open("melee");var name=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Exposure").Name;
            var search=Descendants(content).OfType<TextBox>().Single();search.Text="Melee Exposure";content.UpdateLayout();
            var choice=Descendants(content).OfType<Button>().Single(b=>b.Content is StackPanel p&&p.Children.OfType<TextBlock>().Any(t=>t.Text.Contains(name)));
            choice.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Yes(ReadBuild().Arcanes?.Melee is not null&&meleeSlotName.Text==name,"근접 아케인 목록 검색 → 항목 클릭 → 슬롯과 계산 설정 연결");
            Yes(arcaneExpander!.IsExpanded&&arcaneConditionFields["Exposure"].Visibility==Visibility.Visible,"장착한 익스포저의 발동 횟수 입력 표시");
            encounterChoice.SelectedValue=EncounterMode.FiveSeconds;
            Yes(arcaneConditionFields["Exposure"].Visibility==Visibility.Collapsed,"시간 교전은 정적 익스포저 중첩 대신 전투 중 발동 조건 사용");
            encounterChoice.SelectedValue=EncounterMode.FirstHit;
            Open("melee");arcaneEditors[0].Rank.SelectedItem=2;Yes(ReadBuild().Arcanes!.Melee!.Rank==2&&meleeSlotRank.Text.Contains("2/5"),"아케인 랭크 변경 → 저장 값·슬롯 표시 반영");CloseEquipmentPicker();
            content=Open("exilus");choice=Descendants(content).OfType<Button>().First(b=>b.Content is StackPanel);choice.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Yes(ReadBuild().Melee!.Exilus is not null&&exilusSlotName.Text!="＋ 장착","엑실러스 목록 클릭 → 독립 슬롯 장착");
            Open("exilus");exilusRank.SelectedItem=0;exilusPolarity.SelectedValue="AP_ATTACK";
            Yes(ReadBuild().Melee!.Exilus!.Rank==0&&ReadBuild().Melee!.ExilusPolarity=="AP_ATTACK","엑실러스 랭크·극성 저장");CloseEquipmentPicker();
            var roundtrip=System.Text.Json.JsonSerializer.Deserialize<Build>(System.Text.Json.JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!;
            ApplyBuild(saved);ApplyBuild(roundtrip);Yes(ReadBuild().Melee!.Exilus==roundtrip.Melee!.Exilus&&ReadBuild().Arcanes!.Melee==roundtrip.Arcanes!.Melee&&exilusSlotName.Text!="＋ 장착","장착·랭크·극성 왕복 및 상시 슬롯 표시 복원");
            for(var i=0;i<3;i++){Open("exilus");Open("melee");CloseEquipmentPicker();}checks.Add("선택창 반복 전환·닫기: 컨트롤 부모 중복 없음");
            content=Open("melee");Descendants(content).OfType<Button>().Single(b=>b.Content is string s&&s=="해제").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Yes(ReadBuild().Arcanes?.Melee is null&&meleeSlotName.Text=="＋ 장착","근접 아케인 해제");
            content=Open("zaw");Yes(Descendants(content).OfType<Button>().Where(b=>b.Content is StackPanel).All(b=>!b.IsEnabled),"조우 미지원 장착을 일반 무기에 허용하지 않음");
            return new{passed=true,checks};
        }
        finally{CloseEquipmentPicker();ApplyBuild(saved);arcaneExpander!.IsExpanded=false;scenarioScroller?.ScrollToTop();}
    }
    public FrameworkElement EquipmentPickerForCheck()
    {OpenEquipmentPicker("melee",false);return (FrameworkElement)equipmentPicker!.Child;}
    public void CloseEquipmentPickerForCheck()=>CloseEquipmentPicker();
}
