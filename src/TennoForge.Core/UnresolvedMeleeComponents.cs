namespace TennoForge.Core;

public record SlamWavePattern(int Rows,int ExplosionsPerRow,double Spacing,double? Radius,double? BaseDamage,
    double? StatusChance,double? Falloff,bool HeavyCombo,bool ConditionOverload,bool ComboMods,string Source)
{
    public int ExplosionCount=>Rows*ExplosionsPerRow;
}
public record StaticDischargePattern(double SpinFirstTick,double OverheadFollowup,double OverheadFirstTick,double SpinFollowup,double TickInterval=.2,double Radius=4);

/// <summary>Verified portions of components whose indispensable damage stats are absent.
/// Null means absent source data; it is never interpreted as zero or copied from the blade.</summary>
public static class UnresolvedMeleeComponents
{
    public const string Exec="/Lotus/Weapons/Tenno/Melee/Swords/CrpBigSlash/CrpBigSlash";
    public const string Sampotes="/Lotus/Weapons/Tenno/Melee/Hammer/DaxDuviriHammer/DaxDuviriHammerWeapon";
    public const string Azothane="/Lotus/Weapons/Tenno/Melee/Swords/DaxDuviriTwoHandedKatana/DaxDuviriTwoHandedKatanaWeapon";
    public const string ExecSource="https://wiki.warframe.com/w/Tenet_Exec?oldid=2792713";
    public const string SampotesSource="https://wiki.warframe.com/w/Sampotes?oldid=2779643";
    public const string AzothaneSource="https://wiki.warframe.com/w/Azothane?oldid=2794721";
    public const string DischargeSource="https://wiki.warframe.com/w/Static_Discharge?oldid=2688723";
    public static SlamWavePattern Pattern(string id,bool heavy)=>id switch {
        Exec=>new(heavy?3:1,3,5,null,null,null,heavy?.5:.7,heavy,false,true,ExecSource),
        Sampotes=>new(heavy?3:1,heavy?4:8,5,heavy?7:5,null,.1,null,false,false,true,SampotesSource),
        _=>throw new ArgumentException("고유 슬램 파동 무기 오류")};
    // Rank table describes the augment's fraction; full mod/crit/CO brackets remain unspecified.
    public static StaticDischargePattern Discharge(int rank)
    {
        if(rank is <0 or >5)throw new ArgumentException("스태틱 디스차지 랭크는0~5입니다.");
        var fraction=.3+.1*rank;return new(.3*fraction,10*fraction,fraction,3*fraction);
    }
    public static double AzothaneHeat(Calculation calculation,bool arsenalHalf)
    {
        var slam=MeleeRuleCatalog.Load().Weapon(Azothane).Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")=="Slam Attack");
        var basis=arsenalHalf?.5*slam.GetProperty("Damage").EnumerateObject().Sum(d=>d.Value.GetDouble()):85;
        return basis*(1+calculation.Melee!.Effects.GetValueOrDefault("SlamDamage"));
    }
    public static SecondaryEffect[] Describe(Build build,Calculation calculation)
    {
        var result=new List<SecondaryEffect>();
        if(PathocystMaggots.Applies(build.WeaponId))result.Add(PathocystMaggots.Display(build));
        if(build.WeaponId is Exec or Sampotes)foreach(var heavy in new[]{false,true})
        {
            var p=Pattern(build.WeaponId,heavy);var name=build.WeaponId==Exec?"테넷 엑제크":"삼포테스";
            var rules=build.WeaponId==Exec
                ?"충격 + 리치 추가 원소 · 슬램 모드/버프 적용 · "+(heavy?"강공격/콤보 배수 적용":"명중 콤보 획득·콤보 획득 확률 모드 적용")+$" · 가장자리 {(1-p.Falloff!.Value):P0}\n높이가 달라지는 첫 폭발에서 경로 중단 · 지형 틈은 통과 · 미라지 분신은 파동을 복제하지 않음"
                :"기본 상확10% · 블러드 러시/위핑 운즈 적용 · 강공격 콤보 피해 배수/컨디션 오버로드 제외 · 띄우기/래그돌";
            result.Add(new(name+" · "+(heavy?"강슬램 충격파":"슬램 충격파"),$"{p.Rows}줄 × {p.ExplosionsPerRow}회 = {p.ExplosionCount}개 · 5m 간격",Radius:p.Radius??0,
                Note:rules+"\n미기재: 파동1개 기본 피해·치명타 수치·실제 폭발 시각"+(build.WeaponId==Exec?"·반경·기본 상확":"·반경 안 감쇠율")+". 슬램 직격 수치를 파동 피해로 대체하거나 전부 명중한다고 합산하지 않습니다. "+p.Source));
        }
        if(build.WeaponId==Azothane)foreach(var half in new[]{false,true})result.Add(new("아조테인 · 막기 충격파 · "+(half?"슬램50% 서술":"85 화염 서술"),"원문 상충 · 비치명 기본 피해 · 적마다 콤보 +4",AzothaneHeat(calculation,half),DamageType.Heat,10,
            Note:(half?"장비창 슬램 광역 피해 × 50%":"고정85 화염")+" × (1 + 슬램 피해) · 감쇠 없음 · 강제 충격\n일반 피해/원소/콤보/팩션을 임의로 더하지 않습니다. 두 서술을 더하지 않으며 기본 치명/상태 확률 미기재로 전체 DPS는 확정하지 않습니다. "+AzothaneSource,DamageLabel:"비치명 기본 피해"));
        if(calculation.Melee!.Effects.GetValueOrDefault("StaticDischarge")>0)
        {
            var slot=build.Slots.Where(s=>s is not null).Cast<ModSlot>().FirstOrDefault(s=>MeleeRuleCatalog.Load().Mods.Any(m=>m.Id==s.ModId&&m.Effects?.ContainsKey("StaticDischarge")==true));
            if(slot is not null)
            {
                var p=Discharge(slot.Rank);
                result.Add(new("스태틱 디스차지 · 회전으로 시작","준비 동작 시작: 1피해 전기 상태",Radius:p.Radius,Note:$"회전 중 {p.TickInterval:0.#}초마다 D × {p.SpinFirstTick:0.###} → 다음 내려찍기 D × {p.OverheadFollowup:0.###} 1회\n"+DischargeNote));
                result.Add(new("스태틱 디스차지 · 내려찍기로 시작","플레이어 중심 · 사거리 모드 미적용",Radius:p.Radius,Note:$"내려찍기 중 {p.TickInterval:0.#}초마다 D × {p.OverheadFirstTick:0.###} → 다음 회전 D × {p.SpinFollowup:0.###} 1회\n"+DischargeNote));
            }
        }
        return result.ToArray();
    }
    private const string DischargeNote="D는 원문의 weapon damage입니다. 모딩 피해/강공격과의 정확한 브라켓·치명/상확·애니메이션 시작/종료 시각이 미기재여서 검 피해나 전체 콤보 시간을 대입하지 않습니다. 낮은 랭크 계수는 랭크 표 비율로 환산한 조건부 값입니다. 전기 상태는 인플루언스를 발동할 수 있습니다. "+DischargeSource;
}
