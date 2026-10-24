using System.Text.RegularExpressions;
using TennoForge.Core;

namespace TennoForge.Desktop;
internal static class KoreanPresentation
{
    internal static string WeaponClass(string value)=>value switch{
        "Assault Saw"=>"돌격용 톱","Bayonet"=>"총검","Blade and Whip"=>"도검과 채찍","Claws"=>"클로","Dagger"=>"단검","Dual Daggers"=>"쌍수 단검","Dual Nikanas"=>"쌍수 니카나","Dual Swords"=>"쌍수 도검","Fist"=>"피스트","Glaive"=>"글레이브","Gunblade"=>"건블레이드","Hammer"=>"해머","Heavy Blade"=>"중도검","Heavy Scythe"=>"대낫","Machete"=>"마체테","Nikana"=>"니카나","Nunchaku"=>"쌍절곤","Polearm"=>"폴암","Rapier"=>"레이피어","Scythe"=>"낫","Sparring"=>"스파링","Staff"=>"스태프","Sword"=>"도검","Sword and Shield"=>"도검과 방패","Tonfa"=>"톤파","Two-Handed Nikana"=>"양손 니카나","Warfan"=>"전투부채","Whip"=>"채찍",
        "Rifle"=>"소총","Shotgun"=>"산탄총","Pistol"=>"권총","Bow"=>"활","Sniper Rifle"=>"저격총","Dual Pistols"=>"쌍권총","Dual Shotguns"=>"쌍산탄총","Launcher"=>"발사기","Tome"=>"책","Thrown"=>"투척 무기",_=>value};
    private sealed class ClassConverter:System.Windows.Data.IValueConverter
    {
        public object Convert(object value,Type type,object parameter,System.Globalization.CultureInfo culture)=>WeaponClass(value?.ToString()??"");
        public object ConvertBack(object value,Type type,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
    }
    internal static System.Windows.DataTemplate ClassTemplate()
    {
        var text=new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
        text.SetBinding(System.Windows.Controls.TextBlock.TextProperty,new System.Windows.Data.Binding{Converter=new ClassConverter()});
        return new System.Windows.DataTemplate{VisualTree=text};
    }
    private static readonly Lazy<Dictionary<string,ReferenceMod>> Mods=new(()=>ReferenceCatalog.Load().Mods.GroupBy(m=>m.Id).ToDictionary(g=>g.Key,g=>g.First()));
    internal static string ModDescription(RangedMod mod,int rank)
    {
        if(RangedRivens.IsRiven(mod))return "내 리벤의 옵션과 수치를 입력하세요.";
        if(Mods.Value.TryGetValue(mod.Id,out var reference)&&rank>=0&&rank<reference.RankText.Length)
            return string.Join("\n",reference.RankText[rank].Distinct());
        return "공식 한국어 설명 없음 · 자료 탭에서 원문 확인";
    }
    internal static string ArcaneDescription(RangedArcaneRule? rule,int rank)
    {
        if(rule is null)return "장착 안 함";
        var item=ReferenceCatalog.Load().Arcanes.FirstOrDefault(a=>a.Id==rule.Id);
        return item is not null&&rank>=0&&rank<item.RankText.Length?item.Describe(rank):"공식 한국어 설명 없음 · 자료 탭에서 원문 확인";
    }
    // UI names for Wiki attack components. These are presentation translations,
    // not official item names; original IDs and calculation inputs stay intact.
    private static readonly (string From,string To)[] Terms=[
        ("Area of Effect","범위 피해"),("Tendril","촉수"),("Bomblet","자탄"),
        ("Napalm Grenades","네이팜 그레네이즈"),("Nightwatch Napalm","나이트워치 네이팜"),("Wild Frenzy","와일드 프렌지"),("Pax Seeker","팩스 시커"),("Returning Blades","귀환 칼날"),("Bursting Mass","버스팅 매스"),("Vile Discharge","바일 디스차지"),("Stored","저장된 피해"),("Sac","주머니"),
        ("Concentrated Arrow","컨센트레이티드 애로우"),("Incarnon Form","인카논"),("Reload From Empty","빈 탄창 재장전"),("Partial Reload","부분 재장전"),
        ("Damage over Time","지속 피해"),("Fully Stacked Orb Singularity","최대 중첩 구체 특이점"),("Fully Spooled","최대 연사"),("Fully Charged","완전 충전"),("Partially Charged","부분 충전"),
        ("Cannon Mode","포격 모드"),("Barrage Mode","산탄 모드"),("Semi-Auto","반자동"),("Full Auto","자동"),("Full-Auto","자동"),("Alt-Fire","보조 발사"),("Alt Fire","보조 발사"),("Alternate Fire","보조 발사"),("Secondary Fire","보조 발사"),("Primary-Fire","일반 사격"),
        ("Burst-Fire","점사"),("Lock-On","유도"),("Mid-Flight","비행 중"),("Horizontal/Vertical","수평/수직"),("shot by player","사격으로 기폭"),("While Aiming","조준 중"),
        ("Cluster Bombs","자탄"),("Cluster Bomb","자탄"),("Plasma Bomb","플라즈마 폭탄"),("Radial Attack","범위 공격"),("Direct Hit","직접 명중"),("Single Pellet","펠릿 1개"),
        ("Air Burst","공중 기폭"),("Headshot","헤드샷"),("Uncharged","비충전"),("Charged","충전"),("Projectile","투사체"),("Explosion","폭발"),("Detonation","기폭"),("Contact","접촉"),("Impact","명중"),
        ("Charge","충전"),("Shot","사격"),("Attack","공격"),("Normal","일반"),("Base","기본"),("Primary","일반 사격"),("Quick","즉시"),("Perfect","완벽 충전"),("Active","활성"),
        ("Auto","자동"),("Semi","반자동"),("Burst","점사"),("Mode","모드"),("Held","지속 사격"),("Beam","빔"),("AoE","범위 피해"),("Damage","피해"),("DoT","지속 피해"),
        ("Orb","구체"),("Launch","발사"),("Merging","병합"),("Blob","점액"),("Embed","부착"),("Embedded","부착"),("Bubble","구체"),("Collapse","붕괴"),
        ("Bounce","도탄"),("Grenade","유탄"),("Rocket","로켓"),("Single","단일"),("Arrow","화살"),("Dart","다트"),("Buckshot","산탄"),("Slug","슬러그"),
        ("Homing","유도"),("Spore","포자"),("Infested","인페스티드"),("Harpoon","작살"),("Spear","창"),("Throw","투척"),("Glaive","글레이브"),
        ("Cold","냉기"),("Cryo","냉기"),("Ice","냉기"),("Electric","전기"),("Fire","화염"),("Poison","독성"),("Toxin","독성"),("Corrosive","부식"),("Viral","바이러스"),("Magnetic","자성"),("Gas","가스"),
        ("Cloud","구름"),("Wave","파동"),("Quill","가시"),("Cube","큐브"),("Glass","유리"),("Initial","최초"),("Blast","폭발"),("Attraction Field","흡인장"),("Appendages","촉수"),("Beacon","비콘"),("Turret Expiry","터렛 종료"),("Zoom","조준"),("Direct","직접"),("Heat","화염"),("Incarnon","인카논"),("Tick","지속 피해")
    ];
    internal static string Attack(string text)
    {
        foreach(var (from,to) in Terms)text=Regex.Replace(text,@"\b"+Regex.Escape(from)+@"\b",to,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        return text;
    }
}
