namespace TennoForge.Desktop;

// Normalize a bounded card's display dimming, never its text/values. The game
// dims the previous roll; its neutral locked line can fall below the OCR mask.
internal static class RivenTone
{
    internal static (byte[] Pixels,double Gain) Normalize(byte[] pixels,int width,int height)
    {
        var histogram=new int[256];int count=0;
        bool Dark(int x,int y){int p=(y*width+x)*4;return Math.Max(pixels[p],Math.Max(pixels[p+1],pixels[p+2]))<105;}
        for(int y=Math.Max(3,(int)(height*.35));y<Math.Min(height-3,(int)(height*.90));y++)
        for(int x=Math.Max(3,(int)(width*.1));x<Math.Min(width-3,(int)(width*.9));x++)
        {
            int p=(y*width+x)*4,b=pixels[p],g=pixels[p+1],r=pixels[p+2];
            if(r<=42||r-g<=7||b-r<=5)continue;
            int dark=(Dark(x-3,y)?1:0)+(Dark(x+3,y)?1:0)+(Dark(x,y-3)?1:0)+(Dark(x,y+3)?1:0);
            if(dark<2)continue;histogram[r]++;count++;
        }
        if(count<50)return (pixels,1);
        int total=0,level=255;for(int i=0;i<256;i++){total+=histogram[i];if(total>=count*.85){level=i;break;}}
        if(level>=110)return (pixels,1);
        double gain=Math.Clamp(170d/Math.Max(1,level),1,3);var result=pixels.ToArray();
        for(int p=0;p<result.Length;p+=4)for(int c=0;c<3;c++)result[p+c]=(byte)Math.Min(255,Math.Round(result[p+c]*gain));
        return (result,gain);
    }
}
