using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;
public partial class MainWindow
{
    private sealed record RubbleEditor(TextBox Time,TextBox Target,TextBox Amount,TextBox Missing);
    private readonly CheckBox rubbleEnabled=new(){Content="아틀라스 · 러블"},rubbleNullified=new(){Content="무효화 영역 안에서 시작"};
    private readonly TextBox rubbleArmor=new(){Text="0"},rubbleGrace=new(){Text="0"};
    private readonly StackPanel rubbleRows=new();
    private readonly List<RubbleEditor> rubbleEditors=[];
    private FrameworkElement CreateAtlasRubblePanel()
    {
        var note=T("만피에서는 방어력, 부상 중에는 체력을 회복합니다. 습득 직전 부족 체력을 입력하세요. 현재 대상이 떨어뜨린 러블은 대상 번호로 선택합니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        var details=Stack(FlightLine(Field("시작 러블 (0~1500)",rubbleArmor),Field("감소 유예 (0~2초)",rubbleGrace)),rubbleNullified,note,rubbleRows,Button("＋ 러블 습득",()=>{AddRubblePickup(new(1));Recalculate();}));
        void Show()=>details.Visibility=rubbleEnabled.IsChecked==true?Visibility.Visible:Visibility.Collapsed;
        rubbleEnabled.Checked+=(_,_)=>Show();rubbleEnabled.Unchecked+=(_,_)=>Show();Show();FlightWatch(rubbleEnabled,rubbleNullified,rubbleArmor,rubbleGrace);
        return Stack(rubbleEnabled,details);
    }
    private void AddRubblePickup(RubblePickup p)
    {
        var e=new RubbleEditor(FlightNumber(p.Time),FlightNumber(p.Target),FlightNumber(p.Amount),FlightNumber(p.MissingHealth));var row=new StackPanel{Margin=new(0,8,0,12)};
        row.Children.Add(FlightLine(Field("습득 (초)",e.Time),Field("대상 번호 · 외부 러블은 0",e.Target)));
        var amount=Field("외부 러블의 회복량",e.Amount);row.Children.Add(amount);row.Children.Add(Field("습득 직전 부족 체력 · 만피는 0",e.Missing));
        void Show()=>amount.Visibility=double.TryParse(e.Target.Text,out var n)&&n>0?Visibility.Collapsed:Visibility.Visible;
        e.Target.TextChanged+=(_,_)=>Show();Show();row.Children.Add(Button("삭제",()=>{rubbleEditors.Remove(e);rubbleRows.Children.Remove(row);Recalculate();}));rubbleEditors.Add(e);rubbleRows.Children.Add(row);FlightWatch(e.Time,e.Target,e.Amount,e.Missing);
    }
    private AtlasRubbleSetup? ReadAtlasRubble()=>rubbleEnabled.IsChecked!=true?null:new(Number(rubbleArmor,"시작 러블"),Number(rubbleGrace,"러블 감소 유예"),rubbleNullified.IsChecked==true);
    private RubblePickup[] ReadRubblePickups()=>rubbleEnabled.IsChecked!=true?[]:rubbleEditors.Select(e=>new RubblePickup(Number(e.Time,"러블 습득"),Number(e.Amount,"외부 러블 회복량"),Number(e.Missing,"부족 체력"),Integer(e.Target,"러블 대상"))).ToArray();
    private void ApplyAtlasRubble(AtlasRubbleSetup? s)
    {rubbleEnabled.IsChecked=s is not null;s??=new();rubbleArmor.Text=s.Armor.ToString(System.Globalization.CultureInfo.InvariantCulture);rubbleGrace.Text=s.Grace.ToString(System.Globalization.CultureInfo.InvariantCulture);rubbleNullified.IsChecked=s.Nullified;}
    private void ApplyRubblePickups(EncounterSetup? s)
    {rubbleEditors.Clear();rubbleRows.Children.Clear();foreach(var p in s?.RubblePickups??[])AddRubblePickup(p);}
}
