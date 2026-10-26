using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal object CheckEquationInteractions()
    {
        var equationWasOpen=equationDetails.IsExpanded;equationDetails.IsExpanded=true;
        var saved=ReadBuild();var cases=new List<object>();
        try
        {
            Mod M(string name)=>catalog.Mods.Single(m=>m.EnglishName==name);
            var names=new[]{"Pressure Point","True Steel","Fury","Fever Strike","Melee Elementalist"};
            var slots=new ModSlot?[8];for(var i=0;i<names.Length;i++){var m=M(names[i]);slots[i]=new(m.Id,m.MaxRank);}
            var baseline=saved with {Encounter=new(EncounterMode.FirstHit),EnemySelection=null,Stance=null,Slots=slots,Arcanes=null,Melee=new(),Scenario=new(Enemy:new(10000,2700))};ApplyBuild(baseline);UpdateLayout();
            var initialSize=equationView.RenderSize;
            var initialPosition=equationView.TranslatePoint(new Point(),root);
            var font=new Typeface(DpsEquationView.MathFont,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);
            if(!font.TryGetGlyphTypeface(out var glyph)||!new[]{'∑','×','÷','0','1','9'}.All(c=>glyph.CharacterToGlyphMap.ContainsKey(c)))throw new Exception("수학 글꼴의 실제 글리프 확인 실패");
            foreach(var item in new[]{(0,"damage"),(1,"critical"),(2,"time"),(3,"types")})
            {
                board.HoverSlotForCheck(item.Item1,true);SettleEquationAnimation(()=>equationView.Highlighted.SequenceEqual(new[]{item.Item2})&&equationView.FontSizeFor(item.Item2)>=23.99);
                if(!equationView.Highlighted.SequenceEqual(new[]{item.Item2})||equationView.FontSizeFor(item.Item2)<23.9)throw new Exception("모드 호버 계산식 강조 실패: "+names[item.Item1]);
                UpdateLayout();if(equationView.RenderSize!=initialSize||equationView.TranslatePoint(new Point(),root)!=initialPosition)throw new Exception("수식 강조로 레이아웃이 움직임");
                board.HoverSlotForCheck(item.Item1,false);SettleEquationAnimation(()=>equationView.Highlighted.Length==0&&Math.Abs(equationView.FontSizeFor(item.Item2)-20)<=.01);
                if(equationView.Highlighted.Length!=0||Math.Abs(equationView.FontSizeFor(item.Item2)-20)>.01)throw new Exception($"수식 강조 복귀 실패: {item.Item2}, 크기 {equationView.FontSizeFor(item.Item2):F4}, 강조 {string.Join(',',equationView.Highlighted)}");
                cases.Add(new{source=names[item.Item1],stage=item.Item2,font=equationView.FontNameFor(item.Item2),layoutStable=true});
            }
            board.HoverSlotForCheck(4,true);if(equationView.Highlighted.Length!=0)throw new Exception("상태 피해 모드가 직접 DPS를 강조함");board.HoverSlotForCheck(4,false);
            var fury=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Fury");
            ApplyBuild(baseline with {Arcanes=new(Warframe1:new(fury.Id,5))});
            var arcaneRow=(FrameworkElement)arcaneEditors[1].Choice.Parent;
            arcaneRow.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseEnterEvent});SettleEquationAnimation();
            if(!equationView.Highlighted.Contains("damage"))throw new Exception("아케인 실제 행 호버 실패");
            arcaneRow.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseLeaveEvent});
            ApplyBuild(saved);stance.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseEnterEvent});SettleEquationAnimation();
            if(!equationView.Highlighted.Contains("motion")||!equationView.Highlighted.Contains("time"))throw new Exception("스탠스 호버 단계 연결 실패");
            stance.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseLeaveEvent});
            var beforeScroll=equationView.TranslatePoint(new Point(),root);ScrollChartsForCheck();UpdateLayout();
            if(beforeScroll!=equationView.TranslatePoint(new Point(),root))throw new Exception("결과 스크롤 시 수식 고정 실패");ResetChartsScroll();
            SetLightMode(true);board.HoverSlotForCheck(0,true);if(equationView.FontSizeFor("damage")!=24)throw new Exception("경량 모드 즉시 강조 실패");board.HoverSlotForCheck(0,false);SetLightMode(false);
            var watch=System.Diagnostics.Stopwatch.StartNew();for(var n=0;n<40;n++){SetEquationSource("mod",saved.Slots[0]!.ModId,M("Pressure Point").Name);equationView.Highlight([],immediate:true);}watch.Stop();
            return new{passed=true,mathFont=glyph.FamilyNames.Values.ToArray(),glyph.FontUri,cases,arcane=true,stance=true,statusExcluded=true,pinned=true,reducedMotion=true,cachedHoverMeanMilliseconds=watch.Elapsed.TotalMilliseconds/40};
        }
        finally{equationDetails.IsExpanded=equationWasOpen;equationSource=null;ApplyBuild(saved);equationView.Highlight([],immediate:true);ResetChartsScroll();}
    }
    internal void EquationPreviewForCheck(string? source)
    {
        equationDetails.IsExpanded=source is not null;
        equationView.ReduceMotion=true;
        if(source is null){equationSource=null;equationView.Highlight([],immediate:true);equationView.ReduceMotion=lightMode;return;}
        if(source=="stance")SetEquationSource("stance",((Stance)stance.SelectedItem).Id,((Stance)stance.SelectedItem).Name);
        else {var mod=catalog.Mods.Single(m=>m.EnglishName==source);SetEquationSource("mod",mod.Id,mod.Name);}
    }
    private static void SettleEquationAnimation(Func<bool>? complete=null)
    {
        // Let the real animation reach its target, with a bounded deadline. A fixed
        // wall-clock delay can end between composition frames on an isolated desktop.
        var watch=System.Diagnostics.Stopwatch.StartNew();int stable=0;var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(33)};
        timer.Tick+=(_,_)=>{stable=(complete?.Invoke()??watch.ElapsedMilliseconds>=230)?stable+1:0;if(stable>=2||watch.ElapsedMilliseconds>=1800){timer.Stop();frame.Continue=false;}};timer.Start();Dispatcher.PushFrame(frame);
    }
}
