using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;

namespace TennoForge.Desktop;

internal sealed record DiagnosticComponent(string Name,bool Present,long? Bytes);
internal sealed record ClientDiagnosticReport(int SchemaVersion,DateTimeOffset CreatedUtc,string AppVersion,string Runtime,string OperatingSystem,string Architecture,
    bool UserInteractive,bool ReducedMotion,double ScreenWidth,double ScreenHeight,long WorkingSetBytes,DiagnosticComponent[] Components);

// An allowlist report: never read inventory, OCR text, logs, account identifiers,
// settings paths, credentials, screen pixels or another process's details.
internal static class ClientDiagnostics
{
    internal static ClientDiagnosticReport Capture()
    {
        string[] names=["WFLabs.Startup.exe","x64/tesseract50.dll","x64/leptonica-1.82.0.dll","Assets/Ocr/wfkor.traineddata","Assets/Ocr/kor.traineddata","Assets/Ocr/eng.traineddata","Assets/Ocr/korean-line-v5.onnx"];
        var components=names.Select(name=>
        {
            try{var file=new FileInfo(Path.Combine(AppContext.BaseDirectory,name));return new DiagnosticComponent(name,file.Exists,file.Exists?file.Length:null);}
            catch(IOException){return new DiagnosticComponent(name,false,null);}catch(UnauthorizedAccessException){return new DiagnosticComponent(name,false,null);}
        }).ToArray();
        using var process=Process.GetCurrentProcess();
        return new(1,DateTimeOffset.UtcNow,VelopackUpdateFeed.AppVersion,RuntimeInformation.FrameworkDescription,RuntimeInformation.OSDescription,RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.UserInteractive,!UiMotion.Enabled,SystemParameters.PrimaryScreenWidth,SystemParameters.PrimaryScreenHeight,process.WorkingSet64,components);
    }
    internal static void Save(string path)=>File.WriteAllText(path,JsonSerializer.Serialize(Capture(),new JsonSerializerOptions{WriteIndented=true}));
}
