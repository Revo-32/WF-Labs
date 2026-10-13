using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record RapierEditor(TextBox Launch,TextBox Impact,TextBox Volley,TextBox Rapier,TextBox Target);
    private readonly List<RapierEditor> rapierEditors=[];
    private readonly StackPanel rapierRows=new();
    private readonly TextBox rapierStatusAssumption=new();
    private Expander? rapierPanel;
    private bool RapierSelected=>weapon.SelectedItem is Weapon w&&w.Id is MeleeMechanicScope.DestrezaId or MeleeMechanicScope.DestrezaPrimeId;
    private FrameworkElement CreateDestrezaPanel()
    {
        var note=T("한 번에 5개의 레이피어가 2초간 비행합니다. 실제 명중 시각을 지정하면 관통한 대상마다 강제 충격·베기와 컨오를 갱신합니다.",14);note.TextWrapping=TextWrapping.Wrap;
        var missing=T("기본 상확은 위키 미기재입니다. 비우면 무작위 상태를 제외한 부분 피해만 계산합니다. 값을 넣으면 해당 상확을 가정한 조건부 계산입니다.",14);missing.TextWrapping=TextWrapping.Wrap;missing.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");
        var windup=T("강공격 준비 중에 명중하면 그 순간의 콤보·강공격 보너스를 적용합니다. 전투 행동에서 준비 시작/종료와 강공격 완료(빗나감 포함)를 지정하세요. 칼날 접촉은 이 표에 합산하지 않습니다.",13);windup.TextWrapping=TextWrapping.Wrap;
        rapierStatusAssumption.TextChanged+=(_,_)=>Recalculate();
        rapierPanel=new(){Header="데스트레자 · 유령 레이피어",Visibility=Visibility.Collapsed,Content=Stack(note,missing,Field("투사체 기본 상확 가정 (%) · 선택",rapierStatusAssumption),windup,rapierRows,Button("＋ 레이피어 명중",()=>{AddRapierContact(new(0,.1,Rapier:Math.Min(5,rapierEditors.Count+1)));Recalculate();}))};
        return rapierPanel;
    }
    private void AddRapierContact(DestrezaContact c)
    {
        TextBox Input(double n)=>new(){Text=n.ToString(CultureInfo.InvariantCulture)};
        var e=new RapierEditor(Input(c.Launch),Input(c.Impact),Input(c.Volley),Input(c.Rapier),Input(c.Target));
        var row=new StackPanel{Margin=new(0,8,0,10)};
        row.Children.Add(FlightLine(Field("발사 묶음",e.Volley),Field("레이피어 (1~5)",e.Rapier),Field("대상 번호",e.Target)));
        row.Children.Add(FlightLine(Field("발사 시각 (초)",e.Launch),Field("명중 시각 (초)",e.Impact)));
        row.Children.Add(Button("삭제",()=>{rapierEditors.Remove(e);rapierRows.Children.Remove(row);Recalculate();}));
        rapierEditors.Add(e);rapierRows.Children.Add(row);foreach(var box in new[]{e.Launch,e.Impact,e.Volley,e.Rapier,e.Target})box.TextChanged+=(_,_)=>Recalculate();
    }
    private DestrezaContact[] ReadRapierContacts()=>!RapierSelected?[]:rapierEditors.Select(e=>new DestrezaContact(Number(e.Launch,"레이피어 발사"),Number(e.Impact,"레이피어 명중"),Integer(e.Volley,"발사 묶음"),Integer(e.Rapier,"레이피어 번호"),Integer(e.Target,"레이피어 대상"),string.IsNullOrWhiteSpace(rapierStatusAssumption.Text)?null:Number(rapierStatusAssumption,"레이피어 기본 상확 가정")/100)).ToArray();
    private void ApplyRapierContacts(EncounterSetup? setup)
    {
        rapierEditors.Clear();rapierRows.Children.Clear();rapierStatusAssumption.Text=(setup?.DestrezaContacts?.FirstOrDefault()?.AssumedStatusChance*100)?.ToString(CultureInfo.InvariantCulture)??"";
        foreach(var c in setup?.DestrezaContacts??[])AddRapierContact(c);
    }
}
