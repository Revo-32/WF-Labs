namespace TennoForge.Core;

public record ThalysShardEvent(int Target,bool Impale=false,bool Detonation=false,bool Root=false);

/// <summary>Target-owned shard clocks, independent from the global Incarnon timer.</summary>
public sealed class ThalysShardState
{
    private sealed class Host {public double End=-1,ImpaleEnd=-1;public int Growth;public long LastGrowth=-1;}
    private readonly Host[] hosts;
    private readonly bool growth,chain;
    public double Duration {get;}
    public ThalysShardState(int targets,bool explosiveGrowth=false,bool chainShatter=false,bool voidSplinters=false)
    {
        if(targets is <1 or >33)throw new ArgumentException("탈리스 가시 대상 수 오류");
        if(new[]{explosiveGrowth,chainShatter,voidSplinters}.Count(v=>v)>1)throw new ArgumentException("탈리스 진화V는 하나만 선택하세요.");
        hosts=Enumerable.Range(0,targets).Select(_=>new Host()).ToArray();growth=explosiveGrowth;chain=chainShatter;Duration=voidSplinters?30:15;
    }
    public bool Embedded(int target,double time)=>hosts[target].End>time;
    public int Growth(int target,double time)=>Embedded(target,time)?hosts[target].Growth:0;
    public double ImpaleRemaining(int target,double time)=>Math.Max(0,hosts[target].ImpaleEnd-time);
    public ThalysShardEvent[] Embed(int target,double time,long swing)
    {
        var h=hosts[target];
        // A second contact on an already embedded enemy is not a NEW shard.
        // The Wiki does not authorize refreshing its lifetime here.
        if(Embedded(target,time)||h.ImpaleEnd>time)return [];
        h.End=time+Duration;h.Growth=0;h.LastGrowth=-1;
        var events=new List<ThalysShardEvent>();
        for(var i=0;i<hosts.Length;i++)
        {
            if(i==target||!Embedded(i,time))continue;
            var other=hosts[i];var impale=false;
            if(growth&&other.LastGrowth!=swing)
            {
                other.LastGrowth=swing;other.Growth++;
                if(other.Growth==3){impale=true;other.End=-1;other.ImpaleEnd=time+5;}
            }
            events.Add(new(i));
            if(impale)events.Add(new(i,Impale:true));
        }
        return events.ToArray();
    }
    public ThalysShardEvent[] Detonate(int target,double time,Func<int,int,double> distance)
    {
        if(!chain||!Embedded(target,time))return [];
        var queue=new Queue<int>();var result=new List<ThalysShardEvent>();queue.Enqueue(target);hosts[target].End=-1;
        while(queue.Count>0)
        {
            var origin=queue.Dequeue();result.Add(new(origin,Detonation:true,Root:origin==target));
            for(var i=0;i<hosts.Length;i++)if(Embedded(i,time)&&distance(origin,i)<=5)
            {hosts[i].End=-1;queue.Enqueue(i);}
        }
        return result.ToArray();
    }
    public void Remove(int target){hosts[target].End=-1;hosts[target].ImpaleEnd=-1;}
}
