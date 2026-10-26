using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TennoForge.Desktop;

// A small glyph template, not a second OCR pass. MR is corroborated by the
// neighbouring R, a dark footer and the long separator immediately above it.
// Missing/occluded anchors simply leave the existing visual detector in charge.
internal static class RivenFooterAnchor
{
    private const string M="11100000111/11100000111/11100000111/11110001111/11110001111/11111011111/11111011011/11011011011/11011011011/11011011011/11001110011/11001110011/11000100011/11000000011/11000000011";
    private const string R="11111111000/11111111110/11100001110/11100000110/11100000110/11100000110/11100001110/11111111100/11111111000/11100111000/11100011100/11100011110/11100001110/11100000110/01100000111";
    private static readonly string[] TemplateM=M.Split('/'),TemplateR=R.Split('/');
    internal static Task<Rect[]> FindAsync(BitmapSource source,CancellationToken token)
    {
        var bgra=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);int w=bgra.PixelWidth,h=bgra.PixelHeight;
        var pixels=new byte[w*h*4];bgra.CopyPixels(pixels,w*4,0);
        return Task.Run(()=>
        {
            token.ThrowIfCancellationRequested();
            var detached=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,pixels,w*4);detached.Freeze();
            var result=Find(detached);token.ThrowIfCancellationRequested();return result;
        },token);
    }
    internal static Rect[] Find(BitmapSource source)
    {
        var normal=FindCore(source,false);
        if(normal.Length==2)return normal;
        var dim=FindCore(source,true);var combined=normal.ToList();
        foreach(var candidate in dim)
            if(!combined.Any(a=>Rect.Intersect(a,candidate).Width>Math.Min(a.Width,candidate.Width)*.4&&Rect.Intersect(a,candidate).Height>Math.Min(a.Height,candidate.Height)*.3))combined.Add(candidate);
        return combined.Count is 1 or 2?combined.OrderBy(a=>a.X).ToArray():normal;
    }
    private static Rect[] FindCore(BitmapSource source,bool dim)
    {
        using var scope=RivenPerformance.Measure("footer-anchor");
        double scale=Math.Min(1,2560d/source.PixelWidth);
        var image=new FormatConvertedBitmap(scale<1?new TransformedBitmap(source,new ScaleTransform(scale,scale)):source,PixelFormats.Bgra32,null,0);
        int w=image.PixelWidth,h=image.PixelHeight;var pixels=new byte[w*h*4];image.CopyPixels(pixels,w*4,0);var mask=new byte[w*h];
        for(int i=0;i<mask.Length;i++){int p=i*4,b=pixels[p],g=pixels[p+1],r=pixels[p+2];if(r>(dim?42:75)&&r-g>(dim?7:12)&&b-r>(dim?5:8))mask[i]=1;}
        var seen=new bool[w*h];var component=new List<int>();var glyphs=new List<Int32Rect>();
        for(int start=0;start<mask.Length;start++)
        {
            if(mask[start]==0||seen[start])continue;component.Clear();component.Add(start);seen[start]=true;int left=w,right=0,top=h,bottom=0;
            for(int n=0;n<component.Count;n++)
            {
                int p=component[n],x=p%w,y=p/w;left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {int xx=x+dx,yy=y+dy;if(xx<0||xx>=w||yy<0||yy>=h)continue;int q=yy*w+xx;if(mask[q]!=0&&!seen[q]){seen[q]=true;component.Add(q);}}
            }
            int height=bottom-top+1,width=right-left+1;
            if(height>=8&&height<=60&&width>=height*.45&&width<=height*1.1&&component.Count>height*2)glyphs.Add(new(left,top,width,height));
        }
        double Error(Int32Rect b,string[] pattern)
        {
            int mismatches=0;
            for(int y=0;y<15;y++)for(int x=0;x<11;x++)
            {int xx=b.X+Math.Min(b.Width-1,x*b.Width/11),yy=b.Y+Math.Min(b.Height-1,y*b.Height/15);if((mask[yy*w+xx]!=0)!=(pattern[y][x]=='1'))mismatches++;}
            return mismatches/165d;
        }
        var anchors=new List<Rect>();
        foreach(var m in glyphs.Where(b=>Error(b,TemplateM)<.25))
        foreach(var r in glyphs.Where(b=>b.X>=m.X+m.Width&&b.X<=m.X+m.Width+m.Height*.6&&Math.Abs(b.Y-m.Y)<m.Height*.16&&Math.Abs(b.Height-m.Height)<m.Height*.2&&Error(b,TemplateR)<.25))
        {
            int size=m.Height;int left=Math.Max(0,m.X-size),right=Math.Min(w,m.X+size*18),top=Math.Max(0,m.Y-size*14);
            int separator=0,lineLeft=left,lineRight=right,lineY=0;
            for(int y=Math.Max(0,m.Y-size);y<m.Y-size*.12;y++)
            {
                int ink=0,first=right,last=left;
                for(int x=left;x<right;x++)if(mask[y*w+x]!=0){ink++;first=Math.Min(first,x);last=x+1;}
                if(ink>separator){separator=ink;lineLeft=first;lineRight=last;lineY=y;}
            }
            if(separator<size*5)continue;
            // Uncycled cards center MR and have no reroll counter. Follow the
            // actual separator around MR instead of treating it as a left edge.
            int center=m.X+m.Width/2,gaps=0,extendedLeft=center,extendedRight=center,gapLimit=Math.Max(2,size/6);
            for(int x=center;x>=Math.Max(0,m.X-size*12);x--)
            {if(mask[lineY*w+x]!=0){extendedLeft=x;gaps=0;}else if(++gaps>gapLimit)break;}
            gaps=0;
            for(int x=center;x<Math.Min(w,m.X+size*22);x++)
            {if(mask[lineY*w+x]!=0){extendedRight=x+1;gaps=0;}else if(++gaps>gapLimit)break;}
            if(extendedLeft<m.X-size*3&&extendedRight-extendedLeft>=size*8&&extendedRight-extendedLeft<=size*25)
            {lineLeft=extendedLeft;lineRight=extendedRight;left=Math.Max(0,lineLeft-size/2);right=Math.Min(w,lineRight+size/2);}
            int dark=0,total=0;
            for(int y=m.Y;y<Math.Min(h,m.Y+size);y+=2)for(int x=left;x<right;x+=3)
            {int p=(y*w+x)*4;total++;if(Math.Max(pixels[p],Math.Max(pixels[p+1],pixels[p+2]))<115)dark++;}
            if(dark<total*.70)continue;
            // On a small card, the footer edge excludes animation that could
            // connect the first text row to its frame. A zoomed card may let
            // long faction labels overflow that edge, so retain its margin.
            if((right-left)/(double)w<=.14)
            {left=Math.Max(left,lineLeft+size/5);right=Math.Min(right,lineRight-size/5);}
            var box=new Rect(left/(double)w,top/(double)h,(right-left)/(double)w,(m.Y-top)/(double)h);
            if(!anchors.Any(a=>Rect.Intersect(a,box).Width>box.Width*.5))anchors.Add(box);
        }
        // Inventory lists contain numerous MR footers. They must never become
        // expanded-card candidates even if individual glyph pairs match.
        return anchors.Count is 1 or 2?anchors.OrderBy(a=>a.X).ToArray():[];
    }
}
