using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record CeramicEditor(TextBox Launch,TextBox Impact,TextBox Volley,TextBox Dagger,TextBox Target,TextBox Travel,CheckBox Surface);
    private readonly List<CeramicEditor> ceramicEditors=[];
    private readonly StackPanel ceramicRows=new();
    private readonly CheckBox ceramicOnly=new(){Content="투사체만 계산",IsChecked=true};
    private Expander? ceramicPanel;
    private bool CeramicSelected=>weapon.SelectedItem is Weapon w&&w.Id==CeramicDaggerMechanic.WeaponId;
    private FrameworkElement CreateCeramicPanel()
    {
        var note=T("강공격 한 번에 단검 2개가 발사됩니다. 같은 발사 번호는 콤보를 공유합니다. 단검 1은 레드 라이트 핸드를 받는 단검입니다. 명중 시각과 대상은 아래에서 지정합니다. 표면 충돌은 해당 대상 위치에서 직격 없이 폭발합니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        ceramicOnly.Checked+=(_,_)=>Recalculate();ceramicOnly.Unchecked+=(_,_)=>Recalculate();
        ceramicPanel=new(){Header="세라믹 대거 · 유령 단검",Visibility=Visibility.Collapsed,Content=Stack(note,ceramicOnly,ceramicRows,Button("＋ 강공격 발사",()=>{
            var volley=ceramicEditors.Count==0?1:ceramicEditors.Max(e=>Integer(e.Volley,"발사 번호"))+1;
            AddCeramicContact(new(0,.1,volley,1));AddCeramicContact(new(0,.1,volley,2));Recalculate();
        }))};return ceramicPanel;
    }
    private void AddCeramicContact(CeramicDaggerContact c)
    {
        TextBox Input(double? n)=>new(){Text=n?.ToString(CultureInfo.InvariantCulture)??""};
        var e=new CeramicEditor(Input(c.Launch),Input(c.Impact),Input(c.Volley),Input(c.Dagger),Input(c.Target),Input(c.Travel),new(){Content="표면 충돌",IsChecked=c.Surface});
        var row=new StackPanel{Margin=new(0,10,0,12)};
        FrameworkElement Line(params UIElement[] fields){var grid=new Grid();for(var i=0;i<fields.Length;i++){grid.ColumnDefinitions.Add(new());((FrameworkElement)fields[i]).Margin=new(0,0,i==fields.Length-1?0:6,8);Grid.SetColumn(fields[i],i);grid.Children.Add(fields[i]);}return grid;}
        row.Children.Add(Line(Field("발사 번호",e.Volley),Field("단검 (1·2)",e.Dagger),Field("대상 번호",e.Target)));
        row.Children.Add(Line(Field("발사 (초)",e.Launch),Field("명중 (초)",e.Impact),Field("거리 (m) · 선택",e.Travel)));
        var remove=Button("삭제",()=>{ceramicEditors.Remove(e);ceramicRows.Children.Remove(row);Recalculate();});remove.Height=32;
        row.Children.Add(Line(e.Surface,remove));ceramicEditors.Add(e);ceramicRows.Children.Add(row);
        foreach(var box in new[]{e.Launch,e.Impact,e.Volley,e.Dagger,e.Target,e.Travel})box.TextChanged+=(_,_)=>Recalculate();e.Surface.Checked+=(_,_)=>Recalculate();e.Surface.Unchecked+=(_,_)=>Recalculate();
    }
    private CeramicDaggerContact[] ReadCeramicContacts()=>!CeramicSelected?[]:ceramicEditors.Select(e=>new CeramicDaggerContact(Number(e.Launch,"세라믹 발사 시각"),Number(e.Impact,"세라믹 명중 시각"),Integer(e.Volley,"발사 번호"),Integer(e.Dagger,"단검 번호"),Integer(e.Target,"대상 번호"),e.Surface.IsChecked==true,string.IsNullOrWhiteSpace(e.Travel.Text)?null:Number(e.Travel,"비행 거리"))).ToArray();
    private void ApplyCeramicContacts(EncounterSetup? setup){ceramicEditors.Clear();ceramicRows.Children.Clear();ceramicOnly.IsChecked=setup?.ScriptedAttacksOnly==true;foreach(var c in setup?.CeramicDaggers??[])AddCeramicContact(c);}
}
