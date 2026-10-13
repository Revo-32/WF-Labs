namespace TennoForge.Core;

public record ArcaneSlot(string Id,int Rank,bool Active=true);
public record ArcaneSetup(ArcaneSlot? Melee=null,ArcaneSlot? Warframe1=null,ArcaneSlot? Warframe2=null,int ExposureStacks=0,double Shields=0,double Overshields=0,bool Frozen=false,int FinisherKills=0,double? MaximumShields=null,double MaximumOvershields=1200,bool ShieldRechargeEnabled=true,double ShieldRechargeBonus=0,double ShieldRechargeDelayReduction=0,double ShieldRechargeDelayRemaining=0,double? PlayerArmor=null,ArcaneSlot? Operator=null);
public record ArcaneBonuses(double Damage,double Speed,double FlatCriticalChance,double Corrosive,double DuplicateChance,double DoughtyPerStep,int InitialCombo,double FrozenMultiplier,string[] Notes);

public static class ArcaneEngine
{
    public const string RulesVersion="wiki-arcane-snapshot-0.5.0";
    public static ArcaneBonuses Resolve(ArcaneSetup? setup)
    {
        if(setup is null)return new(0,0,0,0,0,0,0,1,[]);
        if(setup.ExposureStacks is <0 or >24 || setup.FinisherKills is <0 or >10000 ||
            !double.IsFinite(setup.Shields)||setup.Shields is <0 or >1000000||!double.IsFinite(setup.Overshields)||setup.Overshields is <0 or >1000000)
            throw new ArgumentException("아케인 중첩·실드 조건 범위가 올바르지 않습니다.");
        var catalog=ReferenceCatalog.Load();var ids=new HashSet<string>();var notes=new List<string>();
        double damage=0,speed=0,cc=0,corrosive=0,duplicate=0,doughty=0,frozen=1;var initial=0;
        foreach(var (slot,category) in new[]{(setup.Melee,"Melee"),(setup.Warframe1,"Warframe"),(setup.Warframe2,"Warframe")})
        {
            if(slot is null)continue;
            if(!ids.Add(slot.Id))throw new ArgumentException("동일 아케인을 중복 장착할 수 없습니다.");
            var a=catalog.Arcanes.SingleOrDefault(a=>a.Id==slot.Id)??throw new ArgumentException("아케인 식별자가 없습니다.");
            if(a.Category!=category)throw new ArgumentException($"{a.Name}: {category} 슬롯에 장착할 수 없습니다.");
            if(slot.Rank<0||slot.Rank>a.MaxRank)throw new ArgumentException($"{a.Name}: 랭크 범위 오류");
            if(a.Rule=="Unsupported")throw new ArgumentException($"{a.Name}: 자료는 등록되어 있으나 해당 효과의 계산은 아직 지원하지 않습니다. 효과를 제외한 DPS로 표시하지 않습니다.");
            if(!slot.Active){notes.Add(a.Name+": 미발동 상태");continue;}
            var level=slot.Rank+1;
            switch(a.Rule)
            {
                case "Fury":damage+=.3*level;break;
                case "Strike":speed+=.1*level;break;
                case "Avenger":cc+=.075*level;break;
                case "BladeCharger":damage+=.5*level;break;
                case "Exposure":corrosive+=Math.Min(2.4,.1*level*setup.ExposureStacks);break;
                case "Retaliation":damage+=Math.Min(4.2,.05*level*(Math.Floor(setup.Shields/200)+.5*Math.Floor(setup.Overshields/200)));break;
                case "Doughty":doughty=.5+.1*slot.Rank;break;
                case "Duplicate":duplicate=.25+.15*slot.Rank;break;
                case "ComboSetup":initial=level*setup.FinisherKills;notes.Add(a.Name+": 입력한 마무리 처치 횟수의 초기 콤보만 반영, 전투 중 처치는 증가시키지 않음");break;
                case "Utility":notes.Add(a.Name+": 방어력/적 끌어당기기 효과는 단일 대상 직접 피해를 변경하지 않음");break;
                case "HeavyOnly":notes.Add(a.Name+": 강공격 전용 효과, 현재 일반 스탠스 타격에는 피해 보너스 없음");break;
                case "Careen":if(setup.Frozen)frozen=1+.25*level;break;
                default:throw new ArgumentException(a.Name+": 알 수 없는 아케인 계산 규칙");
            }
            notes.Add(a.Name+$" · 랭크 {slot.Rank} · 입력한 발동 상태 고정");
        }
        return new(damage,speed,cc,corrosive,duplicate,doughty,initial,frozen,notes.ToArray());
    }
    public static double YellowChance(double chance)=>Math.Clamp(1-Math.Abs(chance-1),0,1);
    public static double ExpectedMultiplier(double cc,double cd,double duplicate)=> (1+cc*(cd-1))*(1+YellowChance(cc)*duplicate);
    public static double ExpectedMultiplier(Calculation c)=>ExpectedMultiplier(c.CriticalChance,c.CriticalMultiplier,c.DuplicateChance);
    public static double DoughtyBonus(Dictionary<DamageType,double> damage,double status,double perStep)
    {
        var total=damage.Values.Sum();if(total<=0||perStep==0)return 0;
        // Wiki example uses status in percent; the engine stores probabilities as fractions.
        return Math.Min(50,Math.Round(damage.GetValueOrDefault(DamageType.Puncture)/total*status/.1*perStep,1,MidpointRounding.AwayFromZero));
    }
}
