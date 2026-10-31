using System.Windows.Controls;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal readonly CheckBox RivenAutomatic=new();
    internal readonly TextBlock RivenAutoStatus=new(){FontSize=13};
    internal RecognitionStatusBar RivenRecognitionBar=null!;
    private bool syncingRivenRecognition;
    private void SetRivenAutomatic(bool value)
    {
        if(syncingRivenRecognition)return;
        ConfigureRivenCapture(rivenCapturePreferences with{Automatic=value});
    }
    private void SyncRivenRecognition()
    {
        syncingRivenRecognition=true;
        try
        {
            RivenAutomatic.IsChecked=rivenCapturePreferences.Automatic;
            RivenAutoStatus.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");
            RivenAutoStatus.Text=string.IsNullOrWhiteSpace(rivenCaptureStatus)?"리벤 화면을 열거나 순환하면 자동으로 갱신합니다.":rivenCaptureStatus;
            rivenCaptureWindow?.SyncAutomatic(rivenCapturePreferences.Automatic);
        }
        finally{syncingRivenRecognition=false;}
    }
}
