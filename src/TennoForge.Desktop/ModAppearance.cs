using System.Windows;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal enum ModFinish { Neutral,Common,Uncommon,Rare,Legendary,Galvanized,Amalgam,Umbra,Riven }
internal static class ModAppearance
{
    // Metadata comes from the existing pinned Wiki snapshot, joined by internal ID.
    private static readonly Lazy<Dictionary<string,ModFinish>> Finishes=new(()=>ReferenceCatalog.Load().Mods.GroupBy(m=>m.Id).ToDictionary(g=>g.Key,g=>
    {
        // Wiki aliases can share an internal ID. Only use a finish if those records agree.
        var finishes=g.Select(Classify).Distinct().ToArray();
        return finishes.Length==1?finishes[0]:ModFinish.Neutral;
    }));
    private static ModFinish Classify(ReferenceMod m)
    {
        string Field(string key)=>m.Raw.TryGetProperty(key,out var value)&&value.ValueKind==System.Text.Json.JsonValueKind.String?value.GetString()??"":"";
        return Field("Class") switch
        {
            "Riven"=>ModFinish.Riven,"Galvanized"=>ModFinish.Galvanized,"Amalgam"=>ModFinish.Amalgam,
            _ when Field("Polarity")=="Umbra"=>ModFinish.Umbra,
            _=>Field("Rarity") switch {"Common"=>ModFinish.Common,"Uncommon"=>ModFinish.Uncommon,"Rare"=>ModFinish.Rare,"Legendary"=>ModFinish.Legendary,_=>ModFinish.Neutral}
        };
    }
    internal static ModFinish For(Mod mod)=>Finishes.Value.GetValueOrDefault(mod.Id,ModFinish.Neutral);
    internal static string Label(ModFinish finish)=>finish switch{ModFinish.Common=>"일반",ModFinish.Uncommon=>"고급",ModFinish.Rare=>"레어",ModFinish.Legendary=>"레전더리",ModFinish.Galvanized=>"갈바나이즈드",ModFinish.Amalgam=>"아말감",ModFinish.Umbra=>"움브라",ModFinish.Riven=>"리벤",_=>"등급 정보 없음"};
    internal static string BackgroundKey(ModFinish finish)=>$"Mod{finish}Background";
    internal static string StrokeKey(ModFinish finish)=>$"Mod{finish}Stroke";
    internal static void ApplyTheme(bool dark)
    {
        foreach(var finish in Enum.GetValues<ModFinish>())
        {
            var (top,bottom,accent)=finish switch
            {
                ModFinish.Common=>("#493223","#241D18","#CA946A"),
                ModFinish.Uncommon=>("#35404A","#1C232A","#ADBCC9"),
                ModFinish.Rare=>("#493E24","#272116","#D8B864"),
                ModFinish.Legendary=>("#454A50","#23292F","#E3DDC9"),
                ModFinish.Galvanized=>("#293C46","#192229","#B7D0DA"),
                ModFinish.Amalgam=>("#3F343D","#24252E","#C7AD8E"),
                ModFinish.Umbra=>("#2E2D2A","#171A1D","#C6B075"),
                ModFinish.Riven=>("#493058","#281B34","#C59BE2"),
                _=>("#25282D","#191C20","#656B74")
            };
            Color C(string hex)=>(Color)ColorConverter.ConvertFromString(hex);
            var accentColor=C(accent);var topColor=C(top);var bottomColor=C(bottom);
            Color Mix(Color a,Color b,double t)=>Color.FromRgb((byte)(a.R+(b.R-a.R)*t),(byte)(a.G+(b.G-a.G)*t),(byte)(a.B+(b.B-a.B)*t));
            if(!dark){topColor=Mix(accentColor,Colors.White,.80);bottomColor=Mix(accentColor,Colors.White,.96);accentColor=Mix(accentColor,Colors.Black,.35);}
            var gradient=new LinearGradientBrush(topColor,bottomColor,65);gradient.Freeze();
            var drawing=new DrawingGroup();
            using(var dc=drawing.Open())
            {
                dc.DrawRectangle(gradient,null,new Rect(0,0,160,104));
                var glint=new SolidColorBrush(Color.FromArgb(dark?(byte)150:(byte)120,accentColor.R,accentColor.G,accentColor.B));glint.Freeze();
                dc.DrawLine(new Pen(glint,1.2),new Point(8,1),new Point(152,1));
                var etch=new SolidColorBrush(Color.FromArgb(dark?(byte)35:(byte)45,accentColor.R,accentColor.G,accentColor.B));etch.Freeze();
                var pen=new Pen(etch,1);
                if(finish==ModFinish.Riven)
                {dc.DrawLine(pen,new(100,4),new(156,60));dc.DrawLine(pen,new(112,4),new(156,48));dc.DrawLine(pen,new(120,100),new(156,64));}
                else if(finish is ModFinish.Galvanized or ModFinish.Umbra)
                {dc.DrawLine(pen,new(140,100),new(156,84));dc.DrawLine(pen,new(126,100),new(152,74));dc.DrawLine(pen,new(156,84),new(156,68));}
                else if(finish==ModFinish.Legendary)
                {dc.DrawLine(pen,new(98,100),new(142,100));dc.DrawLine(pen,new(106,96),new(150,96));}
                else if(finish==ModFinish.Amalgam)
                {dc.DrawLine(pen,new(80,104),new(160,24));dc.DrawLine(pen,new(90,104),new(160,34));}
            }
            drawing.Freeze();var fill=new DrawingBrush(drawing){Stretch=Stretch.Fill};fill.Freeze();
            Application.Current.Resources[BackgroundKey(finish)]=fill;
            var stroke=new SolidColorBrush(Mix(accentColor,dark?Colors.Black:Colors.White,dark?.50:.40));stroke.Freeze();Application.Current.Resources[StrokeKey(finish)]=stroke;
        }
    }
}
