using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddZymosControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        var panel=Stack(Text("몸통 명중은 즉시 61 Toxin 폭발, 약점 명중은 1.1초 후 953 Toxin 폭발과 포자 6개를 생성합니다. 약점 굴착의 지속 피해 수치·포자 비행 속도·무작위 대상 가중치는 위키에 없습니다."));
        var omit=new CheckBox{Content="수치 미확정인 굴착 지속 피해를 제외해 계산",IsChecked=c.ZymosBurrowTicks is {Length:0},Margin=new(0,8,0,8)};omit.Checked+=(_,_)=>{storedConditions=storedConditions with{ZymosBurrowTicks=[]};Refresh();};omit.Unchecked+=(_,_)=>{storedConditions=storedConditions with{ZymosBurrowTicks=null};Refresh();};panel.Children.Add(omit);
        var burrow=Stack(Text("각 사건은 다른 모드·치명 계산을 마친 방어 적용 전 출력입니다. 원문에 없는 값을 자동으로 만들지 않습니다."));
        for(int i=0;i<(c.ZymosBurrowTicks?.Length??0);i++)
        {
            var index=i;var tick=c.ZymosBurrowTicks![i];void EditTick(Func<RangedBurrowTick,RangedBurrowTick> edit){var values=storedConditions.ZymosBurrowTicks!.ToArray();values[index]=edit(values[index]);storedConditions=storedConditions with{ZymosBurrowTicks=values};Refresh();}
            var types=Enum.GetValues<DamageType>().Select(t=>new ArcaneChoice(t.ToString(),Labels.Damage(t))).ToArray();var kind=new ComboBox{ItemsSource=types,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=tick.Type.ToString(),Margin=new(0,4,0,8)};kind.SelectionChanged+=(_,_)=>{if(kind.SelectedValue is string value)EditTick(t=>t with{Type=Enum.Parse<DamageType>(value)});};
            burrow.Children.Add(Stack(Text($"굴착 사건 {i+1}"),Number("직격 후 (초) · 1.1초 미만",tick.Time,v=>EditTick(t=>t with{Time=v??0})),kind,Number("방어 적용 전 피해",tick.Output,v=>EditTick(t=>t with{Output=v??0})),Button("사건 삭제",()=>{storedConditions=storedConditions with{ZymosBurrowTicks=storedConditions.ZymosBurrowTicks!.Where((_,n)=>n!=index).ToArray()};RefreshConditionControls();Refresh();})));
        }
        burrow.Children.Add(Button("＋ 굴착 피해 사건",()=>{storedConditions=storedConditions with{ZymosBurrowTicks=[..storedConditions.ZymosBurrowTicks??[],new(0,DamageType.Toxin,0)]};RefreshConditionControls();Refresh();}));
        panel.Children.Add(new Expander{Header="굴착 피해 · 직접 시나리오 입력",Content=burrow,IsExpanded=c.ZymosBurrowTicks is {Length:>0},Margin=new(0,4,0,12)});
        var orbs=Stack(Text("포자가 별도로 통과하는 감염 구체입니다. 최초 탄환의 통과 구체와 분리합니다. 6개 포자는 아래와 같은 구체 배치를 통과하는 시나리오입니다."));
        for(int i=0;i<(c.ZymosSporeOrbs?.Length??0);i++)
        {
            var index=i;var row=new DockPanel{Margin=new(0,4,0,8)};var remove=Button("×",()=>{storedConditions=storedConditions with{ZymosSporeOrbs=storedConditions.ZymosSporeOrbs!.Where((_,n)=>n!=index).ToArray()};RefreshConditionControls();Refresh();});remove.ToolTip="포자 통과 구체 삭제";DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);
            var choice=new ComboBox{ItemsSource=new[]{"1중첩 구체","2중첩 구체","3중첩 구체"},SelectedIndex=c.ZymosSporeOrbs![i]-1,Margin=new(0,0,8,0)};choice.SelectionChanged+=(_,_)=>{var values=storedConditions.ZymosSporeOrbs!.ToArray();values[index]=choice.SelectedIndex+1;storedConditions=storedConditions with{ZymosSporeOrbs=values};Refresh();};row.Children.Add(choice);orbs.Children.Add(row);
        }
        orbs.Children.Add(Button("＋ 포자 통과 구체",()=>{storedConditions=storedConditions with{ZymosSporeOrbs=[..storedConditions.ZymosSporeOrbs??[],1]};RefreshConditionControls();Refresh();}));panel.Children.Add(new Expander{Header="포자 · 별도 감염 구체 통과",Content=orbs,IsExpanded=c.ZymosSporeOrbs is {Length:>0},Margin=new(0,4,0,12)});
        var explosion=new ComboBox{ItemsSource=new[]{"포자 폭발 · 본문과 Bugs 중 선택 필요","본문 시나리오 · 포자마다 333 Toxin 폭발","Bugs 시나리오 · 포자 폭발 없음"},SelectedIndex=c.ZymosSporeExplosion is null?0:c.ZymosSporeExplosion.Value?1:2,Margin=new(0,0,0,8)};explosion.SelectionChanged+=(_,_)=>{storedConditions=storedConditions with{ZymosSporeExplosion=explosion.SelectedIndex==0?null:explosion.SelectedIndex==1};Refresh();};panel.Children.Add(explosion);
        var head=new CheckBox{Content="초기 약점 폭발이 숙주의 머리에 명중",IsChecked=c.ZymosHeadExplosionHitsHead,Margin=new(0,0,0,8)};head.Checked+=(_,_)=>{storedConditions=storedConditions with{ZymosHeadExplosionHitsHead=true};Refresh();};head.Unchecked+=(_,_)=>{storedConditions=storedConditions with{ZymosHeadExplosionHitsHead=false};Refresh();};panel.Children.Add(head);
        var immune=new CheckBox{Content="기절 면역 대상 · 데몰리셔 등",IsChecked=c.ZymosStunImmune,Margin=new(0,0,0,8)};immune.Checked+=(_,_)=>{storedConditions=storedConditions with{ZymosStunImmune=true};Refresh();};immune.Unchecked+=(_,_)=>{storedConditions=storedConditions with{ZymosStunImmune=false};Refresh();};panel.Children.Add(immune);
        if(c.ZymosSpores is null)panel.Children.Add(Button("포자 6개의 경로 설정",()=>{storedConditions=storedConditions with{ZymosSpores=Enumerable.Range(0,6).Select(_=>new RangedSporePath(0,null)).ToArray()};RefreshConditionControls();Refresh();}));
        else for(int i=0;i<c.ZymosSpores.Length;i++)
        {
            var index=i;var path=c.ZymosSpores[i];panel.Children.Add(Text($"포자 {i+1}"));
            void Edit(Func<RangedSporePath,RangedSporePath> update){var paths=storedConditions.ZymosSpores!.ToArray();paths[index]=update(paths[index]);storedConditions=storedConditions with{ZymosSpores=paths};Refresh();}
            var targets=new ComboBox{ItemsSource=Enumerable.Range(0,c.GroupCount).Select(n=>new ArcaneChoice(n.ToString(),$"대상 {n+1}" )).Prepend(new("-1","빗나감")).ToArray(),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=path.Miss?"-1":path.Target.ToString(),Margin=new(0,0,0,4)};targets.SelectionChanged+=(_,_)=>{if(targets.SelectedValue is string value){var target=int.Parse(value);Edit(p=>p with{Miss=target<0,Target=Math.Max(0,target)});}};panel.Children.Add(targets);
            panel.Children.Add(Number("폭발 후 도착까지 (초) · 지정 시나리오",path.Flight,v=>Edit(p=>p with{Flight=v})));
            var hitHead=new CheckBox{Content="포자 직격 약점 명중",IsChecked=path.Headshot,Margin=new(0,0,0,10)};hitHead.Checked+=(_,_)=>Edit(p=>p with{Headshot=true});hitHead.Unchecked+=(_,_)=>Edit(p=>p with{Headshot=false});panel.Children.Add(hitHead);
        }
        panel.Children.Add(Text("포자는 숙주로부터 20m 안의 적을 선택합니다. 최초 탄환이 받은 구체 버프는 포자로 상속하지 않습니다. 오버가드는 자동으로 기절 면역 처리합니다."));
        conditionPanel.Children.Add(new Expander{Header="자이모스 · 약점과 유도 포자",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
}
