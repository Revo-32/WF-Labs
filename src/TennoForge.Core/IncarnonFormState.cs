namespace TennoForge.Core;

/// <summary>Transformation clock. Attack input time is distinct from contact time.</summary>
public sealed class IncarnonFormState
{
    public const string RefreshSource="https://www.warframe.com/en/patch-notes/pc/33-0-0";
    public IncarnonDefinition Definition {get;}
    public double Duration {get;}
    public int RequiredCombo {get;}
    public double End {get;private set;}=double.NegativeInfinity;
    public int HeavyKills {get;private set;}
    public int ResilienceStacks {get;private set;}
    public bool Active(double time)=>End>time+1e-9;
    public double Remaining(double time)=>Math.Max(0,End-time);
    public double Resilience(double time)=>Active(time)?1-Math.Pow(.9,ResilienceStacks):0;
    public static bool IsActivationInput(string kind)=>kind is "IncarnonHeavyInput" or "IncarnonSlamInput" or "IncarnonHeavySlamInput";
    public IncarnonFormState(Build build)
    {
        Definition=IncarnonEngine.ForWeapon(build.WeaponId)??throw new ArgumentException("인카논 무기가 아닙니다.");
        Duration=Definition.Code switch
        {
            "Innodem" or "Praedos" or "Ruvox" or "Thalys" or "Obex"=>90,
            "AckBrunt" or "Anku" or "Bo" or "CeramicDagger" or "Destreza" or "DualIchor" or "Furax" or "Hate" or "Magistar" or "NamiSolo" or "Okina" or "Sibear" or "Skana"=>180,
            _=>throw new CombatRuleUnavailableException("이 인카논의 변신 지속시간이 연결되지 않았습니다.")
        };
        RequiredCombo=Definition.Code is "Innodem" or "Praedos"?5:6;
        foreach(var perk in Definition.Perks.Where(p=>(build.Melee?.Evolutions??[]).Contains(p.Id)))
            if(perk.Effects.TryGetValue("TransformCombo",out var combo))RequiredCombo=(int)combo;
        if(build.Melee?.Incarnon==true)
        {
            var remaining=build.Melee.IncarnonSecondsRemaining??Duration;MeleeEngine.Range(remaining,.000001,Duration,"시작 인카논 잔여 시간");End=remaining;
            HeavyKills=(int)(build.Melee.Conditions?.GetValueOrDefault("IncarnonHeavyKills")??0);
            if(HeavyKills is <0 or >30||HeavyKills!=(build.Melee.Conditions?.GetValueOrDefault("IncarnonHeavyKills")??0))throw new ArgumentException("변신 강공격 처치는0~30 정수입니다.");
        }
        else if(build.Melee?.IncarnonSecondsRemaining is not null)throw new ArgumentException("변신하지 않은 시작 상태에는 인카논 잔여 시간을 입력할 수 없습니다.");
    }
    public bool Advance(double time)
    {
        if(End==double.NegativeInfinity||Active(time))return false;
        End=double.NegativeInfinity;HeavyKills=0;ResilienceStacks=0;return true;
    }
    public bool TryActivate(double time,int combo,string attack)
    {
        MeleeEngine.Range(time,0,1e9,"인카논 발동 시각");MeleeEngine.Range(combo,1,13,"인카논 발동 콤보");
        Advance(time);
        var eligible=attack is "Heavy Attack" or "Heavy Slam Attack"||attack=="Slam Attack"&&Definition.Code is "Destreza" or "Obex" or "Thalys";
        if(Active(time)||!eligible||combo<RequiredCombo)return false;
        End=time+Duration;return true;
    }
    public void DirectHeavyKill(double time){Advance(time);if(Active(time)&&Definition.Code=="Destreza")HeavyKills=Math.Min(30,HeavyKills+1);}
    public void Finisher(double time){Advance(time);if(Active(time)&&Definition.Code=="Innodem")ResilienceStacks=Math.Min(4,ResilienceStacks+1);}
    public void Death(){End=double.NegativeInfinity;HeavyKills=0;ResilienceStacks=0;}
}
