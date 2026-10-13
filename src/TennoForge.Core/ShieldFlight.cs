using System.Text.Json;

namespace TennoForge.Core;

public record ShieldFlight(double Launch,double Returned,double? Impact=null,int Target=1,int Throw=1,double? Travel=null,GlaiveDetonation? Detonation=null,double? FinisherWindowSeconds=null,int Hologram=0);

/// <summary>Explicit Sigma shield contacts and manual recall, with the infobox's separate payloads.</summary>
public static class ShieldFlightRules
{
    public static void Validate(ShieldFlight[] flights,string id,int targets)
    {
        if(flights.Length==0)return;
        if(id!=ShieldThrowMechanic.Sigma)throw new ArgumentException("이 방패 비행은 시그마 & 옥탄티스 전용입니다.");
        if(flights.Length>64||flights.Select(f=>f.Throw).Distinct().Count()!=flights.Length)throw new ArgumentException("방패 투척 번호는 중복 없이 최대64개입니다.");
        foreach(var f in flights)
        {
            MeleeEngine.Range(f.Hologram,0,4,"방패 공격 주체");
            MeleeEngine.Range(f.Throw,1,10000,"방패 투척 번호");MeleeEngine.Range(f.Launch,0,300,"방패 발사");MeleeEngine.Range(f.Returned,f.Launch,300,"방패 회수 완료");
            if(f.Returned==f.Launch)throw new ArgumentException("회수 완료는 발사보다 뒤여야 합니다.");
            MeleeEngine.Range(f.Target,1,targets,"방패 대상");
            if(f.Impact is {} t){MeleeEngine.Range(t,f.Launch,f.Returned,"방패 명중");if(t==f.Returned)throw new ArgumentException("명중은 회수 완료 전이어야 합니다.");}
            if(f.FinisherWindowSeconds is {} window)MeleeEngine.Range(window,.001,300,"방패 마무리 노출 시간");
            if(f.Travel is {} travel)MeleeEngine.Range(travel,0,10000,"방패 비행 거리");
            if(f.Detonation is {} d)
            {
                MeleeEngine.Range(d.Time,f.Launch,f.Returned,"회수 폭발");if(d.Time==f.Returned)throw new ArgumentException("기폭은 회수 완료 전이어야 합니다.");
                if(f.Impact is {} hit&&hit>=d.Time)throw new ArgumentException("직격은 수동 기폭보다 먼저 지정하세요. 기폭 이후 귀환 명중의 규칙은 원문에 없습니다.");
                if(d.Position is {} pos)foreach(var n in new[]{pos.X,pos.Y,pos.Z})MeleeEngine.Range(n,-10000,10000,"방패 기폭 위치");
                else MeleeEngine.Range(d.CenterTarget,1,targets,"방패 기폭 중심");
            }
        }
        foreach(var actor in flights.GroupBy(f=>f.Hologram)){var ordered=actor.OrderBy(f=>f.Launch).ToArray();for(var i=1;i<ordered.Length;i++)if(ordered[i].Launch<ordered[i-1].Returned)throw new ArgumentException("같은 공격 주체의 이전 방패 회수 후 다음 투척을 발사하세요.");}
    }
    public static Build WithCharges(Build current,Build input)
    {
        var conditions=new Dictionary<string,double>(current.Melee!.Conditions??[]){["BlockCharges"]=input.Melee!.Conditions?.GetValueOrDefault("BlockCharges")??0,["Airborne"]=1};
        return current with {Melee=current.Melee with {Conditions=conditions}};
    }
    public static Calculation Calculate(Catalog catalog,Build b,bool explosion=false,double distance=0)
    {
        var name=explosion?"Shield Throw Explosion":"Shield Throw";
        return MeleeEngine.CalculateAttack(catalog,b with {Stance=null,Scenario=b.Scenario with {MotionMultiplier=1},Melee=b.Melee! with {Attack=name,Distance=distance,TargetIndex=1,Tennokai=false,Finisher=null}},useInitialComboFloor:false);
    }
    public static void CheckTravel(Build b,ShieldFlight f,Calculation c)
    {
        if(f.Travel is not {} travel)return;
        var speed=60*(1+c.Melee!.Effects.GetValueOrDefault("ProjectileSpeed"));
        var first=f.Impact??f.Detonation?.Time??f.Returned;
        if(speed<=0||first+1e-9<f.Launch+travel/speed)throw new ArgumentException("방패 접촉이60m/s로 정한 최단 비행보다 빠릅니다.");
        // Reuse the same range conflict handling as static calculations.
        if(!Calculate(Catalog.LoadMelee(),b,distance:travel).Melee!.HitConnects)throw new ArgumentException("방패 비행 거리가 최대 투척 사거리를 초과합니다.");
    }
    internal static string Signature(Catalog catalog,Build b,bool explosion)
    {
        var m=b.Melee!;var neutral=b with {Scenario=b.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},Arcanes=(b.Arcanes??new()) with {Frozen=false},Melee=m with {Faction="Neutral",Statuses=[],Debuffs=[],Shields=0,Overguard=0,HeatArmorStrip=0,Conditions=(m.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary()}};
        var c=Calculate(catalog,neutral,explosion);return JsonSerializer.Serialize(new {c.Damage,c.CriticalChance,c.CriticalMultiplier,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
    }
}
