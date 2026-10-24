using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;
namespace TennoForge.Desktop;
internal sealed partial class RangedPage
{
    private void AddSyndicateControls(RangedConditions c,Func<string,double,Action<double>,FrameworkElement> Number,Func<string,double?,Action<double?>,FrameworkElement> OptionalNumber)
    {
        if(Selected is not {} selected)return;
        var effects=new Dictionary<string,double>();
        foreach(var slot in slots.OfType<ModSlot>())
            foreach(var pair in RangedEngine.ModCatalog.Mods.Single(m=>m.Id==slot.ModId).Effects.Where(p=>p.Key.StartsWith("Syndicate")))effects[pair.Key]=pair.Value;
        if(RangedSyndicates.Resolve(selected,effects) is not {} rule)return;
        var panel=Stack(Text("처치와 공유 숙련도로 충전하고, 발동 후 30초 동안 다시 충전되지 않습니다. 폭발은 플레이어 중심 25m에 적용됩니다."),Number("시작 게이지 (%)",c.SyndicateCharge*100,v=>storedConditions=storedConditions with{SyndicateCharge=v/100}),Number("숙련도 배수 · 부스터 포함",c.AffinityMultiplier,v=>storedConditions=storedConditions with{AffinityMultiplier=v}),Number("장착한 무기 개수 · 공유 숙련도 분배",c.EquippedWeaponCount,v=>storedConditions=storedConditions with{EquippedWeaponCount=(int)v}));
        if(rule.Code=="Justice")
        {
            var options=new[]{new ArcaneChoice("Unspecified","계산 기준 선택 · 위키 내용 불일치"),new ArcaneChoice("StunOnly","총괄 문서 · Blast 상태 없이 기절"),new ArcaneChoice("Blast","Justice 문서 · Blast 강제 상태")};
            var combo=new ComboBox{ItemsSource=options,DisplayMemberPath="Name",SelectedItem=options.Single(v=>v.Id==c.JusticeStatus),Margin=new(0,4,0,8)};
            combo.SelectionChanged+=(_,_)=>{if(combo.SelectedItem is ArcaneChoice v){storedConditions=storedConditions with{JusticeStatus=v.Id};Refresh();}};
            panel.Children.Add(Text("Justice 강제 상태"));panel.Children.Add(combo);
            panel.Children.Add(OptionalNumber("모드 적용 전 기본 방어력",c.PlayerBaseArmor,v=>storedConditions=storedConditions with{PlayerBaseArmor=v}));
        }
        if(rule.Code=="Purity")panel.Children.Add(OptionalNumber("모드 적용 전 기본 체력",c.PlayerBaseHealth,v=>storedConditions=storedConditions with{PlayerBaseHealth=v}));
        if(rule.Code is "Entropy" or "Blight")panel.Children.Add(OptionalNumber("워프레임 랭크 0 기본 에너지",c.PlayerBaseEnergy,v=>storedConditions=storedConditions with{PlayerBaseEnergy=v}));
        if(rule.Code=="Sequence")panel.Children.Add(OptionalNumber("모드 적용 전 기본 실드",c.PlayerBaseShields,v=>storedConditions=storedConditions with{PlayerBaseShields=v}));
        panel.Children.Add(Text("예약 사건의 ‘무기 숙련도 획득’은 분배 후 이 무기에 들어오는 양입니다. 적 처치에서 계산한 숙련도를 중복 입력하지 마세요."));
        conditionPanel.Children.Add(new Expander{Header="신디케이트 · "+rule.Code,Content=panel,IsExpanded=true,Margin=new(0,4,0,12)});
    }
}
