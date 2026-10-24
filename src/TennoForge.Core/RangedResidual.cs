namespace TennoForge.Core;
public record ResidualScenario(double? OutputMultiplier=null,double? TickInterval=null,double? FirstTick=null,double[]? Activations=null,double? BoilsApproachRadius=null,double? BoilsFalloff=null,bool? ViremiaCombines=null,bool? WeaponCredit=null,bool FixedPacketScenario=false,double[]? OtherKitgunPoolEnds=null,double? StatusTick=null);
public record ResidualPulse(double Time,double Scale);
public static class RangedResidual
{
    public static bool Is(string? id)=>id is "ResidualBoils" or "ResidualMalodor" or "ResidualShock" or "ResidualViremia";
    public static DamageType Type(string id)=>id switch{"ResidualBoils"=>DamageType.Heat,"ResidualMalodor"=>DamageType.Cold,"ResidualShock"=>DamageType.Electricity,"ResidualViremia"=>DamageType.Toxin,_=>throw new ArgumentException("레지듀얼 종류 오류")};
    public static double Base(string id)=>id switch{"ResidualBoils"=>80,"ResidualShock"=>200,_=>40};
    public static double Radius(string id)=>id is "ResidualBoils" or "ResidualShock"?10:9;
    public static void Validate(ResidualScenario? c)
    {
        if(c is null)return;
        if(new[]{c.OutputMultiplier,c.FirstTick,c.BoilsApproachRadius,c.StatusTick}.Any(v=>v is {} x&&(!double.IsFinite(x)||x<0))||c.TickInterval is {} tick&&(!double.IsFinite(tick)||tick<.001||tick>30)||c.BoilsFalloff is {} fall&&(!double.IsFinite(fall)||fall<0||fall>1)||c.Activations is {} events&&(events.Length>10000||events.Any(x=>!double.IsFinite(x)||x<0||x>12))||c.OtherKitgunPoolEnds is {} ends&&(ends.Length>2||ends.Any(x=>!double.IsFinite(x)||x<0||x>12)))throw new ArgumentException("레지듀얼 시나리오 시간·배율·다른 키트건의 장판 수 오류");
    }
    public static ResidualPulse[] Pulses(string id,int rank,ResidualScenario c)
    {
        Validate(c);var duration=3*(rank+1);
        if(id is "ResidualBoils" or "ResidualShock")return(c.Activations??throw new CombatRuleUnavailableException("하이브 접근/전기 방출의 횟수·시점은 원문에 없습니다. 생성 후 발동 시점을 지정하세요. 빈 목록은 접근·방출 없음 시나리오입니다.")).Where(t=>t<duration).Select(t=>new ResidualPulse(t,1)).ToArray();
        var interval=c.TickInterval??throw new CombatRuleUnavailableException("레지듀얼의 초당 40 피해는 확인되지만 틱 주기는 없습니다. 틱 간격을 지정하세요.");var first=c.FirstTick??throw new CombatRuleUnavailableException("레지듀얼의 첫 틱 시나리오를 지정하세요.");if(first>interval)throw new ArgumentException("첫 틱은 주기 이내여야 합니다.");
        var output=new List<ResidualPulse>();for(double t=first;t<duration-1e-9||first>0&&Math.Abs(t-duration)<1e-9;t+=interval)output.Add(new(t,interval));return output.ToArray();
    }
    public static Dictionary<DamageType,double> Packet(RangedBuild b,string id,double scale,double distance)
    {
        var c=b.Conditions?.Residual??throw new CombatRuleUnavailableException("레지듀얼의 미기재 배율·발동 시나리오를 지정하세요.");Validate(c);
        if(!c.FixedPacketScenario||c.OutputMultiplier is null)throw new CombatRuleUnavailableException("레지듀얼의 피해 모드·치명 브라켓은 원문에 없습니다. 비치명 시나리오와 최종 배율을 지정하세요.");
        if(distance>Radius(id))return [];
        var basis=Base(id)*scale;var packet=new Dictionary<DamageType,double>();var elements=new List<(DamageType Type,double Value)>();var innate=Type(id);
        var combines=id=="ResidualMalodor"||id=="ResidualViremia"&&(c.ViremiaCombines??throw new CombatRuleUnavailableException("바이레미아의 무기 원소 결합 여부가 원문에 없습니다. 원소 처리 시나리오를 지정하세요."));
        if(combines)
        {
            foreach(var slot in b.Slots.Append(b.Exilus).OfType<ModSlot>())
            {
                var mod=RangedEngine.ModCatalog.Mods.Single(m=>m.Id==slot.ModId);if(RangedRivens.IsRiven(mod))mod=RangedRivens.Resolve(mod,b.Riven,b.WeaponId,slot.Rank);
                foreach(var entry in mod.Effects)if(Enum.TryParse<DamageType>(entry.Key,out var type)&&type is >=DamageType.Heat and <=DamageType.Corrosive)
                {
                    var value=(mod.RankEffects?.TryGetValue(entry.Key,out var ranks)==true?ranks[slot.Rank]:entry.Value*(slot.Rank+1)/(mod.MaxRank+1))*basis;
                    if(type is DamageType.Heat or DamageType.Cold or DamageType.Electricity or DamageType.Toxin)elements.Add((type,value));else packet[type]=packet.GetValueOrDefault(type)+value;
                }
            }
            elements.Add((innate,basis));var grouped=elements.GroupBy(e=>e.Type).Select(g=>(Type:g.Key,Value:g.Sum(e=>e.Value))).ToArray();
            for(int i=0;i<grouped.Length;i+=2){var type=i+1<grouped.Length?DamageEngine.Combine(grouped[i].Type,grouped[i+1].Type):grouped[i].Type;packet[type]=packet.GetValueOrDefault(type)+grouped[i].Value+(i+1<grouped.Length?grouped[i+1].Value:0);}
        }
        else packet[innate]=basis;
        var multiplier=c.OutputMultiplier.Value;
        if(id=="ResidualBoils")multiplier*=1-(c.BoilsFalloff??throw new CombatRuleUnavailableException("보일스 감쇠는 위키에 약 90%로 기재됩니다. 사용할 끝점 감쇠 시나리오를 지정하세요."))*distance/10;
        return packet.ToDictionary(p=>p.Key,p=>p.Value*multiplier);
    }
}
public sealed class RangedResidualPools
{
    private readonly List<(int Id,double End)> pools=[];private int sequence;
    public int Count(double time){pools.RemoveAll(p=>p.End<=time+1e-9);return pools.Count;}
    public void AddExternal(double end){if(pools.Count>=2)throw new ArgumentException("다른 키트건 장판도 합쳐 최대 2개입니다.");pools.Add((++sequence,end));}
    public int? TrySpawn(double time,int rank,double random)
    {if(rank is <0 or >3)throw new ArgumentException("레지듀얼 랭크 오류");if(Count(time)>=2||random>=.2)return null;var id=++sequence;pools.Add((id,time+3*(rank+1)));return id;}
}
