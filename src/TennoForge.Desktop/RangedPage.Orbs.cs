using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddOrbShotControls(RangedConditions c,Func<string,double?,Action<double?>,FrameworkElement> Number)
    {
        var panel=Stack(Text("발사 번호로 오브를 조준할 공격을 지정합니다. 해당 발사의 탄약과 시간이 실제로 소비됩니다. 체력·에너지 기본량은 각각 50·25이며 다른 오브는 회복량을 입력하세요."));
        for(int i=0;i<(c.OrbShots?.Length??0);i++)
        {
            var index=i;var row=c.OrbShots![i];
            void Change(Func<RangedOrbShot,RangedOrbShot> edit){var rows=storedConditions.OrbShots!.ToArray();rows[index]=edit(rows[index]);storedConditions=storedConditions with{OrbShots=rows};Refresh();}
            var box=Stack(Number("몇 번째 발사",row.Shot,v=>Change(r=>r with{Shot=(int)(v??1)})),Number("오브 기본 회복량 · 비우면 일반 오브",row.BaseValue,v=>Change(r=>r with{BaseValue=v})),Number("광역 공격 중심에서 오브까지 (m)",row.RadialDistance,v=>Change(r=>r with{RadialDistance=v??0})));
            var attacks=new[]{new ArcaneChoice("-1","현재 선택한 공격")}.Concat(RangedEngine.Attacks(Selected!).Select((a,n)=>new ArcaneChoice(n.ToString(),RangedEngine.S(a,"AttackName")))).ToArray();
            var attack=new ComboBox{ItemsSource=attacks,DisplayMemberPath="Name",SelectedItem=attacks.Single(a=>a.Id==row.Attack.ToString()),Margin=new(0,4,0,8)};attack.SelectionChanged+=(_,_)=>{if(attack.SelectedItem is ArcaneChoice v)Change(r=>r with{Attack=int.Parse(v.Id)});};box.Children.Insert(0,attack);
            var choices=new[]{new ArcaneChoice("Unknown","히트스캔 통과 · 원문 미명시"),new ArcaneChoice("Stop","오브에서 멈춤"),new ArcaneChoice("Continue","오브를 지나 대상 명중")};var choice=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedItem=choices[row.HitscanContinues is null?0:row.HitscanContinues.Value?2:1],Margin=new(0,4,0,8)};choice.SelectionChanged+=(_,_)=>{if(choice.SelectedItem is ArcaneChoice v)Change(r=>r with{HitscanContinues=v.Id=="Unknown"?null:v.Id=="Continue"});};box.Children.Add(choice);
            box.Children.Add(Button("오브 사격 삭제",()=>{storedConditions=storedConditions with{OrbShots=storedConditions.OrbShots!.Where((_,n)=>n!=index).ToArray()};RefreshConditionControls();Refresh();}));
            panel.Children.Add(new Expander{Header=$"{row.Shot}번째 발사 · 오브 회수",Content=box,IsExpanded=true,Margin=new(0,4,0,8)});
        }
        panel.Children.Add(Button("＋ 오브 사격",()=>{storedConditions=storedConditions with{OrbShots=[..storedConditions.OrbShots??[],new((storedConditions.OrbShots??[]).Select(o=>o.Shot).DefaultIfEmpty(0).Max()+1)]};RefreshConditionControls();Refresh();}));
        conditionPanel.Children.Add(new Expander{Header="오브 사격 · 체력·에너지 회수",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
}
