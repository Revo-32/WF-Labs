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
    internal static ModFinish ForId(string id)=>Finishes.Value.GetValueOrDefault(id,ModFinish.Neutral);
    internal static string Label(ModFinish finish)=>finish switch{ModFinish.Common=>"일반",ModFinish.Uncommon=>"고급",ModFinish.Rare=>"레어",ModFinish.Legendary=>"레전더리",ModFinish.Galvanized=>"갈바나이즈드",ModFinish.Amalgam=>"아말감",ModFinish.Umbra=>"움브라",ModFinish.Riven=>"리벤",_=>"등급 정보 없음"};
    internal static string BackgroundKey(ModFinish finish)=>$"Mod{finish}Background";
    internal static string StrokeKey(ModFinish finish)=>$"Mod{finish}Stroke";
    internal static void ApplyTheme(bool dark)
    {
        foreach(var finish in Enum.GetValues<ModFinish>())
        {
            var (top,bottom,accent)=finish switch
            {
                ModFinish.Common=>("#463322","#211B16","#C59165"),
                ModFinish.Uncommon=>("#383C3D","#1C2021","#CCD1D1"),
                ModFinish.Rare=>("#49402A","#242015","#E1C777"),
                ModFinish.Legendary=>("#3E4347","#202428","#F0E9DA"),
                ModFinish.Galvanized=>("#343B3E","#1B2023","#D4DEE1"),
                ModFinish.Amalgam=>("#3F343D","#24252E","#C7AD8E"),
                ModFinish.Umbra=>("#2E2D2A","#171A1D","#C6B075"),
                ModFinish.Riven=>("#49305D","#25182F","#C797EC"),
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
                // Metallic perimeter and inset corners recall the game's collapsed mod cards.
                // Keep the text field quiet; ornaments never cross the central name area.
                var rim=new LinearGradientBrush();rim.StartPoint=new(0,0);rim.EndPoint=new(1,1);
                rim.GradientStops.Add(new(Mix(accentColor,Colors.Black,.25),0));rim.GradientStops.Add(new(Mix(accentColor,Colors.White,.28),.25));rim.GradientStops.Add(new(accentColor,.48));rim.GradientStops.Add(new(Mix(accentColor,Colors.Black,.52),.74));rim.GradientStops.Add(new(accentColor,1));rim.Freeze();
                var outer=Geometry.Parse("M 8,2 L 152,2 L 158,8 L 158,96 L 152,102 L 8,102 L 2,96 L 2,8 Z");
                dc.DrawGeometry(null,new Pen(rim,3),outer);
                var etched=new SolidColorBrush(Color.FromArgb(110,accentColor.R,accentColor.G,accentColor.B));etched.Freeze();
                var inset=Geometry.Parse("M 11,7 L 149,7 L 153,11 L 153,93 L 149,97 L 11,97 L 7,93 L 7,11 Z");dc.DrawGeometry(null,new Pen(etched,.7),inset);
                var ornament=new Pen(rim,1.4);
                foreach(var x in new[]{14d,146d}){var sign=x<80?1:-1;dc.DrawLine(ornament,new(x,3),new(x+sign*8,10));dc.DrawLine(ornament,new(x,101),new(x+sign*8,94));}
                if(finish is ModFinish.Legendary or ModFinish.Umbra)
                {
                    var gold=new SolidColorBrush(dark?C("#D9BC73"):C("#8C6D28"));
                    dc.DrawGeometry(null,new Pen(gold,1.6),Geometry.Parse("M 54,3 L 62,9 L 98,9 L 106,3 M 54,101 L 62,95 L 98,95 L 106,101"));
                }
                else if(finish==ModFinish.Galvanized)
                {
                    dc.DrawGeometry(null,new Pen(rim,2),Geometry.Parse("M 8,10 L 14,7 L 26,7 M 152,10 L 146,7 L 134,7 M 8,94 L 14,97 L 26,97 M 152,94 L 146,97 L 134,97"));
                }
                else if(finish==ModFinish.Riven)
                {
                    dc.DrawGeometry(null,new Pen(rim,1.5),Geometry.Parse("M 64,3 L 72,9 L 80,3 L 88,9 L 96,3 M 64,101 L 72,95 L 80,101 L 88,95 L 96,101"));
                }
                else if(finish==ModFinish.Amalgam)
                    dc.DrawGeometry(null,new Pen(rim,1.6),Geometry.Parse("M 8,40 L 13,48 L 8,56 M 152,40 L 147,48 L 152,56"));
            }
            drawing.Freeze();var fill=new DrawingBrush(drawing){Stretch=Stretch.Fill};fill.Freeze();
            Application.Current.Resources[BackgroundKey(finish)]=fill;
            var stroke=new SolidColorBrush(Mix(accentColor,dark?Colors.Black:Colors.White,dark?.50:.40));stroke.Freeze();Application.Current.Resources[StrokeKey(finish)]=stroke;
        }
    }
}
