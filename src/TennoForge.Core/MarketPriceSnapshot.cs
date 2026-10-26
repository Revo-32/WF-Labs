namespace TennoForge.Core;

public sealed record MarketSnapshotProgress(int Complete,int Total,int Failed,bool Running,DateTimeOffset CheckedUtc);

// Shared price store for relic comparison and future mastery purchasing costs.
// No work is awaited by the reward screen. Each successful item persists atomically;
// interruption or a failed update never destroys the previous snapshot.
public static class MarketPriceSnapshot
{
    public static async Task RunAsync(RelicMarketClient market,bool crossplay,Action<MarketSnapshotProgress> progress,Action<string> updated,CancellationToken token)
    {
        while(!token.IsCancellationRequested)
        {
            int complete=0,failed=0,total=0;
            try
            {
                var items=await market.PriceItemsAsync(token);total=items.Length;
                progress(new(0,total,0,true,DateTimeOffset.UtcNow));
                foreach(var item in items)
                {
                    token.ThrowIfCancellationRequested();
                    if(await market.SnapshotDueAsync(item,crossplay,token))
                    {
                        try{await market.RefreshSnapshotItemAsync(item,crossplay,token);updated(item.Id);}
                        catch(Exception e)when(!token.IsCancellationRequested&&e is HttpRequestException or System.IO.IOException or System.Text.Json.JsonException or InvalidOperationException or TaskCanceledException)
                        {
                            failed++;progress(new(complete,total,failed,true,DateTimeOffset.UtcNow));
                            // Honour server backoff and avoid hundreds of attempts offline.
                            if(failed>=3)break;
                            await Task.Delay(TimeSpan.FromSeconds(30),token);
                        }
                    }
                    complete++;progress(new(complete,total,failed,true,DateTimeOffset.UtcNow));
                }
            }
            catch(Exception e)when(!token.IsCancellationRequested&&e is HttpRequestException or System.IO.IOException or System.Text.Json.JsonException or InvalidOperationException or TaskCanceledException){failed++;}
            progress(new(complete,total,failed,false,DateTimeOffset.UtcNow));
            // Inspect ages periodically; only data older than 12 hours is fetched.
            await Task.Delay(TimeSpan.FromMinutes(failed>0?5:30),token);
        }
    }
}
