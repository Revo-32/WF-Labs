using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;
public record RangedSporePath(int Target,double? Flight,bool Headshot=false,bool Miss=false);
public record RangedBurrowTick(double Time,DamageType Type,double Output);
public static class RangedZymos
{
    public const string WeaponId="wiki:secondary/Zymos";
    public static bool Applies(ReferenceWeapon w)=>w.Id==WeaponId;
    public static JsonElement Normalize(ReferenceWeapon w,JsonElement attack,int index)
    {
        if(!Applies(w))return attack;var n=JsonNode.Parse(attack.GetRawText())!;
        if(index>=3)n["Multishot"]=1;if(index==3){n["StatusChance"]=0;n["ForcedProcs"]=new JsonArray();}
        return JsonSerializer.SerializeToElement(n);
    }
    public static void Validate(RangedConditions c)
    {
        if(c.ZymosSpores is {} paths&&(paths.Length!=6||paths.Any(p=>p.Target<0||p.Target>=c.GroupCount||p.Flight is {} flight&&(!double.IsFinite(flight)||flight<0||flight>300))))throw new ArgumentException("자이모스는 포자 6개의 대상·비행 시간 또는 빗나감을 지정해야 합니다.");
        if(!double.IsFinite(c.ZymosSporeDistance)||c.ZymosSporeDistance<0||c.ZymosSporeOrbs?.Any(v=>v<1||v>3)==true)throw new ArgumentException("포자 이동 거리·구체 중첩 오류");
        if(c.ZymosBurrowTicks?.Any(t=>!double.IsFinite(t.Time)||t.Time<0||t.Time>=1.1||!Enum.IsDefined(t.Type)||!double.IsFinite(t.Output)||t.Output<0)==true)throw new ArgumentException("굴착 피해 사건은 직격 후 0~1.1초 미만의 유효한 최종 출력이어야 합니다.");
    }
    public static void RequireHead(RangedConditions c)
    {
        Validate(c);
        if(c.ZymosBurrowTicks is null)throw new CombatRuleUnavailableException("자이모스 굴착 중 지속 피해의 타격 시간·속성·배율은 원문에 없습니다. 지정한 피해 사건 또는 굴착 피해 제외 시나리오를 선택하세요.");
        if(c.ZymosSpores is null||c.ZymosSpores.Any(p=>!p.Miss&&p.Flight is null))throw new CombatRuleUnavailableException("자이모스 포자의 비행 속도와 무작위 대상 가중치는 원문에 없습니다. 6개 포자의 대상·비행 시간 또는 빗나감을 지정하세요.");
        if(c.ZymosSporeExplosion is null)throw new CombatRuleUnavailableException("자이모스 위키에는 포자 폭발 설명과 폭발이 발생하지 않는 Bugs 설명이 함께 있습니다. 본문 또는 버그 시나리오를 선택하세요.");
    }
}
