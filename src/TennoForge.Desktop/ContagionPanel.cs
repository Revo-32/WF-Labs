using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private sealed record ContagionEditor(TextBox Time,TextBox Projectile,TextBox Target,TextBox Travel,ComboBox Mode);
    private readonly List<ContagionEditor> contagionEditors=[];
    private readonly StackPanel contagionRows=new();
    private readonly CheckBox contagionOnly=new(){Content="투사체만 계산 · 검 타격 자동 반복 끄기"};
    private Expander? contagionPanel;
    private readonly TextBox[] contagionBuffEditors=Enumerable.Range(0,8).Select(_=>new TextBox{Text="0"}).ToArray();
    private Expander? contagionBuffPanel;
    private bool ContagionEquipped=>selectedExodia is {Active:true} slot&&MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==slot.Id).Code=="Contagion";
    private FrameworkElement CreateContagionPanel()
    {
        contagionOnly.Checked+=(_,_)=>Recalculate();contagionOnly.Unchecked+=(_,_)=>Recalculate();
        var add=Button("＋ 투사체 접촉",()=>{AddContagionCollision(new(0,contagionEditors.Count+1));Recalculate();});
        var note=T("적에 부착하면 1~4초 피해와 5초째 폭발을 자동 계산합니다. 직격으로 처치하면 같은 투사체의 다음 접촉을 추가할 수 있습니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        var buffBody=new StackPanel();var buffNote=T("투사체에 적용된 버프의 실제 피해 증가율(%)입니다. ‘투사체만 계산’에서 구간 내 유지하며, 위력 수치를 다시 곱하지 않습니다.",14,"#596B85");buffNote.TextWrapping=TextWrapping.Wrap;buffBody.Children.Add(buffNote);
        var labels=new[]{"벡스 아머","데스브링어","앰프","리프트 토렌트","리썰 레비테이션","보이드 스트라이크","이클립스","데스 게이트"};
        for(var i=0;i<labels.Length;i+=2)buffBody.Children.Add(GlassLine(Field(labels[i]+" (%)",contagionBuffEditors[i]),Field(labels[i+1]+" (%)",contagionBuffEditors[i+1])));
        foreach(var field in contagionBuffEditors)field.TextChanged+=(_,_)=>Recalculate();
        contagionBuffPanel=new Expander{Header="투사체 외부 피해 버프",Content=buffBody};
        contagionPanel=new(){Header="컨테이전 · 투사체 경로",IsExpanded=true,Visibility=Visibility.Collapsed,Content=Stack(note,contagionOnly,contagionBuffPanel,contagionRows,add)};
        return contagionPanel;
    }
    private void AddContagionCollision(ContagionCollision value)
    {
        TextBox Input(double n)=>new(){Text=n.ToString(CultureInfo.InvariantCulture)};
        var editor=new ContagionEditor(Input(value.Time),Input(value.Projectile),Input(value.Target),Input(value.Travel),new(){ItemsSource=new[]{"적에게 직격","지형에 충돌 · 즉시 폭발"},SelectedIndex=value.Surface?1:0});
        var row=new StackPanel{Margin=new(0,8,0,10)};
        var header=new DockPanel();var remove=Button("×",()=>{contagionEditors.Remove(editor);contagionRows.Children.Remove(row);Recalculate();});remove.Width=30;remove.Padding=new(4);remove.Margin=new(6,0,0,0);remove.ToolTip="접촉 삭제";DockPanel.SetDock(remove,Dock.Right);header.Children.Add(remove);header.Children.Add(editor.Mode);row.Children.Add(header);
        void Pair(string a,TextBox av,string b,TextBox bv)
        {
            var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new());var left=(FrameworkElement)Field(a,av);left.Margin=new(0,0,6,0);grid.Children.Add(left);var right=(FrameworkElement)Field(b,bv);Grid.SetColumn(right,1);grid.Children.Add(right);row.Children.Add(grid);
        }
        Pair("접촉 시각 (초)",editor.Time,"투사체 번호",editor.Projectile);Pair("대상/폭발 중심 번호",editor.Target,"누적 비행 거리 (m)",editor.Travel);
        editor.Time.ToolTip="실제 접촉을 가정하는 시각입니다. 위키에는 절대 비행 속도가 없어 거리를 시간으로 환산하지 않습니다.";
        editor.Projectile.ToolTip="처치 후 계속 비행하는 같은 투사체는 같은 번호를 사용합니다. 새로 발사했다면 새 번호를 지정하세요.";
        editor.Target.ToolTip="1은 기본 대상입니다. 지형 충돌은 선택한 대상의 위치를 폭발 중심으로 사용합니다. 주변 적은 ‘함께 맞는 적’에서 배치합니다.";
        editor.Travel.ToolTip="발사부터 여기까지 이동한 총 거리입니다. 30m 이상에서 랭크·콤보 보너스가 적용됩니다.";
        contagionEditors.Add(editor);contagionRows.Children.Add(row);
        foreach(var input in new[]{editor.Time,editor.Projectile,editor.Target,editor.Travel})input.TextChanged+=(_,_)=>Recalculate();editor.Mode.SelectionChanged+=(_,_)=>Recalculate();
    }
    private ContagionCollision[] ReadContagion()=>!ContagionEquipped?[]:contagionEditors.Select(e=>new ContagionCollision(Number(e.Time,"투사체 접촉 시각"),Integer(e.Projectile,"투사체 번호"),Integer(e.Target,"접촉 대상 번호"),Number(e.Travel,"누적 비행 거리"),e.Mode.SelectedIndex==1)).ToArray();
    private void ApplyContagion(EncounterSetup? setup)
    {
        contagionRows.Children.Clear();contagionEditors.Clear();foreach(var collision in setup?.Contagion??[])AddContagionCollision(collision);contagionOnly.IsChecked=setup?.ScriptedAttacksOnly==true;
    }
    private ContagionDamageBuffs? ReadContagionBuffs()
    {
        if(!ContagionEquipped)return null;var v=contagionBuffEditors.Select(e=>Number(e,"컨테이전 외부 피해 증가율")/100).ToArray();
        var value=new ContagionDamageBuffs(v[0],v[1],v[2],v[3],v[4],v[5],v[6],v[7]);value.Validate();return value.Active?value:null;
    }
    private void ApplyContagionBuffs(ContagionDamageBuffs? buffs)
    {
        var values=(buffs??new()).Values;for(var i=0;i<values.Length;i++)contagionBuffEditors[i].Text=(values[i]*100).ToString(CultureInfo.InvariantCulture);
    }
}
