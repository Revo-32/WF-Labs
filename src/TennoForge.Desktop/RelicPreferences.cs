using System.IO;
using System.Text.Json;

namespace TennoForge.Desktop;

internal sealed record RelicPreferences(bool Hotkey=false,int Presence=0,bool Crossplay=true,bool Overlay=true,bool Automatic=true)
{
    private static string FilePath=>Path.Combine(WFLabs.Client.ClientPreferences.Folder,"relic-settings.json");
    internal static RelicPreferences Read()
    {
        if(!UiFonts.PersistChanges)return new();
        try{var p=JsonSerializer.Deserialize<RelicPreferences>(File.ReadAllText(FilePath))??new();return p with{Presence=Math.Clamp(p.Presence,0,1)};}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){return new();}
    }
    internal void Save()
    {
        if(!UiFonts.PersistChanges)return;
        try{Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);var temp=FilePath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,FilePath,true);}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){ }
    }
}
