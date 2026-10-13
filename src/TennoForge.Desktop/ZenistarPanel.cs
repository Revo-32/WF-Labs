using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record DiscEditor(TextBox Launch,TextBox Land,TextBox Target,TextBox Id,TextBox Retrieve,TextBox Aura,TextBox Critical,CheckBox Contact,TextBox RetrieveStarted,ComboBox Actor,TextBox ActorDistance);
    private readonly List<DiscEditor> discEditors=[];
    private readonly StackPanel discRows=new();
    private readonly CheckBox discOnly=new(){Content="원반만 계산",IsChecked=true};
    private Expander? discPanel;
    private bool ZenistarSelected=>weapon.SelectedItem is Weapon w&&w.Id==ZenistarDiscState.WeaponId;
    private FrameworkElement CreateZenistarPanel()
    {
        var note=T("원반이 놓일 대상 위치와 시각을 정합니다. 오라 주기는 위키 본문·표가 달라 명중 시각을 직접 지정합니다. 배치 중 칼날의 피해 구성도 자동으로 바뀝니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        discOnly.Checked+=(_,_)=>Recalculate();discOnly.Unchecked+=(_,_)=>Recalculate();
        discPanel=new(){Header="제니스타 · 원반 배치",Visibility=Visibility.Collapsed,Content=Stack(note,discOnly,discRows,Button("＋ 원반 배치",()=>{AddDisc(new(0,.1,Disc:discEditors.Count+1));Recalculate();}))};return discPanel;
    }
    private void AddDisc(ZenistarDeployment d)
    {
        TextBox Input(double? n)=>new(){Text=n?.ToString(CultureInfo.InvariantCulture)??""};
        var actor=new ComboBox{ItemsSource=new[]{new KeyValuePair<int,string>(0,"플레이어"),new(1,"분신 1"),new(2,"분신 2"),new(3,"분신 3"),new(4,"분신 4")},DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=d.Hologram};
        var e=new DiscEditor(Input(d.Launch),Input(d.Land),Input(d.CenterTarget),Input(d.Disc),Input(d.Retrieve),new(){Text=string.Join(", ",(d.AuraTimes??[]).Select(t=>t.ToString(CultureInfo.InvariantCulture)))},Input(d.WrathfulCriticalPercent),new(){Content="배치 전 대상 직격",IsChecked=d.Contact},Input(d.RetrieveStarted),actor,Input(d.HologramDistance));
        var row=new StackPanel{Margin=new(0,10,0,12)};
        FrameworkElement Line(params UIElement[] fields){var grid=new Grid();for(var i=0;i<fields.Length;i++){grid.ColumnDefinitions.Add(new());((FrameworkElement)fields[i]).Margin=new(0,0,i==fields.Length-1?0:6,8);Grid.SetColumn(fields[i],i);grid.Children.Add(fields[i]);}return grid;}
        row.Children.Add(Field("공격 주체",actor));row.Children.Add(Field("분신에서 배치점까지 (m)",e.ActorDistance));
        row.Children.Add(Line(Field("원반 번호",e.Id),Field("중심 대상 번호",e.Target)));
        row.Children.Add(Line(Field("발사 (초)",e.Launch),Field("배치 (초)",e.Land)));
        row.Children.Add(Line(Field("회수 동작 시작 (초) · 선택",e.RetrieveStarted),Field("회수 완료 (초) · 선택",e.Retrieve)));
        var healingHint=T("라이프 스트라이크는 회수 동작 중 오라 피해에만 발동합니다. 회수 시각을 비워 두면 자연 만료까지 배치합니다.",14,"#596B85");healingHint.TextWrapping=TextWrapping.Wrap;row.Children.Add(healingHint);
        row.Children.Add(Field("오라 명중 (초) · 쉼표로 구분",e.Aura));
        row.Children.Add(Field("발사 시 래스풀 최종 치확 보너스 (%)",e.Critical));
        var remove=Button("삭제",()=>{discEditors.Remove(e);discRows.Children.Remove(row);Recalculate();});remove.Height=32;row.Children.Add(Line(e.Contact,remove));
        discEditors.Add(e);discRows.Children.Add(row);foreach(var box in new[]{e.Launch,e.Land,e.Target,e.Id,e.Retrieve,e.Aura,e.Critical,e.RetrieveStarted,e.ActorDistance})box.TextChanged+=(_,_)=>Recalculate();actor.SelectionChanged+=(_,_)=>Recalculate();e.Contact.Checked+=(_,_)=>Recalculate();e.Contact.Unchecked+=(_,_)=>Recalculate();
    }
    private ZenistarDeployment[] ReadDiscs()=>!ZenistarSelected?[]:discEditors.Select(e=>new ZenistarDeployment(Number(e.Launch,"원반 발사"),Number(e.Land,"원반 배치"),Integer(e.Target,"원반 대상"),Integer(e.Id,"원반 번호"),string.IsNullOrWhiteSpace(e.Retrieve.Text)?null:Number(e.Retrieve,"원반 회수"),e.Aura.Text.Split([',',' ',';'],StringSplitOptions.RemoveEmptyEntries).Select(t=>double.TryParse(t,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?n:throw new ArgumentException("오라 명중 시각은 숫자와 쉼표로 입력하세요.")).ToArray(),e.Contact.IsChecked==true,Number(e.Critical,"원반 저장 치확"),string.IsNullOrWhiteSpace(e.RetrieveStarted.Text)?null:Number(e.RetrieveStarted,"회수 동작 시작"),e.Actor.SelectedValue is int n?n:0,Number(e.ActorDistance,"분신 배치 거리"))).ToArray();
    private void ApplyDiscs(EncounterSetup? setup){discEditors.Clear();discRows.Children.Clear();discOnly.IsChecked=setup?.ScriptedAttacksOnly==true;foreach(var d in setup?.ZenistarDiscs??[])AddDisc(d);}
}
