namespace TennoForge.Core;

public record ObexFinisherWave(int Contact,double RawDamage,double TargetDamage,double? StealthRawDamage,double? StealthTargetDamage);
public record ObexFinisherArea(ObexFinisherWave[] Waves,double Radius,string[] Notes)
{
    public double RawDamage=>Waves.Sum(w=>w.RawDamage);
    public double TargetDamage=>Waves.Sum(w=>w.TargetDamage);
}

/// <summary>The finisher's damage is distributed to nearby enemies, not added twice to its victim.</summary>
public static class ObexFinisherMechanic
{
    public const string Source="https://wiki.warframe.com/w/Obex_Incarnon_Genesis?oldid=2803254";
    public static bool Applies(Build build)=>build.Melee?.Incarnon==true&&IncarnonEngine.ForWeapon(build.WeaponId)?.Code=="Obex";
    public static ObexFinisherArea Calculate(Catalog catalog,Build build,StanceCatalog stances,double distance)
    {
        if(!Applies(build)||build.Stance is null)throw new ArgumentException("인카논 오벡스의 마무리 동작을 선택하세요.");
        MeleeEngine.Range(distance,0,10000,"마무리 대상으로부터 주변 적 거리");
        var input=build with {Melee=build.Melee! with {Distance=0,TargetIndex=1}};
        var finisher=StanceEngine.Calculate(catalog,input,stances);
        if(finisher.Combo.FinisherKind is null)throw new ArgumentException("앞·뒤·은신·지상 마무리 동작이 필요합니다.");
        var profile=MeleeRuleCatalog.Load().Weapon(build.WeaponId);var raw=profile.Raw.GetProperty("Attacks")[0];
        var waves=new List<ObexFinisherWave>();
        foreach(var h in finisher.Hits)
        {
            var source=h.Input!;var policy=MeleeEngine.AttackPolicy(build.WeaponId,source.Melee!.Attack);
            var noStealth=MeleeEngine.CalculateAttack(catalog,source,h.SourceHit,h.CoFactor,finisher.Combo.FinisherKind=="Ground",component:new(raw,policy with {Stealth=false}));
            var possibleStealth=h.Calculation!;
            for(var n=0;n<h.Count;n++)waves.Add(new(waves.Count+1,distance<=12?noStealth.ExpectedHit:0,distance<=12?noStealth.TargetDps*noStealth.HitInterval:0,
                possibleStealth.Melee!.StealthBonus>0?distance<=12?possibleStealth.ExpectedHit:0:null,
                possibleStealth.Melee.StealthBonus>0?distance<=12?possibleStealth.TargetDps*possibleStealth.HitInterval:0:null));
        }
        return new(waves.ToArray(),12,[
            "마무리 접촉마다 12m·거리 감쇠 없는 방사형 피해. 현재 설정과 같은 방어층·상태인 주변 대상1명 기준입니다.",
            "원래 마무리 대상의 피해를 다시 더하지 않습니다. 마무리의 각 접촉은 별도 파동이며 전체/개별 시각이 없으면 피해만 제공합니다.",
            "방사형 피해의 은신 보너스는 위키가 잠정적으로 기술하므로 미적용 결과와 적용 가능 결과를 구분합니다.",
            "파라존 파동은 호스트일 때만 발동하는 알려진 버그가 있습니다. 파라존 파동의 기초 피해는 원문 미기재이므로 일반 무기 마무리 배수로 대체하지 않습니다.",Source]);
    }
}
