namespace TennoForge.Core;

public enum RelicRewardSignal { None, Open, Close }

// Message identifiers observed in the public WFInfo log integration. This parser
// and state machine are independently implemented; no external implementation is copied.
public sealed class RelicRewardSignals
{
    private readonly HashSet<string> seen=[];
    private readonly Queue<string> order=[];
    private long openedAt=long.MinValue;
    public static RelicRewardSignal Classify(string line)
    {
        int marker=line.IndexOf("Script [Info]:",StringComparison.Ordinal);
        if(marker>=0)
        {
            var body=line[(marker+14)..].Trim();
            // Real game logs include the Lua emitter. Only strip known emitters;
            // quoted chat text and arbitrary script messages remain ineligible.
            foreach(var emitter in new[]{"ProjectionRewardChoice.lua: ","ProjectionsCountdown.lua: "})
                if(body.StartsWith(emitter,StringComparison.Ordinal)){body=body[emitter.Length..];break;}
            if(body is "Pause countdown done" or "Got rewards")return RelicRewardSignal.Open;
            // Relic timer closed also occurs BEFORE Got rewards (the selection
            // of a relic ends). It does not establish reward-screen closure.
            if(body is "Selection countdown done" or "Relic reward screen shut down")return RelicRewardSignal.Close;
        }
        if(line.Contains("Net [Info]:",StringComparison.Ordinal)&&line.Contains("MatchingService::EndSession",StringComparison.Ordinal))return RelicRewardSignal.Close;
        return RelicRewardSignal.None;
    }
    public RelicRewardSignal Accept(string line,long nowMilliseconds)
    {
        var signal=Classify(line);if(signal==RelicRewardSignal.None)return signal;
        // The same event can arrive from debug output and later from EE.log.
        var key=line.Trim();if(!seen.Add(key))return RelicRewardSignal.None;
        order.Enqueue(key);if(order.Count>128)seen.Remove(order.Dequeue());
        if(signal==RelicRewardSignal.Close)
        {
            if(openedAt==long.MinValue)return RelicRewardSignal.None;
            openedAt=long.MinValue;return signal;
        }
        if(openedAt!=long.MinValue&&nowMilliseconds-openedAt<30_000)return RelicRewardSignal.None;
        openedAt=nowMilliseconds;return signal;
    }
}
