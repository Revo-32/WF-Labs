using System.IO;
using System.Text.Json;

namespace WFLabs.Client;
public record ClientPreferences(bool StartWithWindows=false,bool StartWithWarframe=false,
    bool CheckUpdatesAutomatically=true,bool DownloadUpdatesAutomatically=true)
{
    public static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WF Labs");
    public static string FilePath=>Path.Combine(Folder,"client-settings.json");
    public static ClientPreferences Load()
    {try{return JsonSerializer.Deserialize<ClientPreferences>(File.ReadAllText(FilePath))??new();}catch(Exception ex)when(ex is IOException or JsonException or UnauthorizedAccessException){return new();}}
    public void Save()
    {Directory.CreateDirectory(Folder);var temp=FilePath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,FilePath,true);}
}
