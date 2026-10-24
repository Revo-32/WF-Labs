using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddHemaControls(RangedConditions c,Func<string,double,Action<double>,FrameworkElement> Number,Func<string,double?,Action<double?>,FrameworkElement> OptionalNumber,Func<string,bool,Action<bool>,CheckBox> Toggle)
    {
        var panel=Stack(Number("내 최대 체력",c.PlayerMaxHealth,v=>storedConditions=storedConditions with{PlayerMaxHealth=v}),Number("교전 시작 체력",c.PlayerHealth,v=>storedConditions=storedConditions with{PlayerHealth=v}),OptionalNumber("체력 소비 시점 · 재장전 진행률 (%)",c.HemaDrainPhase*100,v=>storedConditions=storedConditions with{HemaDrainPhase=v/100}),Text("헤마는 기본 최대 체력의 5%, 네크로패직 비거 장착 시 50%를 빈 탄창 재장전에 사용합니다. 부분 재장전은 회복 탄수에 비례합니다. 정확한 소비 시점은 위키에 없어 선택한 진행률을 사용합니다."));
        var vigor=slots.OfType<ModSlot>().Any(s=>RangedEngine.ModCatalog.Mods.Single(m=>m.Id==s.ModId).Effects.ContainsKey("HealthReloadCriticalStep"));
        if(!vigor){conditionPanel.Children.Add(new Expander{Header="헤마 · 체력과 재장전",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});return;}
        panel.Children.Add(Toggle("호스트 · 비거의 치확·치피 효과 적용",c.HemaHost,v=>storedConditions=storedConditions with{HemaHost=v}));
        panel.Children.Add(Number("시작 시 비거 치확·치피 증가 (%)",c.HemaCriticalBonus*100,v=>storedConditions=storedConditions with{HemaCriticalBonus=v/100}));
        var options=new[]{new ArcaneChoice("Unspecified","부분 탄수 규칙 미선택"),new ArcaneChoice("Down","소수 탄수 버림 시나리오"),new ArcaneChoice("Up","소수 탄수 올림 시나리오")};
        var rounding=new ComboBox{ItemsSource=options,DisplayMemberPath="Name",SelectedItem=options.Single(x=>x.Id==c.HemaPartialRounding),Margin=new(0,4,0,8)};
        rounding.SelectionChanged+=(_,_)=>{if(rounding.SelectedItem is ArcaneChoice value){storedConditions=storedConditions with{HemaPartialRounding=value.Id};Refresh();}};
        panel.Children.Add(Text("체력 부족 시 부분 재장전 · 원문 반올림 규칙 미기재"));panel.Children.Add(rounding);
        conditionPanel.Children.Add(new Expander{Header="헤마 · 체력과 재장전",Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
}
