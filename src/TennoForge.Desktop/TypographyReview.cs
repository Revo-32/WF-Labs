using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TennoForge.Desktop;
internal static class TypographyReview
{
    internal static object FontAudit()
    {
        var faces=new List<object>();
        foreach(var weight in new[]{FontWeights.Normal,FontWeights.Medium,FontWeights.SemiBold,FontWeights.Bold})
        {
            var selected=(FontFamily)Application.Current.Resources[weight==FontWeights.Normal?"AppFont":weight==FontWeights.Bold?"AppFontBold":"AppFontMedium"];
            var face=new Typeface(selected,FontStyles.Normal,weight,FontStretches.Normal);
            if(!face.TryGetGlyphTypeface(out var glyph))throw new Exception("글꼴을 해석하지 못했습니다: "+UiFonts.Current.Id+" / "+selected.Source+" / "+weight);
            if(!glyph.CharacterToGlyphMap.ContainsKey('한')||!glyph.FontUri.ToString().Contains(UiFonts.Current.FileToken,StringComparison.OrdinalIgnoreCase))throw new Exception("선택한 글꼴이 아닌 폰트로 대체되었습니다: "+glyph.FontUri);
            faces.Add(new{weight=weight.ToString(),source=glyph.FontUri.ToString(),families=glyph.FamilyNames.Values.ToArray()});
        }
        return new{passed=true,font=UiFonts.Current.Id,faces};
    }
    internal static FrameworkElement Create()
    {
        var grid=new Grid{Background=(Brush)Application.Current.Resources["ResultsBackgroundBrush"],Margin=new(0)};
        foreach(var _ in Enumerable.Range(0,2))grid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        var fonts=new[]{(FontFamily)Application.Current.Resources["Wanted"],(FontFamily)Application.Current.Resources["AppFont"]};
        for(int column=0;column<2;column++)
        {
            var panel=new StackPanel{Margin=new(20)};Grid.SetColumn(panel,column);grid.Children.Add(panel);
            panel.Children.Add(new TextBlock{Text=column==0?"Wanted Sans":UiFonts.Current.Label,FontSize=24,Margin=new(0,0,0,16)});
            foreach(var mode in new[]{TextFormattingMode.Display,TextFormattingMode.Ideal})foreach(var size in new[]{13d,14,15,16})
            {
                panel.Children.Add(new TextBlock{Text=$"{mode} · {size}px",FontSize=14,Margin=new(0,8,0,4)});
                var sample=new TextBlock{Text="컨디션 오버로드 · 치명타 확률 184.8%\n근접 피해 +120% · 상태이상 확률 +60%",FontFamily=fonts[column],FontSize=size,LineHeight=24,TextWrapping=TextWrapping.NoWrap};
                TextOptions.SetTextFormattingMode(sample,mode);panel.Children.Add(sample);
            }
        }
        return grid;
    }
    internal static object CheckSelection(string folder)
    {
        // Exercise the actual settings event on already-created controls; never alter user preferences.
        var original=UiFonts.Current.Id;var persist=UiFonts.PersistChanges;UiFonts.PersistChanges=false;
        try
        {
            var selector=UiFonts.CreateSelector();
            var combo=((StackPanel)selector).Children.OfType<ComboBox>().Single();
            var preview=new TextBlock{Text="근접 피해 +120% · 상태이상 확률 61.6%",FontSize=15};
            var input=new TextBox{Text="184.8"};var button=new Button{Content="현재 모딩에 적용"};
            var heading=new TextBlock{Text="계산 결과",FontSize=20,FontWeight=FontWeights.SemiBold};
            var host=new StackPanel{Margin=new(16),Background=(Brush)Application.Current.Resources["ResultsBackgroundBrush"]};foreach(var element in new FrameworkElement[]{selector,heading,preview,input,button})host.Children.Add(element);
            var window=new Window{Content=host};window.SetResourceReference(Control.FontFamilyProperty,"AppFont");
            var reports=new List<object>();var path=System.IO.Path.Combine(folder,"test-appearance.json");
            foreach(var choice in UiFonts.Choices)
            {
                combo.SelectedItem=choice;var audit=FontAudit();
                foreach(var scale in new[]{1d,1.25,1.5})
                {
                    App.Render(host,System.IO.Path.Combine(folder,$"settings-{choice.Id}-{scale}.png"),420,440,scale);
                    var issues=LayoutDiagnostics.Inspect(host);if(issues.Length>0)throw new Exception(System.Text.Json.JsonSerializer.Serialize(issues));
                }
                foreach(var family in new[]{preview.FontFamily,input.FontFamily,button.FontFamily})if(family.Source!=((FontFamily)Application.Current.Resources["AppFont"]).Source)throw new Exception("기존 본문·입력·버튼의 글꼴이 갱신되지 않았습니다: "+family.Source+" → "+UiFonts.Current.Id);
                if(heading.FontFamily.Source!=((FontFamily)Application.Current.Resources["AppFontMedium"]).Source)throw new Exception("제목 굵기의 글꼴이 갱신되지 않았습니다.");
                if(input.Text!="184.8")throw new Exception("글꼴 변경 중 입력값이 바뀌었습니다.");
                UiFonts.Save(choice.Id,path);if(UiFonts.Read(path).Id!=choice.Id)throw new Exception("저장된 글꼴 복원 실패");
                reports.Add(audit);
            }
            System.IO.File.WriteAllText(path,"{invalid");if(UiFonts.Read(path).Id!="jamsil")throw new Exception("손상 설정 기본값 복구 실패");
            UiFonts.Save("unknown-font",path);if(UiFonts.Read(path).Id!="jamsil")throw new Exception("알 수 없는 글꼴 기본값 복구 실패");
            System.IO.File.Delete(path);
            return new{passed=true,fonts=reports,liveControls=true,persistence=true,preservedInput=true};
        }
        finally{UiFonts.Apply(original);UiFonts.PersistChanges=persist;}
    }
}
