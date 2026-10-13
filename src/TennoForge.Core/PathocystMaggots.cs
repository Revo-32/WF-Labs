namespace TennoForge.Core;

public record MaggotComponent(double Lifetime,double? Damage,double? Radius,double? CriticalChance,double? StatusChance,int? Level,string Source);
public static class PathocystMaggots
{
    public const string Normal="/Lotus/Weapons/Infested/Melee/InfBoomerang/InfBoomerangWeapon";
    public const string Coda="/Lotus/Weapons/Infested/InfestedLich/Melee/CodaPathocyst/CodaPathocyst";
    public static bool Applies(string id)=>id is Normal or Coda;
    public static string Source(string id)=>id==Normal?"https://wiki.warframe.com/w/Pathocyst?oldid=2804892":"https://wiki.warframe.com/w/Coda_Pathocyst?oldid=2804895";
    public static string Scope(string id)=>"미반영: "+(id==Coda?"코다 패소시스트 마이어스마이트":"패소시스트 마고트")+"의 생성 확률·피해·폭발 반경은 위키 미기재입니다. 현재 무기 직접/상태 피해에 소환체 피해와 경계/수면 해제 효과를 포함하지 않습니다. "+Source(id);
    public static MaggotComponent Describe(Build build)
    {
        if(!Applies(build.WeaponId))throw new ArgumentException("패소시스트 소환체 무기가 아닙니다.");
        return new(7,null,null,build.WeaponId==Normal?0:null,build.WeaponId==Normal?0:null,build.WeaponId==Normal?build.Capacity?.WeaponRank:null,Source(build.WeaponId));
    }
    public static SecondaryEffect Display(Build build)
    {
        var m=Describe(build);return new(build.WeaponId==Normal?"패소시스트 · 마고트":"코다 패소시스트 · 마이어스마이트","비행 중/무기 명중에서 생성 · 붙은 적 비틀거림",
            Note:$"수명{m.Lifetime:0}초 후 폭발 · 생성 확률·기본 피해·반경 미기재\n"+(build.WeaponId==Normal?$"순수 바이러스 · 치확0%·상확0% · 소환 레벨 = 무기 랭크{m.Level}\n콤보 배수·팩션·원소·물리 피해 모드 제외 · 니더스/스트레인 세트와 상호작용 없음\n":"일반형보다 강화된 소환체/넓은 반경 · 수치가 없어 일반형 피해/확률을 그대로 복사하지 않습니다.\n")+"주변 적을 경계시키며 수면 해제 시각은 알려지지 않았습니다. "+m.Source);
    }
}
