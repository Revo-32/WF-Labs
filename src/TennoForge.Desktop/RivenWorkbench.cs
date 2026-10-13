using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private RivenRoll? equippedRiven;
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
    private record RivenOption(RivenStat? Stat,string Name);
    private FrameworkElement CreateRivenWorkbench()
    {
        var editor=new StackPanel();var output=new StackPanel();
        rivenWeapon.ItemsSource=catalog.Weapons;rivenWeapon.SelectedItem=weapon.SelectedItem;
        editor.Children.Add(T("리벤",22));editor.Children.Add(Field("무기 · 변형별 기질",rivenWeapon));
        editor.Children.Add(Columns(Field("리벤 랭크",rivenRank),Field("장착할 모드 슬롯",rivenDestination)));
        editor.Children.Add(Columns(Field("표시 코스트",rivenCost),Field("리벤 극성",rivenPolarity)));
        var options=new[]{new RivenOption(null,"선택 안 함")}.Concat(Enum.GetValues<RivenStat>().Select(s=>new RivenOption(s,RivenEngine.Label(s)+(RivenValues.IsSpliced(s)?" · 접합":"")))).ToArray();
        for(var i=0;i<4;i++)
        {
            traits[i]=new(){ItemsSource=options,DisplayMemberPath="Name",SelectedIndex=0};
            negatives[i]=new(){Content="음옵",Margin=new(12,0,0,0),VerticalAlignment=VerticalAlignment.Center};
            rivenValues[i]=new(){ToolTip="게임에 표시된 수치의 크기. 음수 부호는 음옵 스위치로 선택합니다."};
            rivenGrades[i]=T("",14,"#596B85");rivenGrades[i].TextWrapping=TextWrapping.Wrap;
            var valueRow=new DockPanel();DockPanel.SetDock(negatives[i],Dock.Right);valueRow.Children.Add(negatives[i]);valueRow.Children.Add(rivenValues[i]);
            var traitRow=Columns(traits[i],valueRow,1.3,1);traitRow.Margin=new(0,4,0,6);
            rivenGrades[i].Margin=new(0,0,0,12);
            editor.Children.Add(traitRow);editor.Children.Add(rivenGrades[i]);
            traits[i].SelectionChanged+=(_,_)=>RefreshRivenWorkbench();negatives[i].Checked+=(_,_)=>RefreshRivenWorkbench();negatives[i].Unchecked+=(_,_)=>RefreshRivenWorkbench();rivenValues[i].TextChanged+=(_,_)=>RefreshRivenWorkbench();
        }
        rivenWeapon.SelectionChanged+=(_,_)=>{if(!changingRiven){rivenSession=null;rivenBaseline=null;rivenCycles.Text="0";}RefreshRivenWorkbench();};rivenRank.SelectionChanged+=(_,_)=>RefreshRivenWorkbench();
        editor.Children.Add(Button("선택한 슬롯에 장착",()=>
        {
            try
            {
                var roll=ReadRivenWorkbench();RivenValues.Validate(roll);
                var cost=Integer(rivenCost,"리벤 코스트")-roll.Rank;
                if(cost<0||cost>30||rivenPolarity.SelectedValue is not string polarity)throw new ArgumentException("표시 코스트와 극성을 확인하세요.");
                roll=roll with {BaseDrain=cost,Polarity=polarity};equippedRiven=roll;board.SetRiven(roll);
                weaponClass.SelectedIndex=0;weapon.SelectedItem=catalog.Weapons.Single(w=>w.Id==roll.WeaponId);
                var mod=catalog.Mods.Single(m=>m.Id==RivenValues.ModId(roll.WeaponId));
                var slots=board.Slots;for(var i=0;i<slots.Length;i++)if(slots[i]?.ModId==mod.Id)slots[i]=null;
                slots[(int)rivenDestination.SelectedItem-1]=new(mod.Id,roll.Rank);board.SetSlots(slots);ShowModding();
            }
            catch(ArgumentException ex){rivenInfo.Text=ex.Message;}
        }));
        editor.Children.Add(Button("장착한 리벤 불러오기",()=>{if(equippedRiven is {} r)ApplyRivenWorkbench(r);else rivenInfo.Text="아직 장착한 리벤이 없습니다.";}));
        foreach(var action in editor.Children.OfType<Button>())action.Margin=new(0,8,0,0);
        output.Children.Add(T("수치 범위 · 접합 예상",22));rivenInfo.TextWrapping=TextWrapping.Wrap;output.Children.Add(rivenInfo);output.Children.Add(T("옵션별 수치 위치",18));output.Children.Add(T("범위 내 위치이며 획득 확률·빌드 점수가 아닙니다. 세로 표시와 옅은 띠는 현재 값과 반올림 오차 범위입니다.",14,"#596B85"));output.Children.Add(rivenRangeCharts);output.Children.Add(T("접합 예상",18));output.Children.Add(spliceResults);
        var note=T("표시 수치의 반올림을 고려한 범위입니다. 범위 내 위치는 획득 확률이 아닙니다. 음옵은 크기가 낮을수록 불이익이 작지만, 접합에는 부호를 뒤집지 않은 원래 롤이 사용됩니다.\n\n접합은 높은 롤을 계승합니다. 대체 옵션은 무작위이므로 결과를 확정해 장착하지 않습니다. 실제 결과 또는 비교할 가정의 수치를 왼쪽에 입력하면 모딩에 적용할 수 있습니다.",14,"#596B85");note.TextWrapping=TextWrapping.Wrap;
        output.Children.Add(new Expander{Header="수치 범위 · 접합 계산 기준",Margin=new(0,0,0,20),Content=SpacedStack(12,note,Button("위키 계산 근거",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(RivenValues.Source){UseShellExecute=true})))});
        rivenComparison.Text="현재 리벤을 비교 기준으로 지정한 뒤 후보를 비교하세요.";
        var session=CreateRivenSessionPanel();RefreshRivenWorkbench();
        return Columns(Scroll(editor),Columns(Scroll(output),Scroll(session)),1,2);
    }
    private RivenRoll ReadRivenWorkbench(bool requireValues=true)
    {
        var selected=(Weapon?)rivenWeapon.SelectedItem??throw new ArgumentException("리벤 대상 무기를 선택하세요.");
        var values=new List<RivenTrait>();
        for(var i=0;i<4;i++)if((traits[i].SelectedItem as RivenOption)?.Stat is {} stat)
            values.Add(new(stat,requireValues?Number(rivenValues[i],RivenEngine.Label(stat)):0,negatives[i].IsChecked==true,RivenValues.IsSpliced(stat)));
        return new(selected.Id,(int)rivenRank.SelectedItem,values.ToArray(),RivenValues.Version);
    }
    private void RefreshRivenWorkbench()
    {
        if(changingRiven||rivenValues.Any(x=>x is null))return;
        InvalidateRivenComparison();
        RefreshRivenLocks();
        rivenRangeCharts.Children.Clear();spliceResults.Children.Clear();foreach(var grade in rivenGrades)grade.Text="";
        try
        {
            var shape=ReadRivenWorkbench(false);RivenValues.Validate(shape,false);
            rivenInfo.Text=$"기질 {RivenValues.Disposition(shape.WeaponId):0.00} · 랭크 {shape.Rank}/8 · 위키 개정 2817521";
            var n=0;
            for(var i=0;i<4;i++)if((traits[i].SelectedItem as RivenOption)?.Stat is {} stat)
            {
                var trait=shape.Traits[n++];var range=RivenValues.Range(shape,trait);var unit=RivenValues.Unit(stat);
                rivenGrades[i].Text=$"가능 범위 {range.Minimum:0.###}–{range.Maximum:0.###}{unit}";
                if(double.TryParse(rivenValues[i].Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var v))
                {var grade=RivenEngine.Grade(v,range.Minimum,range.Maximum);AddRivenRangeChart(trait,v,range.Minimum,range.Maximum,grade);rivenGrades[i].Text+=$" · 범위 위치 {grade.Position:P1}"+(trait.Negative?(grade.MinCompatible?" · 최소 불이익 표시값 가능":" · 낮을수록 불이익 감소"):(grade.MaxCompatible?" · 최대 표시값 가능":""));}
            }
            var roll=ReadRivenWorkbench();RivenValues.Validate(roll);
            foreach(var prediction in RivenValues.Predict(roll))
            {
                var candidate=prediction.Candidate;
                var text=T($"{RivenEngine.Label(candidate.Result)}\n{prediction.Value.Low:0.###}–{prediction.Value.High:0.###}{RivenValues.Unit(candidate.Result)}\n{RivenEngine.Label(roll.Traits[candidate.First].Stat)} + {RivenEngine.Label(roll.Traits[candidate.Second].Stat)} 소비\n대체 {(candidate.RandomIsNegative?"음옵":"양옵")} 1개 · 종류와 수치 무작위",16);text.TextWrapping=TextWrapping.Wrap;spliceResults.Children.Add(Card(text));
                var action=Button("이 접합으로 예시 만들기",()=>WorkshopAction(s=>RivenWorkshop.SpliceExample(s,candidate,Random.Shared)));
                action.Margin=new(0,0,0,20);spliceResults.Children.Add(action);
            }
            if(spliceResults.Children.Count==0)spliceResults.Children.Add(T(roll.Traits.Any(t=>t.Spliced)?"접합 옵션이 이미 있습니다. 재접합 전 기존 접합을 해제해야 합니다.":"현재 옵션으로 가능한 접합 없음",14));
        }
        catch(ArgumentException ex){rivenInfo.Text=ex.Message;}
    }
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
