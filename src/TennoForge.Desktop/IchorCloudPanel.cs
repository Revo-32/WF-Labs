using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record IchorEditor(TextBox Time,TextBox Cloud,TextBox Target);
    private readonly List<IchorEditor> ichorEditors=[];
    private readonly StackPanel ichorRows=new();
    private Expander? ichorPanel;
    private FrameworkElement CreateIchorPanel()
    {
        var note=T("처치 위치에 7초·4m 구름이 생깁니다. 위키에 피해 간격이 없어 지정한 명중만 계산합니다. 구름 번호는 실제 생성 순서이며, 전투 기록에서 확인할 수 있습니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        ichorPanel=new(){Header="독 구름 명중 · 시간 직접 지정",IsExpanded=false,Visibility=Visibility.Collapsed,Content=Stack(note,ichorRows,Button("＋ 구름 명중",()=>{AddIchorContact(new(.1));Recalculate();}))};return ichorPanel;
    }
    private void AddIchorContact(IchorCloudContact value)
    {
        TextBox Input(double n)=>new(){Text=n.ToString(CultureInfo.InvariantCulture)};
        var editor=new IchorEditor(Input(value.Time),Input(value.Cloud),Input(value.Target));var row=new DockPanel{Margin=new(0,8,0,10)};
        var remove=Button("×",()=>{ichorEditors.Remove(editor);ichorRows.Children.Remove(row);Recalculate();});remove.Width=30;remove.Padding=new(4);remove.Margin=new(6,0,0,0);remove.ToolTip="명중 삭제";DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);
        var grid=new Grid();var fields=new[]{Field("시각 (초)",editor.Time),Field("구름 번호",editor.Cloud),Field("대상 번호",editor.Target)};
        for(var i=0;i<fields.Length;i++){grid.ColumnDefinitions.Add(new());var field=(FrameworkElement)fields[i];field.Margin=new(0,0,i==2?0:6,0);Grid.SetColumn(field,i);grid.Children.Add(field);}row.Children.Add(grid);
        ichorEditors.Add(editor);ichorRows.Children.Add(row);foreach(var box in new[]{editor.Time,editor.Cloud,editor.Target})box.TextChanged+=(_,_)=>Recalculate();
    }
    private IchorCloudContact[] ReadIchorContacts()=>weapon.SelectedItem is not Weapon w||!DualIchorCloudMechanic.Applies(w.Id)?[]:ichorEditors.Select(e=>new IchorCloudContact(Number(e.Time,"구름 명중 시각"),Integer(e.Cloud,"구름 번호"),Integer(e.Target,"대상 번호"))).ToArray();
    private void ApplyIchorContacts(EncounterSetup? setup)
    {ichorEditors.Clear();ichorRows.Children.Clear();foreach(var contact in setup?.IchorClouds??[])AddIchorContact(contact);}
}
