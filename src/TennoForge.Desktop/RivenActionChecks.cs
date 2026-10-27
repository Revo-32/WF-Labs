using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private async Task<string[]> CheckMeleeRivenMountActions(RivenRoll roll)
    {
        var checks=new List<string>();void Yes(bool value,string name){if(!value)throw new InvalidOperationException(name);checks.Add(name);}
        static void Invoke(Button button)=>((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        var saved=ReadBuild();
        try
        {
            ApplyBuild(saved with{WeaponId=roll.WeaponId,Riven=null,Slots=new ModSlot?[8]});ShowRivens();
            var applyButton=rivenApplyButton??throw new InvalidOperationException("근접 리벤 적용 명령 없음");
            var loadButton=rivenLoadEquippedButton??throw new InvalidOperationException("근접 리벤 불러오기 명령 없음");
            ApplyRivenWorkbench(roll with{Traits=[]});
            Yes(!applyButton.IsEnabled&&!loadButton.IsEnabled,"빈 근접 리벤·미장착 상태에서 적용/불러오기 비활성");
            bool blocked=false;try{Invoke(applyButton);}catch(ElementNotEnabledException){blocked=true;}
            Yes(blocked&&equippedRiven is null,"비활성 근접 적용의 실제 Automation Invoke가 빌드를 바꾸지 않음");
            ApplyRivenWorkbench(roll);Yes(applyButton.IsEnabled,"유효한 근접 옵션·수치·장착 정보 입력 시 적용 활성");
            var value=rivenValues[0].Text;rivenValues[0].Text="";
            Yes(!applyButton.IsEnabled,"근접 수치 미입력 즉시 적용 비활성");rivenValues[0].Text=value;
            var cost=rivenCost.Text;rivenCost.Text="";Yes(!applyButton.IsEnabled,"근접 표시 코스트 미입력 즉시 적용 비활성");rivenCost.Text=cost;
            var polarity=rivenPolarity.SelectedIndex;rivenPolarity.SelectedIndex=-1;Yes(!applyButton.IsEnabled,"근접 극성 미선택 즉시 적용 비활성");rivenPolarity.SelectedIndex=polarity;
            Yes(applyButton.IsEnabled,"근접 입력 복구 시 적용 다시 활성");
            Invoke(applyButton);await System.Windows.Threading.Dispatcher.Yield();
            Yes(equippedRiven is {} applied&&applied.Traits.SequenceEqual(roll.Traits)&&board.Slots.Any(s=>s?.ModId==RivenValues.ModId(roll.WeaponId)),"근접 실제 적용 버튼에서 옵션 보존·모딩 슬롯 장착");
            ShowRivens();rivenValues[0].Text="";RefreshRivenMountActions();
            Yes(loadButton.IsEnabled&&!applyButton.IsEnabled,"근접 편집 값이 미완성이어도 실제 장착 리벤은 불러오기 가능");
            Invoke(loadButton);await System.Windows.Threading.Dispatcher.Yield();
            Yes(applyButton.IsEnabled&&ReadRivenWorkbench().Traits.SequenceEqual(roll.Traits),"근접 실제 불러오기 버튼에서 장착 수치 복구");
            board.SetSlots(new ModSlot?[8]);RefreshRivenMountActions();
            Yes(equippedRiven is not null&&!loadButton.IsEnabled,"근접 슬롯 해제 뒤 남은 메타데이터만으로 불러오기 활성화하지 않음");
        }
        finally{ApplyBuild(saved);ShowRivens();ApplyRivenWorkbench(roll);}
        return checks.ToArray();
    }
    private async Task<string[]> CheckRangedRivenImportActions()
    {
        var checks=new List<string>();
        foreach(var name in new[]{"Braton","Strun","Lato"})
        {
            var weapon=RangedEngine.Weapons.Single(w=>w.EnglishName==name);
            var roll=RangedRivenEditor.ValidActionFixture(weapon);
            var build=new RangedBuild(weapon.Id,0,new ModSlot?[8],Enumerable.Repeat("None",8).ToArray(),Riven:roll);
            var view=new RangedRivenWorkbench(weapon.Category,(_,_)=>{},()=>roll,()=>build);page.Content=view;view.RefreshContext();
            var button=LayoutDiagnostics.Tree(view).OfType<Button>().Single(b=>Equals(b.Content,"장착한 리벤 가져오기"));
            if(button.IsEnabled)throw new InvalidOperationException(name+" 미장착 메타데이터로 불러오기 활성화");checks.Add(name+" 미장착 리벤 가져오기 비활성");
            build=build with{Slots=[new(RangedRivens.Mod(weapon).Id,roll.Rank),null,null,null,null,null,null,null]};view.RefreshContext();
            if(!button.IsEnabled)throw new InvalidOperationException(name+" 실제 장착 상태의 불러오기 비활성");
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();await System.Windows.Threading.Dispatcher.Yield();
            if(view.ValidRollForCheck is not {} imported||!imported.Traits.SequenceEqual(roll.Traits))throw new InvalidOperationException(name+" 실제 불러오기에서 옵션 불일치");checks.Add(name+" 실제 리벤 가져오기 Automation Invoke·옵션 보존");
            build=build with{Slots=new ModSlot?[8]};view.RefreshContext();
            if(button.IsEnabled)throw new InvalidOperationException(name+" 해제 뒤 불러오기 상태 미갱신");checks.Add(name+" 슬롯 해제 후 불러오기 비활성 복구");
        }
        return checks.ToArray();
    }
}

internal sealed partial class RangedRivenEditor
{
    internal static RangedRivenRoll ValidActionFixture(ReferenceWeapon weapon)
    {
        var shape=new RangedRivenRoll(weapon.Id,8,[new("Damage",0),new("StatusChance",0)],RivenBalance.Version,10,"Madurai");
        return shape with{Traits=shape.Traits.Select(t=>{var range=RangedRivens.Range(shape,t);return t with{Value=Math.Round((range.Minimum+range.Maximum)/2,1)};}).ToArray()};
    }
    internal static async Task<string[]> CheckApplyActionsAsync(Action<FrameworkElement> host)
    {
        var checks=new List<string>();
        static void Invoke(Button button)=>((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        foreach(var name in new[]{"Braton","Strun","Lato"})
        {
            var weapon=RangedEngine.Weapons.Single(w=>w.EnglishName==name);var roll=ValidActionFixture(weapon);RangedRivenRoll? accepted=null;
            var view=new RangedRivenEditor(weapon,null,r=>accepted=r,()=>{},inline:true);host(view);
            if(view.apply.IsEnabled)throw new InvalidOperationException(name+" 빈 리벤 적용 가능");
            bool blocked=false;try{Invoke(view.apply);}catch(ElementNotEnabledException){blocked=true;}
            if(!blocked||accepted is not null)throw new InvalidOperationException(name+" 비활성 적용 Invoke 차단 실패");checks.Add(name+" 빈 옵션의 실제 적용 명령 차단");
            view.SetWorkshop(new(roll));if(!view.apply.IsEnabled)throw new InvalidOperationException(name+" 유효한 리벤 적용 비활성");
            string value=view.rows[0].Value.Text;view.rows[0].Value.Text="";if(view.apply.IsEnabled)throw new InvalidOperationException(name+" 수치 미입력 적용 가능");view.rows[0].Value.Text=value;
            string cost=view.cost.Text;view.cost.Text="";if(view.apply.IsEnabled)throw new InvalidOperationException(name+" 코스트 미입력 적용 가능");view.cost.Text=cost;
            int polarity=view.polarity.SelectedIndex;view.polarity.SelectedIndex=-1;if(view.apply.IsEnabled)throw new InvalidOperationException(name+" 극성 미선택 적용 가능");view.polarity.SelectedIndex=polarity;
            if(!view.apply.IsEnabled)throw new InvalidOperationException(name+" 입력 복구 후 적용 비활성");checks.Add(name+" 수치·코스트·극성에 따른 즉시 비활성/복구");
            Invoke(view.apply);await System.Windows.Threading.Dispatcher.Yield();
            if(accepted is null||!accepted.Traits.SequenceEqual(roll.Traits)||accepted.BaseDrain!=roll.BaseDrain||accepted.Polarity!=roll.Polarity)throw new InvalidOperationException(name+" 실제 적용 옵션·장착 정보 불일치");checks.Add(name+" 실제 적용 Automation Invoke·옵션/장착 정보 보존");
        }
        return checks.ToArray();
    }
}
