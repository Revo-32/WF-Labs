namespace TennoForge.Core;

public record ExaltedAugmentDefinition(string Id,ExaltedFamily Family,string Name,string Source);

/// <summary>Warframe augment slots are separate from the eight weapon mods.</summary>
public static class ExaltedAugments
{
    public const string Chromatic="/Lotus/Powersuits/Excalibur/SwordOfDoomAugmentCard";
    public const string Reactive="/Lotus/Powersuits/Pacifist/PacifistFistAugmentCard";
    public const string Enraged="/Lotus/Powersuits/Berserker/LastStandAugmentTwoCard";
    public const string Primal="/Lotus/Powersuits/MonkeyKing/MonkeyStaffAugmentCard";
    public const string Rising="/Lotus/Powersuits/Ninja/NinjaStormAugmentCard";
    public const string Accumulating="/Lotus/Powersuits/Khora/KhoraCrackAugmentCard";
    public const string Rubble="/Lotus/Powersuits/Brawler/BrawlerPassiveAugmentCard";
    public static readonly ExaltedAugmentDefinition[] All=[
        new(Chromatic,ExaltedFamily.Blade,"크로마틱 블레이드","https://wiki.warframe.com/w/Chromatic_Blade?oldid=2817132"),
        new(Reactive,ExaltedFamily.Wind,"리액티브 스톰","https://wiki.warframe.com/w/Reactive_Storm?oldid=2795617"),
        new(Enraged,ExaltedFamily.Talons,"인레이지드","https://wiki.warframe.com/w/Enraged?oldid=2729819"),
        new(Primal,ExaltedFamily.Staff,"프라이멀 레이지","https://wiki.warframe.com/w/Primal_Rage?oldid=2729832"),
        new(Rising,ExaltedFamily.Clones,"라이징 스톰","https://wiki.warframe.com/w/Rising_Storm?oldid=2729730"),
        new(Accumulating,ExaltedFamily.Whipclaw,"어큐뮬레이팅 윕클로","https://wiki.warframe.com/w/Accumulating_Whipclaw?oldid=2726160"),
        new(Rubble,ExaltedFamily.Landslide,"러블 힙","https://wiki.warframe.com/w/Rubble_Heap?oldid=2809731")];
    public static ExaltedAugmentDefinition? Find(MeleeSetup setup)=>setup.AbilityAugment is {} slot?All.SingleOrDefault(a=>a.Id==slot.ModId):null;
    public static void Validate(ExaltedDefinition weapon,MeleeSetup setup)
    {
        MeleeEngine.Range(setup.AbilityDuration,.01,100,"어빌리티 지속 배율");
        if(setup.AbilityAugment is not {} slot)return;
        var d=Find(setup)??throw new ArgumentException("익절티드 개조 모드 식별자 오류");
        if(d.Family!=weapon.Family)throw new ArgumentException("선택한 어빌리티에 맞는 개조 모드가 아닙니다.");
        MeleeEngine.Range(slot.Rank,0,3,"어빌리티 개조 랭크");
        if(slot.ModId==Chromatic&&setup.ChromaticElement is not (DamageType.Heat or DamageType.Cold or DamageType.Electricity or DamageType.Toxin))throw new ArgumentException("크로마틱 블레이드의 발광 원소를 선택하세요.");
    }
    internal static void Apply(MeleeSetup setup,Action<string,double> add,List<string> notes)
    {
        if(setup.AbilityAugment is not {} slot)return;
        var rank=slot.Rank;double C(string key)=>setup.Conditions?.GetValueOrDefault(key)??0;
        switch(slot.ModId)
        {
            case Chromatic:add("StatusChance",new[]{1d,2,2.5,3}[rank]*setup.AbilityStrength);break;
            case Reactive:add("StatusChance",new[]{1d,1.5,2,2.5}[rank]*setup.AbilityStrength);break;
            case Enraged:add("Damage",2+.5*rank);add("PassiveCriticalChance",2+.5*rank);break;
            case Primal:MeleeEngine.Range(C("PrimalRage"),0,150,"프라이멀 레이지");add("PassiveCriticalChance",C("PrimalRage")/100);break;
            case Rising:add("ComboDuration",10);add("RisingStormCombo",(rank+1)*setup.AbilityStrength);break;
            case Accumulating:MeleeEngine.Range(C("AccumulatingWhipclaw"),0,350,"윕클로 누적 피해");break;
        }
        notes.Add(Find(setup)!.Name+" · 워프레임 개조 모드 · 무기 슬롯/수용량과 별도. "+Find(setup)!.Source);
    }
    // The official 38.5.5 change excludes physical types and combination
    // with modded basic elements. The converted Impact stays a separate pool.
    public static DamageType AdaptiveType(string faction)=>faction switch
    {
        "Infested"=>DamageType.Heat,"Anarchs"=>DamageType.Electricity,"Narmer"=>DamageType.Toxin,
        "Grineer" or "KuvaGrineer" or "Zariman" or "Scaldra"=>DamageType.Corrosive,
        "Corpus" or "CorpusAmalgam" or "Techrot"=>DamageType.Magnetic,"Orokin"=>DamageType.Viral,
        "Sentient" or "TheMurmur"=>DamageType.Radiation,"InfestedDeimos"=>DamageType.Gas,
        "Neutral"=>DamageType.Impact,_=>throw new ArgumentException("리액티브 스톰 약점 팩션 오류")
    };
}

public sealed class DelayedDecayBonus
{
    private readonly double cap,decay,grace;
    private double clock,lastGain;
    public double Value {get;private set;}
    public DelayedDecayBonus(double cap,double decay,double grace,double start=0,double? graceRemaining=null)
    {
        MeleeEngine.Range(start,0,cap,"시작 개조 보너스");MeleeEngine.Range(graceRemaining??grace,0,grace,"개조 감소까지 남은 시간");
        this.cap=cap;this.decay=decay;this.grace=grace;Value=start;lastGain=(graceRemaining??grace)-grace;
    }
    public void Advance(double time)
    {if(!double.IsFinite(time)||time<clock-1e-9)throw new ArgumentException("개조 보너스 시간 오류");Value=Math.Max(0,Value-decay*Math.Max(0,time-Math.Max(clock,lastGain+grace)));clock=Math.Max(clock,time);}
    public void Gain(double time,double value){MeleeEngine.Range(value,0,1e5,"개조 획득량");Advance(time);Value=Math.Min(cap,Value+value);lastGain=time;}
    public void Reset(double time){Advance(time);Value=0;}
}
