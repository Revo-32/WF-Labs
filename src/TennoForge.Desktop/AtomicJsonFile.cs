using System.IO;
using System.Text;
using System.Text.Json;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal static class AtomicJsonFile
{
    internal static T Read<T>(string path,long maximumBytes=32*1024*1024)
    {
        if(new FileInfo(path).Length>maximumBytes)throw new ArgumentException("파일 크기가 허용 범위를 초과합니다.");
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path),Catalog.Json)??throw new ArgumentException("파일이 비어 있습니다.");
    }
    internal static void Write<T>(string path,T value)
    {
        path=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value,Catalog.Json));
        var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){file.Write(bytes);file.Flush(true);}
            if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
