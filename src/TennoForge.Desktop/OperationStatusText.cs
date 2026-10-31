using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TennoForge.Desktop;

// Operation feedback changes when its message changes, without a polling timer
// or a dependency-property descriptor that could retain the page after closing.
internal sealed class OperationStatusText : TextBlock
{
    static OperationStatusText()=>TextProperty.OverrideMetadata(typeof(OperationStatusText),
        new FrameworkPropertyMetadata("",(sender,_)=>((OperationStatusText)sender).UpdateTone()));
    internal string ToneKey {get;private set;}="MutedBrush";
    internal OperationStatusText(string text,double size=15)
    {
        FontSize=size;TextWrapping=TextWrapping.Wrap;LineHeight=Math.Ceiling(size*1.5);Margin=new(0,0,0,8);
        Typography.SetNumeralAlignment(this,FontNumeralAlignment.Tabular);
        AutomationProperties.SetLiveSetting(this,AutomationLiveSetting.Polite);
        Text=text;UpdateTone();
    }
    private void UpdateTone()
    {
        ToneKey=Text.Contains("실패")||Text.Contains("오류")||Text.Contains("찾지 못")?"ErrorBrush":
            Text.Contains("확인 필요")||Text.Contains("누락")?"AccentBrush":"MutedBrush";
        SetResourceReference(ForegroundProperty,ToneKey);
    }
}
