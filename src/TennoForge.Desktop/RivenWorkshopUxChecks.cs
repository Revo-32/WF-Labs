using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;
public sealed partial class MainWindow
{
    internal async Task<object> CheckRivenWorkshopUx()
    {
        var checks=new List<string>();
        void Yes(bool b,string name){if(!b)throw new Exception(name);checks.Add(name);}
        void Click(string label)
        {
            rivenSections!.Select(label=="이 접합으로 예시 만들기"?1:3);
            rivenPage!.Measure(new Size(1720,820));rivenPage.Arrange(new Rect(0,0,1720,820));rivenPage.UpdateLayout();
            Descendants(rivenPage).OfType<Button>().Single(b=>Equals(b.Content,label)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        }
        var id=catalog.Weapons.Single(w=>w.EnglishName=="Nikana Prime").Id;
        var roll=new RivenRoll(id,8,[new(RivenStat.Damage,195.7),new(RivenStat.StatusChance,106.9)],RivenValues.Version,BaseDrain:6,Polarity:"AP_ATTACK");
        var fixture=new CombatTimelineResult(5,100,50,new(null,null,null,0,128,1,5),[],[],128,[],[]);
        var result=new EncounterResult(EncounterMode.FiveSeconds,5,new(150,150),new(30,30),new(0,0),[],128,[],[],fixture);
        var description=DescribeRivenResult(result);
        Yes(description.Contains("직접 피해 100.0")&&description.Contains("상태 피해 50.0")&&description.Contains("교전 직접 DPS 20.0")&&description.Contains("직접+상태 평균 DPS 30.0"),"직접 피해와 합계 피해의 라벨·분모를 분리");
        Yes(!DescribeRivenResult(result with{Timeline=fixture with{Seconds=0}}).Contains("∞"),"첫 접촉은 0초로 나눈 DPS를 표시하지 않음");
        UpdateRivenComparisonChart(result,result);
        Yes(rivenDpsCharts.Children.OfType<TextBlock>().Any(t=>t.Text=="기준   30.0"),"DPS 그래프: 직접+상태 피해를 교전 시간으로 나눔");
        UpdateRivenComparisonChart(result with{Timeline=fixture with{Seconds=0}},result with{Timeline=fixture with{Seconds=0}});
        Yes(rivenDpsCharts.Children.OfType<TextBlock>().Any(t=>t.Text=="첫 접촉 피해"),"첫 접촉 그래프는 DPS 대신 피해량 표시");
        var negativeShape=roll with{Traits=[..roll.Traits,new(RivenStat.StatusDuration,0,true)]};
        var negative=negativeShape.Traits.Last();var range=RivenValues.Range(negativeShape,negative);
        var rounded=Math.Round(range.Minimum,1);var grade=RivenEngine.Grade(rounded,range.Minimum,range.Maximum);
        rivenRangeCharts.Children.Clear();AddRivenRangeChart(negative,rounded,range.Minimum,range.Maximum,grade);
        Yes(Descendants(rivenRangeCharts).OfType<TextBlock>().Any(t=>t.Text.Contains("최소 표시값")),"음옵 그래프: 낮은 크기가 최소 불이익이며 반올림 최솟값 표시");
        ShowRivens();ApplyRivenWorkbench(roll);
        checks.AddRange(await CheckMeleeRivenMountActions(roll));
        checks.AddRange(await RangedRivenEditor.CheckApplyActionsAsync(host=>page.Content=host));
        checks.AddRange(await CheckRangedRivenImportActions());ShowRivens();ApplyRivenWorkbench(roll);
        var unchanged=ReadRivenWorkbench();rivenReference!.CheckFilters();
        Yes(ReadRivenWorkbench().Traits.SequenceEqual(unchanged.Traits),"도감 검색·구성 선택은 내 리벤을 수정하지 않음");
        var originalValue=rivenValues[0].Text;rivenValues[0].Text="";
        Yes(rivenRangeCharts.Children.Count==2,"수치 미입력 상태에서도 선택 옵션의 최소·최대 표시");
        SetRivenEndpoint(0,true);var endpoint=ReadRivenWorkbench();RivenValues.Validate(endpoint);
        Yes(RivenEngine.Grade(endpoint.Traits[0].Value,RivenValues.Range(endpoint,endpoint.Traits[0]).Minimum,RivenValues.Range(endpoint,endpoint.Traits[0]).Maximum).MaxCompatible,"최대값 버튼은 반올림 허용 범위 안의 값 입력");rivenValues[0].Text=originalValue;
        checks.AddRange(await CheckWeaponSearchUx());ShowRivens();ApplyRivenWorkbench(roll);
        Yes(rivenRangeCharts.Children.Count==2,"선택한 옵션별 범위 그래프 표시");
        Click("현재 리벤을 비교 기준으로 지정");Yes(rivenBaseline?.Traits.SequenceEqual(roll.Traits)==true,"실제 비교 기준 버튼");
        Click("순환 예시 생성");Yes(rivenSession is{Pending:not null,Cycles:1,KuvaSpent:900}&&!rivenWeapon.IsEnabled,"순환 버튼: 미채택 결과·비용 표시·편집 잠금");
        var old=rivenSession!.Current;Click("현재 옵션 유지");Yes(rivenSession.Current==old&&rivenSession.Pending is null&&rivenWeapon.IsEnabled,"유지 버튼: 기존 수치 보존·쿠바 소비·편집 복구");
        Click("순환 예시 생성");var next=rivenSession.Pending;Click("새 결과 채택");Yes(rivenSession.Current==next&&ReadCostedRiven().Traits.SequenceEqual(next!.Traits),"채택 버튼: 편집기 수치 동기화");
        SetWorkshop(new(roll));Click("이 접합으로 예시 만들기");Yes(rivenSession.Current.Traits.Any(t=>t.Spliced)&&rivenSession.SplicersSpent==1,"접합 버튼: 접합 예시·소비 표시");
        preserveSplice.IsChecked=false;Click("순환 예시 생성");Click("새 결과 채택");Yes(!rivenSession.Current.Traits.Any(t=>t.Spliced),"접합 보존 해제→순환→채택 플로우");
        var saved=ReadBuild();
        ApplyBuild(saved with{WeaponId=id,Slots=new ModSlot?[8],Riven=null,Stance=null,Arcanes=null,Melee=new(),Scenario=new(Enemy:new(10000,0)),Capacity=CapacityCatalog.Load().Default(id,null),Encounter=new(EncounterMode.FiveSeconds,Samples:128)});
        ShowRivens();SetWorkshop(new(roll));rivenBaseline=roll;await CompareRivenAsync();
        Yes(rivenComparison.Text.Contains("직접+상태 평균 DPS")&&rivenComparison.Text.Contains("TTK 중앙값")&&rivenComparison.Text.Contains("128회 표본")&&!rivenComparison.Text.Contains("계산 범위 확인"),"실제 5초 비교: 직접·상태 DPS·TTK·표본 수");
        var parts=rivenComparison.Text.Split("\n\n후보 · ");Yes(parts.Length==2&&parts[1].StartsWith(parts[0]["기준 · ".Length..]),"동일 리벤 비교는 동일 결과");
        Yes(rivenDpsCharts.Children.Count>0,"실제 비교 결과의 직접·상태 DPS 그래프");
        var compared=rivenComparison.Text;var chartCount=rivenDpsCharts.Children.Count;
        SetDarkTheme(false);SetDarkTheme(true);rivenSections!.Select(2);rivenSections.Select(3);
        Yes(rivenComparison.Text==compared&&rivenDpsCharts.Children.Count==chartCount,"테마·탭 전환은 비교 결과와 그래프를 보존");
        rivenValues[0].Text="입력 중";Yes(rivenDpsCharts.Children.Count==0,"입력 변경 시 오래된 비교 그래프 제거");rivenAttempts.Text="25";
        var recovery=System.Text.Json.JsonSerializer.Deserialize<UpdateWorkspace>(System.Text.Json.JsonSerializer.Serialize(CaptureUpdateWorkspace(),Catalog.Json),Catalog.Json)!;
        rivenValues[0].Text="1";rivenAttempts.Text="1";rivenBaseline=null;ApplyUpdateWorkspace(recovery);
        Yes(rivenValues[0].Text=="입력 중"&&rivenAttempts.Text=="25"&&rivenBaseline is not null,"업데이트 복원: 미완성 리벤 텍스트·예산 입력·비교 기준 보존");
        SetWorkshop(new(roll with{BaseDrain=null,Polarity=null}));Click("이 접합으로 예시 만들기");
        Yes(rivenSession!.Current.Traits.Any(t=>t.Spliced)&&rivenSession.Current.BaseDrain is null&&rivenSession.Current.Polarity is null,"코스트·극성 없이 접합 예시 생성, 장착 정보 임의 생성 없음");
        SetWorkshop(new(roll));rivenAttempts.Text="10";rivenComparison.Text=compared;rivenBaseline=roll;await CompareRivenAsync();
        rivenSections!.Select(3);return new{passed=true,checks};
    }
    internal void PendingRivenForCheck()=>WorkshopAction(s=>RivenWorkshop.CycleExample(s,new Random(7)));
}
