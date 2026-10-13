using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private sealed record NearbyChoice(EnemyDefinition? Enemy){public string Name=>Enemy?.Name??"대상1과 같은 적";}
    private sealed record NearbyEditor(CombatEnemy Seed,StackPanel Row,TextBlock Heading,ComboBox Enemy,TextBox Level,CheckBox Eximus,ComboBox Difficulty,TextBox X,TextBox Y,TextBox Z,DebuffEditorState Debuffs);
    private readonly List<NearbyEditor> nearbyEditors=[];
    private readonly StackPanel nearbyRows=new(),sceneResults=new();
    private readonly TextBox targetOrderInput=new(){Text="1"};
    private readonly TextBox blockedSightInput=new(){Text="",ToolTip="벽 때문에 서로 보이지 않는 대상 쌍입니다. 예: 1-2, 나-1. 나는 플레이어, 숫자는 적 번호입니다. 빈칸이면 장애물이 없는 장면입니다."};
    private readonly StackPanel attackSequenceRows=new();
    private readonly List<ComboBox> attackSequenceChoices=[];
    private Expander? nearbyExpander;
    private bool updatingScene;
    private FrameworkElement CreateCombatScene()
    {
        var add=Button("＋ 주변 적",()=>{AddNearby(new(Guid.NewGuid().ToString("N"),3*(nearbyEditors.Count+1)));Recalculate();});
        var all=Button("순서대로 공격",()=>targetOrderInput.Text=string.Join(", ",Enumerable.Range(1,nearbyEditors.Count+1)));
        var hint=T("대상1은 위에서 선택한 적입니다. 위치는 대상1 기준이며, 플레이어는 입력한 거리만큼 앞뒤 축의 음수 방향에 있습니다. 순서는 공격 입력마다 반복합니다.",14,"#596B85");hint.TextWrapping=TextWrapping.Wrap;
        targetOrderInput.ToolTip="예: 1, 2, 3 → 각 공격 입력의 대상. 이동 시간이나 검의 궤적을 추정하지 않습니다.";
        targetOrderInput.TextChanged+=(_,_)=>{if(!updatingScene)Recalculate();};
        blockedSightInput.TextChanged+=(_,_)=>{if(!updatingScene)Recalculate();};
        nearbyExpander=new Expander{Header="주변 적 · 0명",Content=SpacedStack(8,hint,nearbyRows,add,Field("공격할 대상 순서",targetOrderInput),all,new Expander{Header="장애물 · 대상 사이 시야",Content=Field("시야가 막힌 대상 쌍",blockedSightInput)})};
        return nearbyExpander;
    }
    private FrameworkElement CreateAttackSequence()
    {
        var add=Button("＋ 공격 동작",()=>{attackMode.SelectedIndex=1;AddAttackSequence((meleeAttack.SelectedItem as AttackChoice)?.Id??"Normal Attack");Recalculate();});
        var reset=Button("기본 동작만",()=>{attackSequenceChoices.Clear();attackSequenceRows.Children.Clear();Recalculate();});
        var hint=T("선택한 동작을 차례대로 반복합니다. 기본 간격은 아래 직접 계산 조건을 사용합니다.",14,"#596B85");hint.TextWrapping=TextWrapping.Wrap;
        return new Expander{Header="공격 동작 순서 · 단일 공격",Content=Stack(hint,attackSequenceRows,Columns(add,reset))};
    }
    private void AddAttackSequence(string attack)
    {
        var row=new DockPanel{Margin=new(0,0,0,6)};var choice=new ComboBox{DisplayMemberPath="Name",SelectedValuePath="Id"};
        var w=(Weapon)weapon.SelectedItem;choice.ItemsSource=MeleeEngine.Attacks(w.Id).Select(a=>new AttackChoice(a,AttackName(a))).ToArray();choice.SelectedValue=attack;CompactInput(choice,34);
        var remove=Button("×",()=>{attackSequenceChoices.Remove(choice);attackSequenceRows.Children.Remove(row);Recalculate();});remove.ToolTip="공격 동작 삭제";DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);row.Children.Add(choice);
        choice.SelectionChanged+=(_,_)=>{if(!updatingScene)Recalculate();};attackSequenceChoices.Add(choice);attackSequenceRows.Children.Add(row);
    }
    private string[]? ReadAttackSequence()=>attackSequenceChoices.Count==0?null:attackSequenceChoices.Select(c=>c.SelectedValue as string??throw new ArgumentException("무기를 바꾼 뒤 공격 순서를 다시 선택하세요.")).ToArray();
    private void RefreshAttackSequence(string weaponId)
    {
        var previousUpdating=updatingScene;updatingScene=true;
        try
        {
            var available=MeleeEngine.Attacks(weaponId).Select(a=>new AttackChoice(a,AttackName(a))).ToArray();
            foreach(var choice in attackSequenceChoices)
            {
                var selected=choice.SelectedValue as string;choice.ItemsSource=available;
                choice.SelectedValue=available.Any(a=>a.Id==selected)?selected:available[0].Id;
            }
        }
        finally{updatingScene=previousUpdating;}
    }
    private void AddNearby(CombatEnemy seed)
    {
        var row=new StackPanel{Margin=new(0,8,0,16)};var heading=T("",14);var top=new DockPanel();
        var remove=Button("×",()=>{
            CombatSightBlock[] sight;try{sight=ReadBlockedSight();}catch(ArgumentException){Recalculate();return;}
            var removed=nearbyEditors.FindIndex(e=>e.Row==row)+1;nearbyEditors.RemoveAll(e=>e.Row==row);nearbyRows.Children.Remove(row);
            updatingScene=true;
            try{var order=targetOrderInput.Text.Split(',').Select(t=>int.TryParse(t.Trim(),out var n)?n:1).Where(n=>n!=removed+1).Select(n=>n>removed+1?n-1:n).ToArray();targetOrderInput.Text=order.Length==0?"1":string.Join(", ",order);blockedSightInput.Text=string.Join(", ",sight.Where(p=>p.First!=removed&&p.Second!=removed).Select(p=>$"{SightLabel(p.First-(p.First>removed?1:0))}-{SightLabel(p.Second-(p.Second>removed?1:0))}"));}
            finally{updatingScene=false;}RefreshNearbyLabels();Recalculate();
        });remove.ToolTip="이 주변 적 삭제";DockPanel.SetDock(remove,Dock.Right);top.Children.Add(remove);top.Children.Add(heading);row.Children.Add(top);
        var enemy=new ComboBox{DisplayMemberPath="Name",ItemsSource=new[]{new NearbyChoice(null)}.Concat(EnemyCatalog.Load().Enemies.Select(e=>new NearbyChoice(e))).ToArray()};
        enemy.SelectedItem=enemy.Items.Cast<NearbyChoice>().FirstOrDefault(c=>c.Enemy?.Id==seed.Selection?.EnemyId)??enemy.Items[0];CompactInput(enemy,34);row.Children.Add(enemy);
        var level=new TextBox{Text=(seed.Selection?.Level??200).ToString(CultureInfo.InvariantCulture)};
        var eximus=new CheckBox{Content="엑시무스",IsChecked=seed.Selection?.Eximus==true};
        var difficulty=new ComboBox{ItemsSource=new[]{"일반","강철의 길","빙주령"},SelectedIndex=(int)(seed.Selection?.Difficulty??EnemyDifficulty.Normal)};CompactInput(difficulty,34);
        var ownEnemy=Stack(Columns(Field("레벨",level),Field("난이도",difficulty)),eximus);row.Children.Add(ownEnemy);
        var x=new TextBox{Text=seed.X.ToString(CultureInfo.InvariantCulture)};var y=new TextBox{Text=seed.Y.ToString(CultureInfo.InvariantCulture)};var z=new TextBox{Text=seed.Z.ToString(CultureInfo.InvariantCulture)};
        var position=new UniformGrid{Columns=3};foreach(var (label,control) in new[]{("가로 (m)",x),("앞뒤 (m)",y),("높이 (m)",z)}){var f=(FrameworkElement)Field(label,control);f.Margin=new(0,0,6,0);position.Children.Add(f);}row.Children.Add(position);
        void SelectionChanged()
        {
            ownEnemy.Visibility=enemy.SelectedItem is NearbyChoice{Enemy:not null}?Visibility.Visible:Visibility.Collapsed;
            if(!updatingScene)
            {
                var index=nearbyEditors.FindIndex(e=>e.Row==row);
                if(index>=0){var editor=nearbyEditors[index];nearbyEditors[index]=editor with {Seed=editor.Seed with {Defense=null,Shields=null,Overguard=null,Faction=null}};}
                Recalculate();
            }
        }
        enemy.SelectionChanged+=(_,_)=>SelectionChanged();difficulty.SelectionChanged+=(_,_)=>{if(!updatingScene)Recalculate();};eximus.Checked+=(_,_)=>{if(!updatingScene)Recalculate();};eximus.Unchecked+=(_,_)=>{if(!updatingScene)Recalculate();};
        foreach(var field in new[]{level,x,y,z})field.TextChanged+=(_,_)=>{if(!updatingScene)Recalculate();};
        var debuffs=CreateDebuffEditor(seed.Debuffs);row.Children.Add(debuffs.View);
        nearbyEditors.Add(new(seed,row,heading,enemy,level,eximus,difficulty,x,y,z,debuffs));nearbyRows.Children.Add(row);ownEnemy.Visibility=seed.Selection is null?Visibility.Collapsed:Visibility.Visible;RefreshNearbyLabels();
    }
    private void RefreshNearbyLabels()
    {
        if(nearbyExpander is not null)nearbyExpander.Header=$"주변 적 · {nearbyEditors.Count}명";
        for(var i=0;i<nearbyEditors.Count;i++)nearbyEditors[i].Heading.Text=$"대상{i+2}";
    }
    private CombatEnemy[] ReadNearby()=>nearbyEditors.Select(e=>e.Seed with {Debuffs=ReadDebuffs(e.Debuffs),X=Number(e.X,"주변 적 가로 위치"),Y=Number(e.Y,"주변 적 앞뒤 위치"),Z=Number(e.Z,"주변 적 높이"),
        Selection=e.Enemy.SelectedItem is NearbyChoice{Enemy:{} def}?new EnemySelection(def.Id,Integer(e.Level,"주변 적 레벨"),(EnemyDifficulty)e.Difficulty.SelectedIndex,e.Seed.Selection?.SquadSize??6,Eximus:e.Eximus.IsChecked==true):null}).ToArray();
    private int[] ReadTargetOrder()=>targetOrderInput.Text.Split(',').Select(t=>int.TryParse(t.Trim(),out var n)&&n>=1&&n<=nearbyEditors.Count+1?n-1:throw new ArgumentException("공격 순서에는 존재하는 대상 번호를 쉼표로 구분해 입력하세요.")).ToArray();
    private static string SightLabel(int index)=>index<0?"나":(index+1).ToString();
    private static bool TrySightEndpoint(string text,out int value){if(text.Trim()=="나"){value=0;return true;}return int.TryParse(text,out value)&&value>=1;}
    private CombatSightBlock[] ReadBlockedSight()=>blockedSightInput.Text.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(s=>{
        var parts=s.Split('-');if(parts.Length!=2||!TrySightEndpoint(parts[0],out var a)||!TrySightEndpoint(parts[1],out var b)||a<0||b<0||a>nearbyEditors.Count+1||b>nearbyEditors.Count+1||a==b)throw new ArgumentException("시야 차단은 1-2 또는 나-1처럼 입력하세요. 나는 플레이어입니다.");return new CombatSightBlock(a-1,b-1);
    }).Distinct().ToArray();
    private void ApplyCombatScene(EncounterSetup? setup)
    {
        updatingScene=true;try{nearbyEditors.Clear();nearbyRows.Children.Clear();foreach(var e in setup?.NearbyEnemies??[])AddNearby(e);targetOrderInput.Text=string.Join(", ",(setup?.TargetOrder??[0]).Select(i=>i+1));blockedSightInput.Text=string.Join(", ",(setup?.BlockedSight??[]).Select(p=>$"{(p.First<0?"나":(p.First+1).ToString())}-{(p.Second<0?"나":(p.Second+1).ToString())}"));attackSequenceChoices.Clear();attackSequenceRows.Children.Clear();foreach(var a in setup?.AttackOrder??[])AddAttackSequence(a);RefreshNearbyLabels();}finally{updatingScene=false;}
    }
    private void ShowSceneResults(CombatTimelineResult? result)
    {
        sceneResults.Children.Clear();sceneResults.Visibility=result?.Enemies is {Length:>1}?Visibility.Visible:Visibility.Collapsed;
        if(result?.Enemies is not {Length:>1} enemies)return;
        sceneResults.Children.Add(T("대상별 피해 · 직접 + 상태",16));
        var maximum=enemies.Max(e=>e.DirectDamage+e.StatusDamage);
        for(var i=0;i<enemies.Length;i++)
        {
            var e=enemies[i];var amount=e.DirectDamage+e.StatusDamage;var line=new DockPanel();var value=T(amount.ToString("N1"),16);DockPanel.SetDock(value,Dock.Right);line.Children.Add(value);line.Children.Add(T($"대상{i+1}"+(i==0?" · 위쪽 수치의 기준":""),14));sceneResults.Children.Add(line);
            var bar=new Grid{Height=5,Margin=new(0,0,0,6)};bar.ColumnDefinitions.Add(new(){Width=new(maximum>0?amount/maximum:0,GridUnitType.Star)});bar.ColumnDefinitions.Add(new(){Width=new(maximum>0?Math.Max(0,1-amount/maximum):1,GridUnitType.Star)});
            var fill=new Border();fill.SetResourceReference(Border.BackgroundProperty,"ChartAccentBrush");bar.Children.Add(fill);sceneResults.Children.Add(bar);
            var label=T($"직접 {e.DirectDamage:N1} · 상태 {e.StatusDamage:N1}\n남은 체력 {e.Health:N0} · 실드 {e.Shields:N0} · 오버가드 {e.Overguard:N0}\n처치 {e.Ttk.KillFraction:P0}"+(e.Ttk.Median is {} ttk?$" · 중앙값 {ttk:0.###}초":"")+(e.DisarmFraction is {} disarm?$"\n무장 해제 효과 발동 {disarm:P0} · 전체 표본 중 비율":""),14,"#596B85");label.TextWrapping=TextWrapping.Wrap;label.Margin=new(0,0,0,16);sceneResults.Children.Add(label);
        }
    }
}
