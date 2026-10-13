using System.Text.Json;

namespace TennoForge.Core;

public enum EpidemicPhase { Wave, Explosion }
/// <summary>Explicit confirmed contact, not an inferred trajectory or animation timestamp.</summary>
public record EpidemicImpact(double Time,EpidemicPhase Phase=EpidemicPhase.Wave,int Target=1);
public record EpidemicResult(Calculation Wave,Calculation Explosion,double SuspensionSeconds,string[] Notes);

public static class ExodiaEpidemicMechanic
{
    public const string Source="https://wiki.warframe.com/w/Exodia_Epidemic?oldid=2723781";
    public static string Label(EpidemicPhase phase)=>phase==EpidemicPhase.Wave?"에피데믹 파동":"에피데믹 끝 폭발";
    public static EpidemicResult Calculate(Catalog catalog,Build build)=>new(Evaluate(catalog,build,EpidemicPhase.Wave),Evaluate(catalog,build,EpidemicPhase.Explosion),build.Melee!.Exodia!.Rank+1,
        ["불릿/더블 점프 직후 슬램으로 발동 · 파동10 폭발 피해 · 끝 폭발은 위키의 근삿값인 무기 피해 약30%를 사용합니다.",
         "파동: 컨오×범용 피해·고정 추가 치확/치피/상확 허용. 끝 폭발: 일반 피해+범용 피해·컨오 제외·원소 모드 제외.",
         "파동 폭 약5m·이동 약31m·끝 폭발 반경 약7m. 이동 속도와 정확한 경계는 원문 미기재이므로 접촉 시각과 명중 대상을 별도로 지정합니다.",
         "부유는 별도의 군중 제어이며 컨오 상태로 세지 않고, 작섬·어플릭션즈를 발동하지 않습니다.",Source]);
    public static void Validate(EpidemicImpact impact,int targets)
    {
        MeleeEngine.Range(impact.Time,0,300,"에피데믹 명중 시각");MeleeEngine.Range(impact.Target,1,targets,"에피데믹 명중 대상");
        if(!Enum.IsDefined(impact.Phase))throw new ArgumentException("에피데믹 성분 오류");
    }
    internal static Calculation Evaluate(Catalog catalog,Build build,EpidemicPhase phase,bool useInitialComboFloor=true)
    {
        if(!ZawCatalog.IsZaw(build.WeaponId)||build.Melee?.Exodia is not {Active:true} slot||MeleeRuleCatalog.Load().Arcanes.Single(a=>a.Id==slot.Id).Code!="Epidemic")throw new ArgumentException("에피데믹을 장착·활성화한 조우가 필요합니다.");
        if(!Enum.IsDefined(phase))throw new ArgumentException("에피데믹 성분 오류");
        var wave=phase==EpidemicPhase.Wave;var weapon=catalog.Weapons.Single(w=>w.Id==build.WeaponId);var name="Exodia Epidemic "+phase;
        var raw=JsonSerializer.SerializeToElement(new Dictionary<string,object>
        {
            ["AttackName"]=name,["Damage"]=new Dictionary<string,double>{{wave?"Blast":"Viral",wave?10:weapon.Damage.Values.Sum()*.3}},
            ["CritChance"]=wave?0:weapon.CriticalChance,["CritMultiplier"]=wave?1:weapon.CriticalMultiplier,
            ["StatusChance"]=wave?0:weapon.StatusChance,["ShotType"]="AoE"
        });
        var policy=new AttackRule(name,DamageSource:wave?"Universal":"Combined",Co:wave?1:0,CoMultiplicative:wave,Tennokai:false,EvolutionDamage:false,EvolutionStats:false,
            StatusMods:!wave,CriticalMods:!wave,Elements:false,Ips:false,Code:"Epidemic",Stealth:false,FollowThrough:false,HitCombo:false);
        var input=build with {Stance=null,Scenario=build.Scenario with {MotionMultiplier=1},Melee=build.Melee with {Attack=name,Finisher=null,Tennokai=false,Distance=0,TargetIndex=1}};
        var result=MeleeEngine.CalculateAttack(catalog,input,useInitialComboFloor:useInitialComboFloor,component:new(raw,policy,NoSupplementalElements:true,FlatCriticalOnly:wave));
        // The article names flat CC/CD/status and CO/universal damage, but does
        // not establish faction/final-damage modifiers for the 10-damage wave.
        if(wave&&(result.Melee!.FactionMultiplier!=1||result.Melee.Effects.GetValueOrDefault("FinalDamage")!=0||build.Melee.Buffs?.Eclipse>0))
            throw new CombatRuleUnavailableException("에피데믹 파동의 팩션·별도 최종 피해 배율 적용은 원문에서 확인되지 않았습니다. 기본 파동·컨오·범용 피해·고정 추가 수치와 구분합니다.");
        return result with {Limitations=result.Limitations.Append(wave?"파동의 부유는 컨오/어플릭션즈 상태가 아닙니다.":"끝 폭발의 기본 배율 약30%는 위키 근삿값입니다. 정확한 실측값으로 표시하지 않습니다.").Append(Source).ToArray()};
    }
}
