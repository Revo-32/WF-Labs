using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal record RivenCapturePreferences(string Kind="Melee",int Rank=8,bool Hotkey=true,bool Automatic=true,bool DetectKind=true)
{
    internal string ReadKind=>DetectKind?"Auto":Kind;
    private static string PathName=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WF Labs","riven-capture.json");
    internal static RivenCapturePreferences Read(){try{var p=JsonSerializer.Deserialize<RivenCapturePreferences>(File.ReadAllText(PathName));if(p is not null&&p.Rank is >=0 and <=8&&new[]{"Melee","Rifle","Shotgun","Pistol"}.Contains(p.Kind))return p;}catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException){}return new();}
    internal void Save(){if(!UiFonts.PersistChanges)return;Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);File.WriteAllText(PathName,JsonSerializer.Serialize(this));}
}

internal sealed class RivenCaptureWindow : Window
{
    internal readonly ComboBox Kind=new(){ItemsSource=new[]{"자동 판별","근접","소총","산탄총","권총"},MinWidth=140};
    internal readonly ComboBox Rank=new(){ItemsSource=Enumerable.Range(0,9).ToArray(),MinWidth=90};
    internal readonly CheckBox Automatic=new(){Content="자동 인식",Margin=new(10,8,10,8)};
    internal readonly CheckBox Shortcut=new(){Content="수동 재인식 단축키",ToolTip="Ctrl + Shift + V · 자동 인식에는 필요하지 않습니다.",Margin=new(10,8,10,8)};
    internal readonly TextBlock Status=new OperationStatusText("자동 인식을 켜면 리벤 카드와 순환 결과를 감지해 좌우에 표시합니다.");
    internal readonly Button LoadButton,PasteButton,CancelButton;
    internal Func<BitmapSource,string,int,CancellationToken,Task<RivenOcrResult>> ReadImage {get;set;}=(image,kind,rank,token)=>RivenOcr.ReadAsync(image,kind,rank,token);
    internal Func<BitmapSource?> ClipboardImage {get;set;}=()=>Clipboard.ContainsImage()?Clipboard.GetImage():null;
    internal readonly StackPanel Results=new();
    internal Action<RivenCapturePreferences>? SettingsChanged;
    internal Action<RivenScanReport>? ImportRequested;
    internal RivenCapturePreferences Preferences=>new(new[]{"Melee","Rifle","Shotgun","Pistol"}[Math.Clamp(Kind.SelectedIndex-1,0,3)],Rank.SelectedItem is int rank?rank:8,Shortcut.IsChecked==true,Automatic.IsChecked==true,Kind.SelectedIndex<=0);
    private CancellationTokenSource? pending;private bool closed;
    internal RivenOcrResult? LastResult;
    internal RivenCaptureWindow(RivenCapturePreferences preferences)
    {
        Title="WF Labs · 리벤 화면 인식";Width=1140;Height=850;MinWidth=800;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty,"SurfaceBrush");SetResourceReference(ForegroundProperty,"TextBrush");UseLayoutRounding=true;
        Kind.SelectedIndex=preferences.DetectKind?0:1+Array.IndexOf(new[]{"Melee","Rifle","Shotgun","Pistol"},preferences.Kind);Rank.SelectedItem=preferences.Rank;Automatic.IsChecked=preferences.Automatic;Shortcut.IsChecked=preferences.Hotkey;
        Status.Text=preferences.Automatic?"자동 인식 켜짐 · 게임의 리벤 카드와 순환 결과를 기다립니다.":"자동 인식 꺼짐 · 스크린샷이나 클립보드 이미지로 수동 인식할 수 있습니다.";
        var root=new DockPanel{Margin=new(22)};root.SetResourceReference(Panel.BackgroundProperty,"SurfaceBrush");Content=root;var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        header.Children.Add(RivenPresentation.Text("리벤 화면 인식",25));
        Kind.ToolTip="기본은 자동 판별입니다. 무기 이름과 종류별 옵션·수치 범위를 확인하며, 여러 종류가 가능한 경우에만 직접 선택합니다.";
        Rank.ToolTip="카드에 표시되는 수치의 리벤 랭크입니다. 게임의 ‘최대 랭크 보기’를 켰다면 8을 사용하세요.";
        Automatic.ToolTip="게임이 전경에 있을 때 작은 화면 샘플로 리벤 카드의 등장·변경을 감지합니다. 옵션이 바뀔 때만 인식하며 단축키를 누를 필요가 없습니다.";
        var settings=new WrapPanel();header.Children.Add(settings);
        settings.Children.Add(Field("리벤 종류",Kind));settings.Children.Add(Field("랭크",Rank));settings.Children.Add(Automatic);settings.Children.Add(Shortcut);
        var actions=new WrapPanel{Margin=new(0,10,0,10)};header.Children.Add(actions);
        LoadButton=Action("스크린샷 불러오기",async()=>{var dialog=new OpenFileDialog{Filter="이미지|*.png;*.jpg;*.jpeg;*.bmp"};if(dialog.ShowDialog(this)==true)await Read(RelicPath(dialog.FileName));});
        PasteButton=Action("클립보드 이미지 읽기",async()=>{if(ClipboardImage() is {} image)await Read(image);else Status.Text="클립보드 이미지 확인 필요 · 스크린샷을 복사한 뒤 다시 읽어 주세요.";});
        CancelButton=Action("수동 인식 취소",()=>{RequestManualCancel();return Task.CompletedTask;});
        CancelButton.IsEnabled=false;CancelButton.ToolTip="이 창에서 시작한 파일·클립보드 인식을 취소합니다. 자동 인식은 위 스위치에서 끌 수 있습니다.";
        foreach(var button in new[]{LoadButton,PasteButton,CancelButton})actions.Children.Add(button);
        Status.ToolTip="수율은 최소~최대 범위 안의 위치입니다. 획득 확률이나 빌드 점수가 아닙니다. 음옵은 절대 크기를 비교합니다.";header.Children.Add(Status);
        root.Children.Add(new ScrollViewer{Content=Results,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,12,0)});
        Kind.SelectionChanged+=Changed;Rank.SelectionChanged+=Changed;Automatic.Checked+=Changed;Automatic.Unchecked+=Changed;Shortcut.Checked+=Changed;Shortcut.Unchecked+=Changed;
        Closed+=(_,_)=>{closed=true;pending?.Cancel();};
    }
    private static BitmapSource RelicPath(string path)=>RelicOcr.Load(path);
    private static FrameworkElement Field(string label,FrameworkElement control)=>new StackPanel{Margin=new(0,0,12,0),Children={RivenPresentation.Text(label,13,"MutedBrush"),control}};
    private Button Action(string label,Func<Task> action)
    {
        var button=new Button{Content=label,Padding=new(12,7,12,7),Margin=new(0,0,8,0)};
        button.Click+=async(_,_)=>{try{await action();}catch(Exception e)when(e is IOException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.ExternalException or Tesseract.TesseractException){Status.Text="작업 실패 · "+e.Message;}};return button;
    }
    private bool syncingAutomatic;
    internal void SyncAutomatic(bool value){syncingAutomatic=true;try{Automatic.IsChecked=value;}finally{syncingAutomatic=false;}}
    private void Changed(object? sender,RoutedEventArgs e)
    {
        if(syncingAutomatic)return;
        if(pending is not null){pending.Cancel();Status.Text="설정 변경으로 수동 인식을 취소했습니다. 변경된 설정으로 다시 읽을 수 있습니다.";}
        var p=Preferences;SettingsChanged?.Invoke(p);
        if((ReferenceEquals(sender,Kind)||ReferenceEquals(sender,Rank))&&LastResult is {} result)SetResult(result with{Cards=result.Cards.Select(c=>c with{Report=Parse(c.Text,p,c.TitleText),Corroborated=false,Corrected=false}).ToArray()});
    }
    private static RivenScanReport Parse(string text,RivenCapturePreferences p,string title)=>p.DetectKind?RivenKindDetection.Resolve(text,p.Rank,title):RivenScan.Parse(text,p.Kind,p.Rank);
    internal async Task Read(BitmapSource image)
    {
        pending?.Cancel();using var cancellation=new CancellationTokenSource();pending=cancellation;var p=Preferences;
        SetManualBusy(true);Status.Text="리벤 옵션을 읽고 있습니다…";
        try{var result=await ReadImage(image,p.ReadKind,p.Rank,cancellation.Token);if(!closed&&!cancellation.IsCancellationRequested)SetResult(result);}
        catch(OperationCanceledException){}
        finally
        {
            if(ReferenceEquals(pending,cancellation))
            {
                pending=null;SetManualBusy(false);
                if(cancellation.IsCancellationRequested&&Status.Text=="수동 인식 취소 요청 중…")Status.Text="수동 인식을 취소했습니다. 자동 인식은 현재 설정을 유지합니다.";
            }
        }
    }
    private void SetManualBusy(bool value){LoadButton.IsEnabled=!value;PasteButton.IsEnabled=!value;CancelButton.IsEnabled=value;}
    private void RequestManualCancel()
    {
        if(pending is null)return;
        pending.Cancel();CancelButton.IsEnabled=false;Status.Text="수동 인식 취소 요청 중…";
    }
    internal void SetResult(RivenOcrResult result)
    {
        LastResult=result;Results.Children.Clear();Status.Text=result.Cards.Length==0?result.Notice:$"{result.Cards.Length}개 카드 · {result.Seconds:0.0}초 · {Rank.SelectedItem}랭크 기준";
        var grid=new Grid();for(int i=0;i<Math.Max(1,result.Cards.Length);i++)grid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});Results.Children.Add(grid);
        for(int i=0;i<result.Cards.Length;i++)
        {
            int index=i;var card=result.Cards[i];var report=card.Report;var panel=new StackPanel{Margin=new(0,10,20,0)};Grid.SetColumn(panel,i);grid.Children.Add(panel);
            panel.Children.Add(RivenPresentation.Text(result.Cards.Length==1?"현재 카드":i==0?"화면 왼쪽":"화면 오른쪽",22));
            var kindText=report.DetectedKind&&report.KindCandidates.Length!=1?"종류 확인 필요 · "+string.Join(" / ",report.KindCandidates.Select(RivenScan.KindName)):RivenScan.KindName(report.Kind)+(report.DetectedKind?$" · 자동 판별 ({report.KindBasis})":" · 직접 선택");
            panel.Children.Add(RivenPresentation.Text(kindText,14,"MutedBrush"));
            panel.Children.Add(RivenPresentation.Text(card.Corrected?"직접 수정한 옵션":card.Corroborated?"옵션 재확인 완료":"인식 내용 확인 필요 · 자동 확정 전",14,card.Corroborated?"MutedBrush":"ErrorBrush"));
            if(report.Valid)panel.Children.Add(RivenPresentation.Text($"양옵 {report.Options.Count(o=>!o.Negative)}개 · 음옵 {report.Options.Count(o=>o.Negative)}개 · 접합 {report.Recipes.Length}가지",16,"AccentBrush"));
            var raw=new TextBox{Text=card.Corroborated&&report.Valid?RivenScan.Editable(report):card.Text,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=130,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontSize=16,Padding=new(10)};
            var apply=Action("수정 적용",()=>{var updated=Parse(raw.Text,Preferences,card.TitleText);var cards=LastResult!.Cards.ToArray();cards[index]=card with{Text=raw.Text,Report=updated,Corroborated=updated.Valid,Corrected=true};SetResult(LastResult with{Cards=cards});return Task.CompletedTask;});
            panel.Children.Add(new Expander{Header="인식 내용 수정",IsExpanded=!card.Corroborated||!report.Valid,Content=RivenPresentation.Stack(raw,apply),Margin=new(0,2,0,2)});
            var import=Action("리벤 분석에 가져오기",()=>{ImportRequested?.Invoke(report);return Task.CompletedTask;});import.IsEnabled=report.Valid;panel.Children.Add(import);
            foreach(var issue in report.Issues)panel.Children.Add(RivenPresentation.Text(issue,14,"ErrorBrush"));
            if(report.Recipes.Length>0)
            {
                var recipes=new StackPanel();foreach(var recipe in report.Recipes)recipes.Children.Add(RivenPresentation.Recipe(recipe.Ingredients,recipe.Result,$"{recipe.Minimum:0.###} ~ {recipe.Maximum:0.###} · 무작위 대체 {recipe.ReplacementOptions}종",recipe.Negative));
                panel.Children.Add(new Expander{Header=$"접합 {report.Recipes.Length}가지 · 조합과 대체 옵션",Content=recipes,Margin=new(0,2,0,2)});
            }
            foreach(var value in report.Values)panel.Children.Add(RivenPresentation.Range(value.Option.Name,value.Option.Unit,value.Range.Minimum,value.Range.Maximum,value.Option.Value,value.Option.Negative));
            if(report.Valid&&report.Options.Any(o=>report.Kind=="Melee"?RivenValues.IsSpliced(Enum.Parse<RivenStat>(o.Key)):RangedRivens.Attribute(o.Key).Spliced))panel.Children.Add(RivenPresentation.Text("이미 접합된 옵션이 있습니다. 재접합 예측은 제공하지 않습니다.",14,"MutedBrush"));
        }
    }
}
