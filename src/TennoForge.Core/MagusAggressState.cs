namespace TennoForge.Core;

public sealed class MagusAggressState
{
    public const string Id="/Lotus/Upgrades/CosmeticEnhancers/Offensive/OperatorHeavyMeleeDamageOnTransference";
    public const string Source="https://wiki.warframe.com/w/Magus_Aggress?oldid=2813200";
    public int Charges {get;private set;}
    public double ActivatedAt {get;private set;}=double.NegativeInfinity;
    public double ReadyMinimum {get;private set;}
    public double ReadyMaximum {get;private set;}
    private double clock;
    private void Advance(double time){if(!double.IsFinite(time)||time<clock)throw new ArgumentException("마구스 어그레스 시각은 순서대로 입력하세요.");clock=time;}
    public bool Transfer(double time)
    {
        Advance(time);
        if(time<ReadyMinimum)return false;
        if(time<ReadyMaximum)throw new CombatRuleUnavailableException("마구스 어그레스20초 재사용 대기의 기산점(발동/마지막 소모)이 위키에 명시되지 않았습니다. 이 재발동 시점은 확정할 수 없습니다. "+Source);
        ActivatedAt=time;Charges=4;ReadyMinimum=time+20;ReadyMaximum=double.PositiveInfinity;return true;
    }
    public bool Consume(string weaponId,double time)
    {
        Advance(time);
        if(Charges==0)return false;
        var exalted=ExaltedRules.Find(weaponId);
        if(exalted is {Pseudo:false})return false;
        if(exalted is {Pseudo:true}&&time<ActivatedAt+5)return false;
        if(exalted is null&&!Eligible(weaponId))return false;
        Charges--;if(Charges==0)ReadyMaximum=time+20;return true;
    }
    public static bool Eligible(string weaponId)=>ExaltedRules.Find(weaponId) is null&&MeleeRuleCatalog.Load().Weapon(weaponId).Raw.Text("Class") is "Heavy Blade" or "Hammer";
    public static double CriticalDamage(Build b)
    {
        var slot=b.Arcanes?.Operator;
        if(slot is null)return 0;
        if(slot.Id!=Id)throw new ArgumentException("오퍼레이터 아케인 계산 규칙이 없습니다.");
        MeleeEngine.Range(slot.Rank,0,5,"마구스 어그레스 랭크");
        var charges=b.Melee?.Conditions?.GetValueOrDefault("MagusAggressCharges",4)??4;
        MeleeEngine.Range(charges,0,4,"마구스 어그레스 잔여 공격");
        if(charges!=Math.Truncate(charges))throw new ArgumentException("마구스 어그레스 잔여 공격 수는 정수입니다.");
        return slot.Active&&charges>0&&Eligible(b.WeaponId)? .5*(slot.Rank+1):0;
    }
}
