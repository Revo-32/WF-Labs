namespace TennoForge.Core;

public enum RivenWatchAction { None, Read, Clear }

// Bounded state machine: stable entry/change -> one read; unchanged frames -> none.
// A short retry budget handles reveal animations without continually re-running OCR.
public sealed class RivenWatchState(int signatureWidth=0)
{
    private byte[]? previous,read;private int stable,missing,retries;private long lastRead=-10000;
    private bool engaged;private int candidates,changedFrames;
    public static double Difference(byte[] a,byte[] b)
    {
        if(a.Length!=b.Length)return 1;int changed=0,total=0;
        for(int i=0;i<a.Length;i++){if(a[i]!=b[i])changed++;if(a[i]!=0||b[i]!=0)total++;}
        return changed/(double)Math.Max(1,total);
    }
    public static double AlignedDifference(byte[] a,byte[] b,int width)
    {
        if(width<=0||a.Length!=b.Length||a.Length%width!=0)return Difference(a,b);
        int height=a.Length/width,minX=width,maxX=-1,minY=height,maxY=-1;
        for(int i=0;i<a.Length;i++)if(a[i]!=0||b[i]!=0){int x=i%width,y=i/width;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
        if(maxX<0)return 0;double best=1;
        for(int dy=-3;dy<=3;dy++)for(int dx=-3;dx<=3;dx++)
        {
            int changed=0,total=0;
            for(int y=Math.Max(0,minY-3);y<=Math.Min(height-1,maxY+3);y++)for(int x=Math.Max(0,minX-3);x<=Math.Min(width-1,maxX+3);x++)
            {
                byte av=a[y*width+x];int bx=x+dx,by=y+dy;byte bv=bx>=0&&bx<width&&by>=0&&by<height?b[by*width+bx]:(byte)0;
                if(av!=bv)changed++;if(av!=0||bv!=0)total++;
            }
            best=Math.Min(best,changed/(double)Math.Max(1,total));if(best==0)return 0;
        }
        return best;
    }
    private double Compare(byte[] a,byte[] b)=>AlignedDifference(a,b,signatureWidth);
    public void Reset(){previous=null;read=null;engaged=false;stable=missing=retries=candidates=changedFrames=0;lastRead=-10000;}
    public void Retry(){if(retries<2){read=null;retries++;}}
    public void Prime(byte[] signature,long now){previous=read=signature;stable=2;changedFrames=0;engaged=true;lastRead=now;}
    public RivenWatchAction Observe(bool candidate,byte[] signature,long now,bool allowInitialRead=false)
    {
        if(!candidate)
        {
            previous=null;stable=candidates=0;if(++missing>=2){bool had=engaged;Reset();return had?RivenWatchAction.Clear:RivenWatchAction.None;}return RivenWatchAction.None;
        }
        missing=0;candidates++;stable=previous is not null&&Compare(previous,signature)<.12?stable+1:1;previous=signature;
        if(read is not null&&Compare(read,signature)<.012){changedFrames=0;return RivenWatchAction.None;}
        changedFrames++;
        // Before the first OCR, candidate bounds can contain animated decoration.
        // Two visual detections may bootstrap one read; subsequent changes use only text.
        if((stable<2&&!(allowInitialRead&&!engaged&&candidates>=2)&&!(engaged&&changedFrames>=3))||now-lastRead<1800)return RivenWatchAction.None;
        if(read is not null)retries=0;read=signature;engaged=true;lastRead=now;return RivenWatchAction.Read;
    }
}
