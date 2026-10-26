using System.Text.Json;

namespace TennoForge.Core;
public sealed record InventoryCandidateInfo(string State,int RootStart,int Bytes,int Members,string[] KnownFields,bool InventoryComplete,bool MasteryComplete,int Equipment);

// Locates a complete JSON object around a known inventory field. This parser is
// independent of any client address/offset and rejects incomplete or unrelated JSON.
public static class InventoryJsonLocator
{
    public static (InventorySnapshot Snapshot,int Start,int Length)? Find(ReadOnlySpan<byte> data,InventoryCatalog catalog)
    {
        ReadOnlySpan<byte> marker="\"XPInfo\""u8;int search=0;
        for(int attempt=0;attempt<32&&search<data.Length;attempt++)
        {
            int rel=FindField(data[search..],"XPInfo"u8);if(rel<0)break;int at=search+rel;search=at+marker.Length;
            int start=EnclosingObject(data,at);if(start<0)continue;
            try
            {
                var reader=new Utf8JsonReader(data[start..],new JsonReaderOptions{MaxDepth=80});
                if(!JsonDocument.TryParseValue(ref reader,out var doc))continue;
                using(doc)
                {
                    var root=doc!.RootElement;
                    if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("XPInfo",out _)||!root.TryGetProperty("Suits",out _)||!root.TryGetProperty("MiscItems",out _)||!root.TryGetProperty("PlayerLevel",out _))continue;
                    var s=InventoryImport.Parse(root,catalog,"게임 로컬 데이터 · 읽기 전용");
                    if(s.InventoryComplete&&s.MasteryComplete&&s.Equipment.Length>0)return(s,start,checked((int)reader.BytesConsumed));
                }
            }
            catch(Exception e)when(e is JsonException or InvalidDataException or InvalidOperationException or OverflowException or ArgumentException){}
        }
        return Recover(data,catalog);
    }
    private static (InventorySnapshot Snapshot,int Start,int Length)? Recover(ReadOnlySpan<byte> data,InventoryCatalog catalog)
    {
        // Allocators may overwrite the opening header of a released response while
        // leaving its JSON members intact. Accept only one syntactically complete
        // suffix with every required inventory array; never join separate fragments
        // or invent a missing closing brace, item, quantity, XP or MR value.
        var starts=new SortedSet<int>();
        foreach(var key in new[]{"PlayerLevel","RegularCredits","Suits","LongGuns","Pistols","Melee","MiscItems","Recipes","XPInfo"})
        {
            int offset=0;var marker=System.Text.Encoding.UTF8.GetBytes(key);
            for(int i=0;i<16&&offset<data.Length;i++){int found=FindField(data[offset..],marker);if(found<0)break;starts.Add(offset+found);offset+=found+marker.Length+2;}
        }
        foreach(int start in starts.Take(64))
        {
            if(data.Length-start>32*1024*1024)continue;var json=new byte[data.Length-start+1];json[0]=(byte)'{';data[start..].CopyTo(json.AsSpan(1));
            try
            {
                var reader=new Utf8JsonReader(json,new JsonReaderOptions{MaxDepth=80});if(!JsonDocument.TryParseValue(ref reader,out var doc))continue;
                using(doc)
                {
                    var root=doc!.RootElement;if(root.ValueKind!=JsonValueKind.Object)continue;
                    string[] required=["Suits","LongGuns","Pistols","Melee","MiscItems","Recipes","XPInfo"];
                    if(!required.All(key=>root.TryGetProperty(key,out var a)&&a.ValueKind==JsonValueKind.Array))continue;
                    var names=root.EnumerateObject().Select(p=>p.Name).ToArray();if(names.Distinct(StringComparer.Ordinal).Count()!=names.Length)continue;
                    var snapshot=InventoryImport.Parse(root,catalog,"게임 응답 복구 · 메타데이터 일부 미확인");
                    if(snapshot.InventoryComplete&&snapshot.MasteryComplete&&snapshot.Equipment.Length>0)return(snapshot,start,(int)reader.BytesConsumed-1);
                }
            }
            catch(Exception e)when(e is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){}
            finally{Array.Clear(json);}
        }
        return null;
    }
    public static int FindField(ReadOnlySpan<byte> data,ReadOnlySpan<byte> key)
    {
        int at=0;
        while(at<data.Length)
        {int rel=data[at..].IndexOf(key);if(rel<0)return -1;int found=at+rel;at=found+key.Length;int end=at;if(found==0||data[found-1]!='"'||end>=data.Length||data[end]!='"')continue;end++;while(end<data.Length&&data[end] is (byte)' ' or (byte)'\r' or (byte)'\n' or (byte)'\t')end++;if(end<data.Length&&data[end]==':')return found-1;}
        return -1;
    }
    public static InventoryCandidateInfo InspectCandidate(ReadOnlySpan<byte> data,InventoryCatalog catalog)
    {
        var keys=new[]{"Suits","LongGuns","Pistols","Melee","MiscItems","Recipes","XPInfo","PlayerLevel","LastInventorySync"};
        var present=new List<string>();foreach(var key in keys)if(FindField(data,System.Text.Encoding.UTF8.GetBytes(key))>=0)present.Add(key);
        int marker=FindField(data,"XPInfo"u8),start=marker<0?-1:EnclosingObject(data,marker);
        if(start<0)return new("root-missing",start,data.Length,0,present.ToArray(),false,false,0);
        try{var reader=new Utf8JsonReader(data[start..],new JsonReaderOptions{MaxDepth=80});JsonDocument.TryParseValue(ref reader,out var doc);using(doc){var root=doc!.RootElement;var s=InventoryImport.Parse(root,catalog,"진단");return new("parsed",start,(int)reader.BytesConsumed,root.EnumerateObject().Count(),present.ToArray(),s.InventoryComplete,s.MasteryComplete,s.Equipment.Length);}}
        catch(Exception e)when(e is JsonException or InvalidDataException or InvalidOperationException or OverflowException or ArgumentException){return new(e.GetType().Name,start,data.Length,0,present.ToArray(),false,false,0);}
    }
    private static int EnclosingObject(ReadOnlySpan<byte> bytes,int at)
    {
        int nesting=0;bool quoted=false;
        for(int i=at-1;i>=0&&at-i<=8*1024*1024;i--)
        {
            byte b=bytes[i];
            if(b=='"')
            {
                int slashes=0;for(int j=i-1;j>=0&&bytes[j]=='\\';j--)slashes++;
                if(slashes%2==0)quoted=!quoted;
            }
            if(quoted)continue;
            if(b=='}')nesting++;
            else if(b=='{'&&nesting--==0)return i;
        }
        return -1;
    }
}
