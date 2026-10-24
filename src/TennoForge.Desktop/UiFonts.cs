using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace TennoForge.Desktop;

internal static class UiFonts
{
    internal sealed record Choice(string Id,string Label,string Regular,string Medium,string Bold,string FileToken);
    internal static readonly Choice[] Choices = [
        new("jamsil","더잠실체","The Jamsil 3 Regular","The Jamsil 4 Medium","The Jamsil 5 Bold","jamsil"),
        new("pretendard","Pretendard","Pretendard","Pretendard SemiBold","Pretendard","pretendard"),
        new("wanted","Wanted Sans","Wanted Sans","Wanted Sans","Wanted Sans","wantedsans")
    ];
    internal static Choice Current {get;private set;}=Choices[0];
    internal static bool PersistChanges {get;set;}=true;
    private sealed record Preference(string FontId="jamsil");
    private static string PreferencePath=>Path.Combine(WFLabs.Client.ClientPreferences.Folder,"appearance.json");
    internal static Choice Read(string? path=null)
    {
        try{return Resolve(JsonSerializer.Deserialize<Preference>(File.ReadAllText(path??PreferencePath))?.FontId);}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException){return Choices[0];}
    }
    private static Choice Resolve(string? id)=>Choices.FirstOrDefault(x=>x.Id==id)??Choices[0];
    internal static void Save(string id,string? path=null)
    {
        path??=PreferencePath;Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(new Preference(Resolve(id).Id)));File.Move(temp,path,true);
    }
    internal static void Apply(string id)
    {
        Current=Resolve(id);
        FontFamily Family(string name)=>new(new Uri("pack://application:,,,/"),"./WFLabs;component/Fonts/#"+name);
        Application.Current.Resources["AppFont"]=Family(Current.Regular);
        Application.Current.Resources["AppFontMedium"]=Family(Current.Medium);
        Application.Current.Resources["AppFontBold"]=Family(Current.Bold);
        foreach(Window window in Application.Current.Windows)
            foreach(var plot in LayoutDiagnostics.Tree(window).OfType<Plot>())plot.InvalidateVisual();
    }
    internal static FrameworkElement CreateSelector()
    {
        var panel=new StackPanel();
        panel.Children.Add(new TextBlock{Text="글꼴",Margin=new(0,0,0,6)});
        var select=new ComboBox{ItemsSource=Choices,DisplayMemberPath=nameof(Choice.Label),SelectedItem=Current,MinHeight=38};
        AutomationProperties.SetName(select,"앱 글꼴");panel.Children.Add(select);
        var preview=new TextBlock{Text="치명타 확률 184.8%\n근접 피해 +120% · DPS 12,345.6",FontSize=15,LineHeight=24,Margin=new(0,10,0,0)};
        panel.Children.Add(preview);
        var status=new TextBlock{Text="선택 즉시 적용 · 다음 실행에도 유지",FontSize=14,Margin=new(0,6,0,0)};status.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");panel.Children.Add(status);
        select.SelectionChanged+=(_,_)=>
        {
            if(select.SelectedItem is not Choice choice)return;
            Apply(choice.Id);
            try{if(PersistChanges)Save(choice.Id);status.Text="선택 즉시 적용 · 다음 실행에도 유지";status.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){status.Text="현재 화면에는 적용했지만 설정을 저장하지 못했습니다.";status.SetResourceReference(TextBlock.ForegroundProperty,"ErrorBrush");}
        };
        return panel;
    }
}
