namespace TennoForge.Core;

public record VitricaGlassContact(double Time,int Target=1);
public record VitricaSlamContact(double Time,int Target=1,bool Heavy=false,int Slam=1);

public sealed class VitricaGlassState
{
    public const string WeaponId="/Lotus/Weapons/Tenno/Melee/Swords/NWIIIOrokinSword/NWOrokinSword";
    public const string Scope="유리 폭발 피해 · 기본 슬램과 지상 파동 제외";
    private readonly Dictionary<int,double> expiry=[];
    public bool Active(int target,double time)=>expiry.GetValueOrDefault(target,double.NegativeInfinity)>time;
    public bool Apply(int target,double time,bool immune=false)
    {
        if(immune)return false;expiry[target]=time+7;return true;
    }
    public bool Remove(int target)=>expiry.Remove(target);
    public int[] Expire(double time){var targets=expiry.Where(e=>e.Value<=time+1e-9).Select(e=>e.Key).ToArray();foreach(var target in targets)expiry.Remove(target);return targets;}
    public double NextExpiry=>expiry.Values.DefaultIfEmpty(double.PositiveInfinity).Min();
    public static void Validate(VitricaGlassContact[] glass,VitricaSlamContact[] slams,int targets)
    {
        if(glass.Length+slams.Length>512)throw new ArgumentException("비트리카 접촉은 최대512회입니다.");
        foreach(var g in glass){MeleeEngine.Range(g.Time,0,300,"유리화 접촉 시각");MeleeEngine.Range(g.Target,1,targets,"유리화 중심 대상");}
        foreach(var s in slams){MeleeEngine.Range(s.Time,0,300,"슬램 접촉 시각");MeleeEngine.Range(s.Target,1,targets,"슬램 대상");MeleeEngine.Range(s.Slam,1,10000,"슬램 번호");}
        foreach(var group in slams.GroupBy(s=>s.Slam))
        {
            if(group.Any(s=>s.Time!=group.First().Time||s.Heavy!=group.First().Heavy))throw new ArgumentException("동일 슬램의 직접 접촉은 같은 시각·강공격 여부를 사용합니다. 지상 파동 비행과 구분하세요.");
            if(group.Select(s=>s.Target).Distinct().Count()!=group.Count())throw new ArgumentException("동일 슬램에서 한 대상의 유리는 한 번만 깨집니다.");
        }
    }
}
