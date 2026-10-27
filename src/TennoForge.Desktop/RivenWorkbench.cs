using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private RivenRoll? equippedRiven;
    private RivenRoll? rivenValidOptions;
    private Button? rivenApplyButton,rivenLoadEquippedButton;
    private readonly WeaponSearchBox rivenWeapon=new();
    private readonly ComboBox rivenRank=new(){ItemsSource=Enumerable.Range(0,9),SelectedItem=8};
    private readonly ComboBox rivenDestination=new(){ItemsSource=Enumerable.Range(1,8),SelectedItem=1};
    private readonly TextBox rivenCost=new(){ToolTip="현재 랭크에서 게임에 표시된 코스트를 입력하세요."};
    private readonly ComboBox rivenPolarity=new(){ItemsSource=CapacityEngine.ModPolarities.Select(p=>new RivenPolarityChoice(p,ModBoard.PolarityLabel(p))).ToArray(),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedIndex=-1};
    private record RivenPolarityChoice(string Id,string Name);
    private readonly TextBox[] rivenValues=new TextBox[4];
    private readonly TextBlock[] rivenGrades=new TextBlock[4];
    private readonly TextBlock rivenInfo=T("",14);
    private bool changingRiven;
    private readonly FrameworkElement[] rivenEndpoints=new FrameworkElement[4];
    private RivenSections? rivenSections;
    private RivenReferenceBrowser? rivenReference;
    private record RivenOption(RivenStat? Stat,string Name);
    private FrameworkElement CreateRivenWorkbench()
    {
        var editor=new StackPanel();var output=new StackPanel();
        rivenWeapon.ItemsSource=catalog.Weapons;rivenWeapon.SelectedItem=weapon.SelectedItem;
        editor.Children.Add(T("내 리벤",22));editor.Children.Add(T("게임에 표시된 옵션 순서대로 입력",14,"#596B85"));editor.Children.Add(Field("리벤 랭크 · 수치 범위에 적용",rivenRank));
        var options=new[]{new RivenOption(null,"옵션 선택")}.Concat(Enum.GetValues<RivenStat>().Select(s=>new RivenOption(s,RivenEngine.Label(s)+(RivenValues.IsSpliced(s)?" · 접합":"")))).ToArray();
        for(var i=0;i<4;i++)
        {
            traits[i]=new(){ItemsSource=options,DisplayMemberPath="Name",SelectedIndex=0};
            negatives[i]=new(){Content="음옵",Margin=new(12,0,0,0),VerticalAlignment=VerticalAlignment.Center};
            rivenValues[i]=new(){ToolTip="게임에 표시된 수치의 크기 · 부호는 옆에서 선택"};
            rivenGrades[i]=T("",14,"#596B85");rivenGrades[i].TextWrapping=TextWrapping.Wrap;
            rivenGrades[i].Margin=new(0,0,0,4);
            var index=i;
            var endpoints=new WrapPanel{Margin=new(0,0,0,8)};var endpointDetails=new Expander{Header="최소·최대값으로 시험",Content=endpoints};rivenEndpoints[i]=endpointDetails;
            foreach(var maximum in new[]{false,true}){var end=maximum;var quick=Button(end?"최대값 입력":"최소값 입력",()=>SetRivenEndpoint(index,end));quick.Padding=new(8,3,8,3);quick.Margin=new(0,0,6,0);quick.ToolTip="선택한 구성의 경계값으로 비교할 가정을 입력합니다.";endpoints.Children.Add(quick);}
            editor.Children.Add(new RivenOptionInput(i,traits[i],rivenValues[i],RivenOptionInput.SignFor(negatives[i]),rivenGrades[i],endpointDetails));
            traits[i].SelectionChanged+=(_,_)=>RefreshRivenWorkbench();negatives[i].Checked+=(_,_)=>RefreshRivenWorkbench();negatives[i].Unchecked+=(_,_)=>RefreshRivenWorkbench();rivenValues[i].TextChanged+=(_,_)=>RefreshRivenWorkbench();
        }
        rivenWeapon.SelectionChanged+=(_,_)=>{if(!changingRiven){rivenSession=null;rivenBaseline=null;rivenCycles.Text="0";}RefreshRivenWorkbench();};rivenRank.SelectionChanged+=(_,e)=>{if(!changingRiven&&e.RemovedItems.Count>0&&e.RemovedItems[0] is int previous&&int.TryParse(rivenCost.Text,out var displayed)&&displayed-previous is >=0 and <=30)rivenCost.Text=(displayed-previous+(int)rivenRank.SelectedItem).ToString();RefreshRivenWorkbench();};
        var equipDetails=new Expander{Header="장착 설정 · 슬롯 / 코스트 / 극성",Content=Stack(Field("장착할 모드 슬롯",rivenDestination),Columns(Field("현재 랭크 표시 코스트",rivenCost),Field("리벤 극성",rivenPolarity))),Margin=new(0,4,0,12)};editor.Children.Add(equipDetails);
        var applyRiven=Button("현재 근접 모딩에 가정 적용",()=>
        {
            try
            {
                var roll=ReadRivenWorkbench();RivenValues.Validate(roll);
                var cost=Integer(rivenCost,"리벤 코스트")-roll.Rank;
                if(cost<0||cost>30||rivenPolarity.SelectedValue is not string polarity)throw new ArgumentException("표시 코스트와 극성을 확인하세요.");
                roll=roll with {WeaponId=((Weapon)weapon.SelectedItem).Id,BaseDrain=cost,Polarity=polarity};equippedRiven=roll;board.SetRiven(roll);

                var mod=catalog.Mods.Single(m=>m.Id==RivenValues.ModId(roll.WeaponId));
                var slots=board.Slots;for(var i=0;i<slots.Length;i++)if(slots[i]?.ModId==mod.Id)slots[i]=null;
                slots[(int)rivenDestination.SelectedItem-1]=new(mod.Id,roll.Rank);board.SetSlots(slots);RefreshRivenMountActions();ShowModding();
            }
            catch(ArgumentException ex){rivenInfo.Text=ex.Message;if(string.IsNullOrWhiteSpace(rivenCost.Text)||rivenPolarity.SelectedValue is null){equipDetails.IsExpanded=true;rivenCost.BringIntoView();rivenCost.Focus();}}
        });
        rivenApplyButton=applyRiven;ToolTipService.SetShowOnDisabled(applyRiven,true);
        var mountSummary=T("",13);mountSummary.TextWrapping=TextWrapping.Wrap;mountSummary.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");
        void UpdateMountSummary(){mountSummary.Text=$"슬롯 {rivenDestination.SelectedItem} · 코스트 {(string.IsNullOrWhiteSpace(rivenCost.Text)?"미입력":rivenCost.Text)} · {(rivenPolarity.SelectedItem as RivenPolarityChoice)?.Name??"극성 미선택"}";RefreshRivenMountActions();}
        rivenDestination.SelectionChanged+=(_,_)=>UpdateMountSummary();rivenCost.TextChanged+=(_,_)=>UpdateMountSummary();rivenPolarity.SelectionChanged+=(_,_)=>UpdateMountSummary();UpdateMountSummary();
        rivenLoadEquippedButton=Button("장착한 리벤 불러오기",()=>{if(HasMountedMeleeRiven()&&equippedRiven is {} r)ApplyRivenWorkbench(r);else rivenInfo.Text="현재 모딩에 장착한 리벤이 없습니다.";});
        ToolTipService.SetShowOnDisabled(rivenLoadEquippedButton,true);
        var footer=Stack(mountSummary,applyRiven,rivenLoadEquippedButton);
        footer.Margin=new(0,12,12,0);foreach(var action in footer.Children.OfType<Button>())action.Margin=new(0,8,0,0);UiHierarchy.Primary(applyRiven);
        var left=new Grid();left.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});left.RowDefinitions.Add(new(){Height=GridLength.Auto});left.Children.Add(Scroll(editor));Grid.SetRow(footer,1);left.Children.Add(footer);
        output.Children.Add(T("옵션 수치 분석",24));rivenInfo.TextWrapping=TextWrapping.Wrap;CollapseEmpty(rivenInfo);output.Children.Add(rivenRangeCharts);
        var note=T("표시 수치의 반올림을 고려한 범위입니다. 범위 내 위치는 획득 확률이 아닙니다. 음옵은 절대 크기를 비교하며 실제 유불리는 옵션마다 다릅니다. 접합에는 부호를 뒤집지 않은 원래 롤이 사용됩니다.\n\n접합은 높은 롤을 계승합니다. 대체 옵션은 무작위이므로 결과를 확정해 장착하지 않습니다. 실제 결과 또는 비교할 가정의 수치를 왼쪽에 입력하면 모딩에 적용할 수 있습니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        output.Children.Add(new Expander{Header="수치 범위 · 접합 계산 기준",Margin=new(0,0,0,20),Content=SpacedStack(12,note,Button("44.1.3 공식 패치",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(RivenBalance.Source){UseShellExecute=true})),Button("위키 계산 근거",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(RivenValues.Source){UseShellExecute=true})))});
        rivenComparison.Text="현재 리벤을 비교 기준으로 지정한 뒤 후보를 비교하세요.";
        var session=CreateRivenSessionPanel();
        rivenReference=new RivenReferenceBrowser(MeleeReferenceRanges,RivenEngine.Recipes.Select(r=>(RivenEngine.Label(r.A)+" + "+RivenEngine.Label(r.B),RivenEngine.Label(r.Result))));
        var splicePage=Stack(T("접합 미리보기",24),T("소비되는 옵션 두 개 → 계승되는 접합 옵션",16),T("왼쪽에 실제 수치를 입력하면 가능한 조합과 계승 범위가 표시됩니다. 전체 조합표는 옵션 도감에서 확인할 수 있습니다.",14,"#596B85"),spliceResults);
        rivenSections=new RivenSections(("수치 확인",Scroll(output)),("접합",Scroll(splicePage)),("옵션 도감",rivenReference),("비교 · 순환",Scroll(session)));
        var workspace=new Grid();workspace.RowDefinitions.Add(new(){Height=GridLength.Auto});workspace.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});workspace.Children.Add(rivenInfo);Grid.SetRow(rivenSections,1);workspace.Children.Add(rivenSections);
        rivenSections.Select(0);RefreshRivenWorkbench();var workbench=Columns(left,workspace,1,2.35);
        workbench.IsVisibleChanged+=(_,e)=>{if(e.NewValue is true)RefreshRivenMountActions();};return workbench;
    }
    private RivenRoll ReadRivenWorkbench(bool requireValues=true)
    {
        var selected=(Weapon?)rivenWeapon.SelectedItem??throw new ArgumentException("리벤 대상 무기를 선택하세요.");
        var values=new List<RivenTrait>();
        for(var i=0;i<4;i++)if((traits[i].SelectedItem as RivenOption)?.Stat is {} stat)
            values.Add(new(stat,requireValues?Number(rivenValues[i],RivenEngine.Label(stat)):0,negatives[i].IsChecked==true,RivenValues.IsSpliced(stat)));
        return new(selected.Id,(int)rivenRank.SelectedItem,values.ToArray(),RivenValues.Version);
    }
    private void RefreshRivenWorkbench(bool invalidateComparison=true)
    {
        if(changingRiven||rivenValues.Any(x=>x is null))return;
        rivenValidOptions=null;
        if(invalidateComparison)InvalidateRivenComparison();
        rivenReference?.Refresh();
        RefreshRivenLocks();
        for(int i=0;i<4;i++){var selected=(traits[i].SelectedItem as RivenOption)?.Stat is not null;rivenEndpoints[i].Visibility=selected?Visibility.Visible:Visibility.Collapsed;rivenEndpoints[i].IsEnabled=rivenSession?.Pending is null;rivenGrades[i].Visibility=selected?Visibility.Visible:Visibility.Collapsed;}
        rivenRangeCharts.Children.Clear();spliceResults.Children.Clear();foreach(var grade in rivenGrades)grade.Text="";
        try
        {
            var shape=ReadRivenWorkbench(false);RivenValues.Validate(shape,false);
            rivenReference?.FollowComposition(shape.Traits.Count(t=>!t.Negative),shape.Traits.Count(t=>t.Negative));
            rivenInfo.Text=$"공통 배율 1.2 · 랭크 {shape.Rank}/8 · Hotfix 44.1.3";
            var n=0;
            for(var i=0;i<4;i++)if((traits[i].SelectedItem as RivenOption)?.Stat is {} stat)
            {
                var trait=shape.Traits[n++];var range=RivenValues.Range(shape,trait);var unit=RivenValues.Unit(stat);
                rivenGrades[i].Text=$"가능 범위 {range.Minimum:0.###}–{range.Maximum:0.###}{unit}";
                var hasValue=double.TryParse(rivenValues[i].Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var v);
                rivenRangeCharts.Children.Add(RivenPresentation.Range(RivenEngine.Label(stat),unit,range.Minimum,range.Maximum,hasValue?v:null,trait.Negative));
                if(hasValue)try{var grade=RivenEngine.Grade(v,range.Minimum,range.Maximum);rivenGrades[i].Text+=$" · 범위 위치 {grade.Position:P1}";}catch(ArgumentException){rivenGrades[i].Text+=" · 범위 밖의 값";}

            }
            var roll=ReadRivenWorkbench();RivenValues.Validate(roll);rivenValidOptions=roll;
            foreach(var prediction in RivenValues.Predict(roll))
            {
                var candidate=prediction.Candidate;
                spliceResults.Children.Add(RivenPresentation.Recipe(RivenEngine.Label(roll.Traits[candidate.First].Stat)+" + "+RivenEngine.Label(roll.Traits[candidate.Second].Stat),RivenEngine.Label(candidate.Result),$"{prediction.Value.Low:0.###}–{prediction.Value.High:0.###}{RivenValues.Unit(candidate.Result)}",candidate.RandomIsNegative));
                var action=Button("이 접합으로 예시 만들기",()=>WorkshopAction(s=>RivenWorkshop.SpliceExample(s,candidate,Random.Shared)));
                action.Margin=new(0,0,0,20);spliceResults.Children.Add(action);
            }
            if(spliceResults.Children.Count==0)spliceResults.Children.Add(T(roll.Traits.Any(t=>t.Spliced)?"접합 옵션이 이미 있습니다. 재접합 전 기존 접합을 해제해야 합니다.":"현재 옵션으로 가능한 접합 없음",14));
        }
        catch(ArgumentException ex)
        {
            rivenInfo.Text=traits.All(t=>(t.SelectedItem as RivenOption)?.Stat is null)?"":ex.Message;
            if(spliceResults.Children.Count==0)spliceResults.Children.Add(T("옵션 2~3개와 게임 표시값을 입력하면 접합을 확인할 수 있습니다.",16));
            if(rivenRangeCharts.Children.Count==0)rivenRangeCharts.Children.Add(UiHierarchy.RivenStart(()=>{traits[0].BringIntoView();traits[0].Focus();traits[0].IsDropDownOpen=true;},()=>rivenSections?.Select(2)));
        }
        RefreshRivenMountActions();
    }
    private bool HasMountedMeleeRiven()=>equippedRiven is {} roll&&board.Slots.Any(s=>s?.ModId==RivenValues.ModId(roll.WeaponId));
    private void RefreshRivenMountActions()
    {
        if(rivenApplyButton is not null)
        {
            bool available=false;string hint;
            try
            {
                if(rivenSession?.Pending is not null)throw new ArgumentException("현재 옵션 유지 또는 새 결과 채택을 먼저 선택하세요.");
                var roll=rivenValidOptions??throw new ArgumentException("옵션과 게임에 표시된 수치를 입력하세요.");
                int cost=Integer(rivenCost,"리벤 코스트")-roll.Rank;
                if(cost<0||cost>30||rivenPolarity.SelectedValue is not string)throw new ArgumentException("장착 설정에서 현재 랭크의 표시 코스트와 극성을 입력하세요.");
                if(rivenDestination.SelectedItem is not int slot||slot is <1 or >8)throw new ArgumentException("장착할 모드 슬롯을 선택하세요.");
                available=true;hint="입력한 리벤을 선택한 슬롯에 가정 적용합니다.";
            }
            catch(ArgumentException ex){hint=ex.Message;}
            rivenApplyButton.IsEnabled=available;rivenApplyButton.ToolTip=hint;
        }
        if(rivenLoadEquippedButton is not null)
        {
            rivenLoadEquippedButton.IsEnabled=rivenSession?.Pending is null&&HasMountedMeleeRiven();
            rivenLoadEquippedButton.ToolTip=rivenSession?.Pending is not null?"현재 옵션 유지 또는 새 결과 채택을 먼저 선택하세요.":
                rivenLoadEquippedButton.IsEnabled?"현재 근접 모딩 슬롯에 장착된 리벤을 가져옵니다.":"현재 근접 모딩 슬롯에 장착된 리벤이 없습니다.";
        }
    }
    private void SetRivenEndpoint(int index,bool maximum)
    {
        if(!rivenValues[index].IsEnabled)return;
        try{var shape=ReadRivenWorkbench(false);RivenValues.Validate(shape,false);if((traits[index].SelectedItem as RivenOption)?.Stat is not {} stat)return;var t=shape.Traits.Single(t=>t.Stat==stat);var range=RivenValues.Range(shape,t);rivenValues[index].Text=Math.Round(maximum?range.Maximum:range.Minimum,1).ToString(CultureInfo.InvariantCulture);}catch(ArgumentException ex){rivenInfo.Text=ex.Message;}
    }
    private IEnumerable<RivenReferenceEntry> MeleeReferenceRanges(int positive,int negative,bool isNegative,bool spliced)
    {
        var selected=(Weapon?)rivenWeapon.SelectedItem??throw new ArgumentException("무기를 선택하세요.");
        // Only composition counts are consumed by Range. The synthetic shape never enters the user's build.
        var shape=new RivenRoll(selected.Id,(int)rivenRank.SelectedItem,Enumerable.Range(0,positive).Select(_=>new RivenTrait(RivenStat.Range,0)).Concat(Enumerable.Range(0,negative).Select(_=>new RivenTrait(RivenStat.StatusDuration,0,true))).ToArray(),RivenValues.Version);
        foreach(var stat in RivenValues.Bases.Keys.Where(s=>RivenValues.IsSpliced(s)==spliced))
        {
            if(isNegative&&(spliced||stat is RivenStat.Damage or RivenStat.Heat or RivenStat.Cold or RivenStat.Electricity or RivenStat.Toxin))continue;
            var range=RivenValues.Range(shape,new(stat,0,isNegative,spliced));yield return new(RivenEngine.Label(stat),stat.ToString(),RivenValues.Unit(stat),range.Minimum,range.Maximum);
        }
    }
    internal void SelectRivenSectionForCheck(int index)=>rivenSections!.Select(index);
    private void ApplyRivenWorkbench(RivenRoll roll)
    {
        changingRiven=true;
        try
        {
            rivenWeapon.SelectedItem=catalog.Weapons.Single(w=>w.Id==roll.WeaponId);rivenRank.SelectedItem=roll.Rank;
            rivenCost.Text=roll.BaseDrain is {} drain?(drain+roll.Rank).ToString():"";rivenPolarity.SelectedValue=roll.Polarity;
            for(var i=0;i<4;i++)
            {var t=roll.Traits.ElementAtOrDefault(i);traits[i].SelectedItem=traits[i].Items.Cast<RivenOption>().Single(o=>o.Stat==t?.Stat);negatives[i].IsChecked=t?.Negative==true;rivenValues[i].Text=t?.Value.ToString(CultureInfo.InvariantCulture)??"";}
        }
        finally{changingRiven=false;}
        RefreshRivenWorkbench();
    }
}
