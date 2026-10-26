using System.IO;

namespace TennoForge.Desktop;

internal static class OcrModels
{
    internal static string DirectoryPath=>Path.Combine(AppContext.BaseDirectory,"Assets","Ocr");
    // The environment override is for reproducible baseline/candidate checks.
    // A packaged trained model is shared by Korean relic and Riven recognition.
    internal static string Korean=>Environment.GetEnvironmentVariable("WF_LABS_OCR_MODEL")=="kor"?"kor":File.Exists(Path.Combine(DirectoryPath,"wfkor.traineddata"))?"wfkor":"kor";
    internal static string Mixed=>Korean+"+eng";
    internal static string ForRelics(int width,int height)=>width>=1920&&height>=1080?Korean:"kor";
}
