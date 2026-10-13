using System.Text.Json;

namespace TennoForge.Core;

public record IncarnonBladeContact(double Launch,double Impact,int Projectile=1,int Target=1,string Motion="Aerial",int LaunchHit=1,bool Surface=false,double? Travel=null,double? BladeImpact=null,int BladeTarget=1);

/// <summary>Hate / Ack & Brunt payloads, separate from the launching weapon contact.</summary>
public static class IncarnonBladeFlight
{
    public const string Hate="/Lotus/Weapons/Tenno/Melee/Scythe/StalkerScytheWeapon";
    public const string Ack="/Lotus/Weapons/Grineer/Melee/GrineerTylAxeAndBoar/RegorAxeShield";
    public const string HateSource="https://wiki.warframe.com/w/Hate_Incarnon_Genesis?oldid=2797300";
    public const string AckSource="https://wiki.warframe.com/w/Ack_%26_Brunt_Incarnon_Genesis?oldid=2787344";
    public static bool Applies(string id)=>id is Hate or Ack;
    public static string Attack(string id,bool explosion=false)=>(id==Hate?"Spectral Blade":id==Ack?"Spectral Axe":throw new ArgumentException("헤이트 또는 액크 & 브런트가 필요합니다."))+(explosion?" Explosion":"");
    public static string Source(string id)=>id==Hate?HateSource:AckSource;
    public static double Delay(string id)=>id==Hate?.4:0;
    public static void Validate(IncarnonBladeContact[] flights,string id,int targets)
    {
        if(flights.Length==0)return;if(!Applies(id))throw new ArgumentException("유령 칼날 비행은 헤이트 또는 액크 & 브런트에서 사용합니다.");
        if(flights.Length>256||flights.Select(f=>f.Projectile).Distinct().Count()!=flights.Length)throw new ArgumentException("유령 칼날 번호는 중복 없이 최대256개입니다.");
        foreach(var f in flights)
        {
            MeleeEngine.Range(f.Launch,0,300,"유령 칼날 발사");MeleeEngine.Range(f.Impact,f.Launch,300,"유령 칼날 명중");MeleeEngine.Range(f.Projectile,1,10000,"투사체 번호");MeleeEngine.Range(f.Target,1,targets,"대상 번호");MeleeEngine.Range(f.LaunchHit,1,256,"발사 타격 번호");
            if(id==Hate?f.Motion is not ("Neutral" or "Forward"):f.Motion is not ("Aerial" or "Slide"))throw new ArgumentException("헤이트는 정지/전진, 액크 & 브런트는 공중/슬라이드에서 발사합니다.");
            if(f.Travel is {} travel){MeleeEngine.Range(travel,0,10000,"유령 칼날 비행 거리");if(f.Impact+1e-9<f.Launch+travel/60)throw new ArgumentException("유령 칼날 명중이60m/s로 정한 최단 비행보다 빠릅니다.");}
            if(f.BladeImpact is {} bladeTime)
            {
                if(id!=Ack)throw new ArgumentException("같은 입력의 칼날 접촉은 액크 & 브런트 공중/슬라이드에서 지정합니다.");
                MeleeEngine.Range(bladeTime,0,300,"실제 칼날 명중 시각");MeleeEngine.Range(f.BladeTarget,1,targets,"칼날 대상 번호");
                if(Math.Abs(bladeTime-f.Impact)<1e-9)throw new ArgumentException("칼날과 투사체 명중 순서를 구분할 수 있도록 서로 다른 시각을 지정하세요.");
            }
        }
    }
    public static StanceHitDamage PhysicalContact(Catalog catalog,StanceCatalog stances,Build b,IncarnonBladeContact f)
    {
        if(b.WeaponId!=Ack)throw new ArgumentException("액크 & 브런트의 칼날 접촉이 필요합니다.");
        var w=catalog.Weapons.Single(w=>w.Id==b.WeaponId);var stanceId=b.Capacity is {StanceEquipped:true}?b.Capacity.StanceId:b.Stance?.Id;
        var available=stances.ForWeapon(w);var selected=stanceId is null?available.Single(s=>s.IsDefault):available.Single(s=>s.Id==stanceId);
        var input=b with {Stance=new(selected.Id,f.Motion,stances.Revision)};
        var hits=StanceEngine.Calculate(catalog,input,stances).Hits.Where(h=>h.SourceHit!.AttackType is "" or "360").SelectMany(h=>Enumerable.Repeat(h,h.Count)).ToArray();
        if(f.LaunchHit>hits.Length)throw new ArgumentException("칼날 타격 순서가 선택한 동작의 타수를 초과합니다.");
        return hits[f.LaunchHit-1];
    }
    public static double MotionMultiplier(Catalog catalog,StanceCatalog stances,Build b,IncarnonBladeContact f)
    {
        if(b.WeaponId==Ack)return 1; // Engine's SpectralAxe policy applies the aerial x2.
        var w=catalog.Weapons.Single(w=>w.Id==b.WeaponId);var stanceId=b.Capacity is {StanceEquipped:true}?b.Capacity.StanceId:b.Stance?.Id;
        var available=stances.ForWeapon(w);var stance=stanceId is null?available.Single(s=>s.IsDefault):available.Single(s=>s.Id==stanceId);
        var combo=stance.Combos.Single(c=>c.Key==f.Motion);
        var hits=combo.Hits.Where(h=>h.Multiplier>0&&h.AttackType!="Slam").SelectMany(h=>Enumerable.Repeat(h,h.Count)).ToArray();
        if(f.LaunchHit>hits.Length)throw new ArgumentException("발사 타격 번호가 선택한 스탠스 동작의 타수를 초과합니다.");
        return hits[f.LaunchHit-1].Multiplier;
    }
    public static Calculation Calculate(Catalog catalog,Build b,IncarnonBladeContact f,bool explosion=false,double distance=0,double motionMultiplier=1,bool useInitialComboFloor=true)
    {
        if(!Applies(b.WeaponId)||b.Melee?.Incarnon!=true)throw new ArgumentException("헤이트 또는 액크 & 브런트 인카논 변신이 필요합니다.");
        var name=Attack(b.WeaponId,explosion);var raw=MeleeRuleCatalog.Load().Weapon(b.WeaponId).Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")==name);
        var conditions=new Dictionary<string,double>(b.Melee.Conditions??[]);if(b.WeaponId==Ack)conditions["Airborne"]=f.Motion=="Aerial"?1:0;
        var input=b with {Stance=null,Scenario=b.Scenario with {MotionMultiplier=b.WeaponId==Ack?1:motionMultiplier},Melee=b.Melee with {Attack=name,Conditions=conditions,Finisher=null,Tennokai=false,Distance=distance,TargetIndex=1}};
        var policy=MeleeEngine.AttackPolicy(b.WeaponId,name) with {FollowThrough=false,Tennokai=false};
        return MeleeEngine.CalculateAttack(catalog,input,useInitialComboFloor:useInitialComboFloor,component:new(raw,policy,NoEnergyChannel:true));
    }
    internal static string Signature(Catalog catalog,Build b,IncarnonBladeContact f,bool explosion,double motionMultiplier)
    {
        var m=b.Melee!;var neutral=b with {Scenario=b.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},Arcanes=(b.Arcanes??new()) with {Frozen=false},Melee=m with {Faction="Neutral",Statuses=[],Debuffs=[],Shields=0,Overguard=0,HeatArmorStrip=0,Conditions=(m.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary()}};
        var c=Calculate(catalog,neutral,f,explosion,motionMultiplier:motionMultiplier,useInitialComboFloor:false);return JsonSerializer.Serialize(new {c.Damage,c.ExpectedHit,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
    }
    internal static Build WithResources(Build current,Build source,bool preserveForm=true)
    {
        var c=new Dictionary<string,double>(current.Melee!.Conditions??[]);
        if(source.WeaponId==Ack)foreach(var element in new[]{"Heat","Cold","Electricity","Toxin"})c["Block"+element]=source.Melee!.Conditions?.GetValueOrDefault("Block"+element)??0;
        return current with {Melee=current.Melee with {Incarnon=preserveForm?source.Melee!.Incarnon:current.Melee.Incarnon,Conditions=c}};
    }
}
