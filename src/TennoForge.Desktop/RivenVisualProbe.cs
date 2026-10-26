using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TennoForge.Desktop;

internal record RivenProbe(Rect[] Regions,byte[] Signature);
internal static class RivenVisualProbe
{
    private static readonly (int X,int Y)[] Neighbors=[(-3,-3),(0,-3),(3,-3),(-3,0),(3,0),(-3,3),(0,3),(3,3)];
    // Small color/geometry probe only. No OCR or neural inference on timer ticks.
    internal static RivenProbe Analyze(BitmapSource source,Rect[]? knownRegions=null)
    {
        var small=source.PixelWidth>640?new TransformedBitmap(source,new ScaleTransform(640d/source.PixelWidth,640d/source.PixelWidth)):source;
        var image=new FormatConvertedBitmap(small,PixelFormats.Bgra32,null,0);int w=image.PixelWidth,h=image.PixelHeight;
        var pixels=new byte[w*h*4];image.CopyPixels(pixels,w*4,0);var mask=new byte[w*h];int totalInk=0;
        // A dim roll needs a lower threshold only inside a corroborated MR
        // footer column. Applying it globally mistakes animated purple ribbons
        // for a second card and repeatedly restarts OCR.
        var footerSource=source.PixelWidth<=640?new TransformedBitmap(source,new ScaleTransform(2,2)):source;
        var footers=RivenFooterAnchor.Find(footerSource);
        var dimColumns=footers.Where(a=>
        {
            long sum=0;int count=0;
            for(int yy=(int)((a.Y+a.Height*.4)*h);yy<Math.Min(h,(a.Y+a.Height)*h);yy++)for(int xx=(int)(a.X*w);xx<Math.Min(w,(a.X+a.Width)*w);xx++)
            {int p=(yy*w+xx)*4,b=pixels[p],g=pixels[p+1],r=pixels[p+2];if(r>42&&r-g>7&&b-r>5){sum+=r;count++;}}
            return count>30&&sum/(double)count<110;
        }).ToArray();
        bool Dark(int x,int y){var p=(y*w+x)*4;return Math.Max(pixels[p],Math.Max(pixels[p+1],pixels[p+2]))<105;}
        for(int y=4;y<h-4;y++)for(int x=4;x<w-4;x++)
        {
            int p=(y*w+x)*4,b=pixels[p],g=pixels[p+1],r=pixels[p+2];
            bool dim=dimColumns.Any(a=>x>=a.X*w&&x<(a.X+a.Width)*w&&y>=a.Y*h&&y<(a.Y+a.Height)*h);
            if(r<=(dim?42:75)||r-g<=(dim?7:12)||b-r<=(dim?5:8))continue;
            int dark=0;foreach(var d in Neighbors)if(Dark(x+d.X,y+d.Y))dark++;
            if(dark>=4){mask[y*w+x]=1;totalInk++;}
        }
        // Card text consists of small glyph components. Reject long connected
        // purple silhouettes, frames and lightning before looking for text rows.
        var seen=new bool[w*h];var component=new List<int>();
        for(int start=0;start<mask.Length;start++)
        {
            if(mask[start]==0||seen[start])continue;component.Clear();component.Add(start);seen[start]=true;int minX=w,maxX=0,minY=h,maxY=0;
            for(int n=0;n<component.Count;n++)
            {
                int p=component[n],x=p%w,y=p/w;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {int xx=x+dx,yy=y+dy;if(xx<0||xx>=w||yy<0||yy>=h)continue;int q=yy*w+xx;if(mask[q]!=0&&!seen[q]){seen[q]=true;component.Add(q);}}
            }
            if(component.Count<2||component.Count>220||maxX-minX>40||maxY-minY>22)foreach(int p in component){mask[p]=0;totalInk--;}
        }
        if(totalInk<65)return new([],mask);
        // Inventory tiles form many text rows across most of the viewport. They
        // are not an expanded/reroll card, even though their names are purple.
        int wideRows=0;
        for(int y=(int)(h*.4);y<(int)(h*.9);y++)
        {
            int ink=0,first=w,last=0;
            for(int x=0;x<w;x++)if(mask[y*w+x]!=0){ink++;first=Math.Min(first,x);last=x;}
            if(ink>35&&last-first>w*.7)wideRows++;
        }
        if(wideRows>=12)return new([],new byte[w*h]);
        var rowPrefix=new int[h*(w+1)];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)rowPrefix[y*(w+1)+x+1]=rowPrefix[y*(w+1)+x]+mask[y*w+x];
        var candidates=new List<(Rect Box,int Score)>();
        foreach(double width in new[]{.10,.16,.26})foreach(double height in new[]{.20,.36})
        {
        int ww=(int)(w*width),hh=(int)(h*height);
        for(int x=(int)(w*.12);x+ww<=w*.9;x+=6)for(int y=(int)(h*.20);y+hh<=h*.85;y+=6)
        {
            int ink=0,bands=0,active=0,gap=4,first=-1,last=-1,minX=w,maxX=0;
            for(int yy=y;yy<y+hh;yy++)
            {
                int count=rowPrefix[yy*(w+1)+x+ww]-rowPrefix[yy*(w+1)+x];
                if(count>=5&&count<ww*.55){if(gap>=3)bands++;gap=0;active++;ink+=count;if(first<0)first=yy;last=yy;}
                else gap++;
            }
            if(bands<2||bands>14||active<9||ink<65||first<0||last<h*.40)continue;
            int dark=0,total=0;for(int yy=first;yy<=last;yy+=3)for(int xx=x;xx<x+ww;xx+=3){total++;if(Dark(xx,yy))dark++;}
            if(dark<total*.82)continue;
            for(int yy=first;yy<=last;yy++)for(int xx=x;xx<x+ww;xx++)if(mask[yy*w+xx]!=0){minX=Math.Min(minX,xx);maxX=Math.Max(maxX,xx);}
            if(maxX-minX<ww*.3)continue;
            candidates.Add((new Rect(Math.Max(0,minX-10),Math.Max(0,first-8),Math.Min(w-minX+10,maxX-minX+21),Math.Min(h-first+8,last-first+17)),ink));
        }
        }
        var selected=new List<Rect>();foreach(var c in candidates.OrderByDescending(c=>c.Score))
        {
            if(selected.Count>0&&Math.Abs(selected[0].Y+selected[0].Height/2-c.Box.Y-c.Box.Height/2)>h*.12)continue;
            if(selected.Any(b=>Rect.Intersect(b,c.Box).Width>Math.Min(b.Width,c.Box.Width)*.10))continue;
            selected.Add(c.Box);if(selected.Count==2)break;
        }
        var regions=selected;
        if(knownRegions is {Length:>0})
        {
            // OCR already supplies a padded text rectangle. Percentage padding pulled
            // the animated frame back into the signature, especially on ultrawide.
            regions=knownRegions.Select(b=>new Rect(Math.Max(0,b.X*w),Math.Max(0,b.Y*h),Math.Min(w,b.Width*w),Math.Min(h,b.Height*h))).ToList();
            // A second physical card may appear while the previously recognized
            // card is unchanged. Include that separate column in the change probe.
            foreach(var candidate in selected)
                if(regions.All(b=>Math.Min(b.Right,candidate.Right)-Math.Max(b.Left,candidate.Left)<=0)&&footers.Any(a=>candidate.Contains(new Point((a.X+a.Width/2)*w,(a.Y+a.Height*.7)*h))))regions.Add(candidate);
        }
        var signature=new byte[w*h];foreach(var region in regions)for(int y=(int)region.Top;y<Math.Min(h,region.Bottom);y++)for(int x=(int)region.Left;x<Math.Min(w,region.Right);x++)
        {
            var p=(y*w+x)*4;int hi=Math.Max(pixels[p],Math.Max(pixels[p+1],pixels[p+2])),lo=Math.Min(pixels[p],Math.Min(pixels[p+1],pixels[p+2]));
            // Locked/spliced lines are neutral white, not purple. Track their
            // changes too, but only inside an already recognized text rectangle.
            signature[y*w+x]=mask[y*w+x]!=0||(knownRegions is {Length:>0}&&hi>75&&hi-lo<25)?(byte)1:(byte)0;
        }
        return new(selected.OrderBy(b=>b.X).Select(b=>new Rect(b.X/w,b.Y/h,b.Width/w,b.Height/h)).ToArray(),signature);
    }
    internal static BitmapSource Crop(BitmapSource image,Rect[] regions)
    {
        if(regions.Length==0)return image;var box=CropBounds(regions);
        int x=(int)(box.X*image.PixelWidth),y=(int)(box.Y*image.PixelHeight);
        var crop=new CroppedBitmap(image,new Int32Rect(x,y,Math.Min(image.PixelWidth-x,(int)Math.Ceiling(box.Width*image.PixelWidth)),Math.Min(image.PixelHeight-y,(int)Math.Ceiling(box.Height*image.PixelHeight))));crop.Freeze();return crop;
    }
    internal static Rect CropBounds(Rect[] regions)
    {
        if(regions.Length==0)return new Rect(0,0,1,1);var detected=regions.Aggregate(Rect.Union);var box=detected;box.Inflate(.03,.06);box.Intersect(new Rect(0,0,1,1));
        // The cheap locator searches the central 90%. A long line on a card
        // near an edge may continue beyond that search area (e.g. faction ×0.59).
        if(detected.Right>.85)box.Width=1-box.X;
        if(detected.Left<.15){double right=box.Right;box.X=0;box.Width=right;}
        return box;
    }
}
