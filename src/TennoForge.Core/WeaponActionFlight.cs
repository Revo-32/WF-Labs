using System.Text.Json;
using System.Text.Json.Nodes;

namespace TennoForge.Core;

public record WeaponActionContact(double Time,int Target=1,int Projectile=1,double Travel=0,bool Blade=false,int BladeHit=1,bool Surface=false,bool Explosion=false,double PenetratedMeters=0,double? Launched=null,int SlamHit=1);
public record WeaponActionInput(double Start,double Launch,int Sequence=1,int Input=1,WeaponActionContact[]? Contacts=null,string ExplosionModel="Unresolved",bool IndependentDaggers=false,double? ProjectileExpires=null,bool Sliding=false,string Mode="Special",int Hologram=0);
public record WeaponActionEvent(double Time,int Kind,WeaponActionInput Input,WeaponActionContact? Contact=null);

/// <summary>One explicit input owns its blade/projectile contacts and heavy resources.
/// No animation, aim, hit time, or return time is invented by this schedule.</summary>
public static class WeaponActionFlight
{
    public static bool Applies(string id)=>WeaponStanceMechanic.Applies(id)||WarfanSlam.Applies(id)||id==VinquibusState.WeaponId;
    public static string Attack(string id,int sequence,bool explosion=false)=>id switch {
        VinquibusState.WeaponId=>explosion?"Gunshot Explosion":"Gunshot",
        WeaponStanceMechanic.Argo=>explosion?"Heavy Attack Glaive AoE":"Heavy Attack Glaive",
        WeaponStanceMechanic.Edun=>explosion?"Polearm Explosion":"Polearm Throw",
        WeaponStanceMechanic.Syam=>sequence==1?"First Shockwave":"Second Shockwave",
        WeaponStanceMechanic.Quassus or WeaponStanceMechanic.QuassusPrime=>sequence==1?"First Heavy Attack - Ethereal Daggers":"Second Heavy Attack - Ethereal Daggers",
        WeaponStanceMechanic.Verdilac=>explosion?"Energy Wave Explosion":"Energy Wave",
        WeaponStanceMechanic.Korumm=>"Tactical Combo Slam Attack",
        WeaponStanceMechanic.Arum=>sequence==1?"First Heavy Attack - Toxic Spines":"Second Heavy Attack - Toxic Spines",
        WeaponStanceMechanic.Nepheri=>"Fireballs",
        WeaponStanceMechanic.Agendus or WeaponStanceMechanic.Grigori=>"Energy Disk",
        _=>throw new ArgumentException("고유 공격 비행 무기 오류")};
    public static int Sequences(string id)=>id==VinquibusState.WeaponId?2:id==WeaponStanceMechanic.Verdilac?3:id is WeaponStanceMechanic.Syam or WeaponStanceMechanic.Nepheri or WeaponStanceMechanic.Agendus or WeaponStanceMechanic.Grigori||WeaponStanceMechanic.IsWarfan(id)?2:1;
    public static double Motion(string id,int sequence,bool explosion=false)=>id switch {WeaponStanceMechanic.Verdilac when !explosion=>sequence switch {1=>4,2=>2,_=>5},_=>1};
    public static bool Heavy(string id,WeaponActionInput input)=>WarfanSlam.Active(input)?input.Mode=="HeavySlam":Heavy(id);
    public static string Attack(string id,WeaponActionInput input,WeaponActionContact? contact,bool explosion=false)=>WarfanSlam.Active(input)?WarfanSlam.Attack(input,contact?.SlamHit??1):contact?.Blade==true?(Heavy(id)?"Heavy Attack":"Normal Attack"):Attack(id,input.Sequence,explosion);
    public static bool Heavy(string id)=>id is not (VinquibusState.WeaponId or WeaponStanceMechanic.Verdilac or WeaponStanceMechanic.Korumm or WeaponStanceMechanic.Nepheri);
    public static int ProjectileCount(string id,int sequence)=>id switch {WeaponStanceMechanic.Quassus=>sequence==1?12:6,WeaponStanceMechanic.QuassusPrime or WeaponStanceMechanic.Arum=>sequence==1?18:9,WeaponStanceMechanic.Nepheri=>4,_=>1};
    public static double PunchThrough(string id)=>id==WeaponStanceMechanic.Arum?1.3:WeaponStanceMechanic.IsQuassus(id)?2:10000;
    public static bool AutomaticExplosion(string id)=>id is WeaponStanceMechanic.Argo or WeaponStanceMechanic.Edun;
    public static double Radius(string id,string model)=>id switch {WeaponStanceMechanic.Argo=>3,WeaponStanceMechanic.Edun=>model=="Article"?5:4,WeaponStanceMechanic.Verdilac=>2,WeaponStanceMechanic.Korumm=>10,_=>0};
    public static WeaponActionEvent[] Schedule(string id,WeaponActionInput[] inputs,int targets,MeleeExternalEquipment? equipment=null)
    {
        if(inputs.Length==0)return [];
        if(!Applies(id)||inputs.Length>64||inputs.Select(v=>v.Input).Distinct().Count()!=inputs.Length)throw new ArgumentException("무기 고유 입력은 호환 무기에서 중복 없이64개까지 지정하세요.");
        var events=new List<WeaponActionEvent>();
        foreach(var v in inputs)
        {
            MeleeEngine.Range(v.Input,1,10000,"고유 공격 입력 번호");MeleeEngine.Range(v.Start,0,300,"공격 입력");MeleeEngine.Range(v.Launch,v.Start,300,"투사체 발사");MeleeEngine.Range(v.Sequence,1,Sequences(id),"공격 순서");
            if(id==VinquibusState.WeaponId){events.AddRange(VinquibusFlight.Schedule(v,targets));continue;}
            if(WarfanSlam.Active(v)){events.AddRange(WarfanSlam.Schedule(id,v,targets));continue;}
            if(WeaponStanceMechanic.IsQuassus(id))events.Add(new(v.Launch,3,v));
            if(v.Mode!="Special"||!WeaponStanceMechanic.Applies(id))throw new ArgumentException("이 무기에서 지원하는 고유 공격 또는 슬램 동작을 선택하세요.");
            var contacts=v.Contacts??[];if(contacts.Length>128)throw new ArgumentException("공격 입력당 접촉은128개까지입니다.");
            if(id==WeaponStanceMechanic.Edun&&contacts.Any(c=>!c.Blade)&&v.ExplosionModel is not ("Article" or "Module"))throw new CombatRuleUnavailableException("에둔 폭발:본문1초·5m와 무기 데이터0.8초·4m가 상충합니다. 사용할 출처 분기를 명시하세요. "+WeaponStanceMechanic.Source(id));
            foreach(var c in contacts)
            {
                if(c.Launched is {} launch)MeleeEngine.Range(launch,v.Launch,300,"개별 투사체 발사");
                MeleeEngine.Range(c.Time,c.Blade?v.Start:c.Launched??v.Launch,300,"접촉 시각");MeleeEngine.Range(c.Target,1,targets,"접촉 대상");MeleeEngine.Range(c.Projectile,1,Math.Ceiling(ProjectileCount(id,v.Sequence)*(WeaponStanceMechanic.IsQuassus(id)&&equipment?.Metronome is {} metronome?1+MetronomeRules.Opera(metronome with {Opera=true,OperaRemaining=null}):1)),"투사체 번호");MeleeEngine.Range(c.Travel,0,1000,"비행 거리");
                MeleeEngine.Range(c.PenetratedMeters,0,PunchThrough(id),"통과한 물체 두께");
                if(id==WeaponStanceMechanic.Verdilac&&c.Explosion&&c.Time>v.Launch+1.3+1e-9)throw new ArgumentException("버디락 소멸 폭발은 최대1.3초 수명을 넘길 수 없습니다.");
                if(c.Surface&&id is not (WeaponStanceMechanic.Argo or WeaponStanceMechanic.Edun or WeaponStanceMechanic.Grigori))throw new ArgumentException("이 투사체는 표면 반사/충돌 경로를 지원하지 않습니다.");
                if(c.Blade&&(c.Surface||c.Explosion))throw new ArgumentException("칼날 접촉에 표면/폭발을 중복 지정하지 마세요.");
                if(c.Explosion&&id is not (VinquibusState.WeaponId or WeaponStanceMechanic.Verdilac or WeaponStanceMechanic.Korumm))throw new ArgumentException("이 무기의 폭발은 직격/표면 충돌에서 발생합니다.");
                if(c.Blade&&!BladeHits(id,v.Sequence).Any())throw new ArgumentException("이 고유 동작에는 별도 칼날 타격이 없습니다.");
                if(c.Blade)MeleeEngine.Range(c.BladeHit,1,BladeHits(id,v.Sequence).Length,"칼날 타격 번호");
                events.Add(new(c.Time,1,v,c));
                if(!c.Blade&&AutomaticExplosion(id))events.Add(new(c.Time+(id==WeaponStanceMechanic.Edun?(v.ExplosionModel=="Article"?1:.8):0),2,v,c));
            }
            var projectiles=contacts.Where(c=>!c.Blade&&!c.Explosion).ToArray();
            if(id==WeaponStanceMechanic.Nepheri)
            {
                var balls=projectiles.GroupBy(c=>c.Projectile).OrderBy(g=>g.Key).ToArray();
                if(balls.Length>1&&(balls.Any(g=>g.First().Launched is null)||balls.Zip(balls.Skip(1),(a,b)=>(b.First().Launched??v.Launch)<=(a.First().Launched??v.Launch)).Any(x=>x)))throw new CombatRuleUnavailableException("네페리 화염구4발은 연속 발사입니다. 각 화염구의 서로 다른 발사 시각을 지정하세요. 원문에 없는 간격을 생성하지 않습니다.");
            }
            if(projectiles.Length>0&&(id==WeaponStanceMechanic.Nepheri&&v.Sequence!=2||id==WeaponStanceMechanic.Agendus&&v.Sequence!=1||id==WeaponStanceMechanic.Grigori&&v.Sequence!=2&&!v.Sliding))throw new ArgumentException("이 동작 순서에서는 투사체가 발사되지 않습니다. 고유 발사 동작을 선택하세요.");
            if(v.Sliding&&id!=WeaponStanceMechanic.Grigori)throw new ArgumentException("이 슬라이딩 발사 선택은 테넷 그리고리 전용입니다.");
            if(id==WeaponStanceMechanic.Grigori&&projectiles.Any(c=>c.Time>v.Launch))
            {
                if(v.ProjectileExpires is not {} end)throw new CombatRuleUnavailableException("테넷 그리고리의 콤보별 원반 수명 수식이 위키 미기재입니다. 명시한 만료 시각의 조건부 경로 또는 발사 즉시 접촉을 사용하세요. "+WeaponStanceMechanic.Source(id));
                MeleeEngine.Range(end,v.Launch,300,"그리고리 원반 만료 시각");if(projectiles.Any(c=>c.Time>end))throw new ArgumentException("원반 만료 뒤 명중할 수 없습니다.");
            }
            if(id==WeaponStanceMechanic.Edun&&projectiles.Length>1)throw new ArgumentException("에둔 투척은 첫 충돌 뒤 폭발합니다. 관통 명중을 추가할 수 없습니다.");
            if(id==WeaponStanceMechanic.Argo&&(projectiles.Length>2||projectiles.Any(c=>c.Surface)&&projectiles.Length>1))throw new CombatRuleUnavailableException("아르고 & 벨은 적1회 반사까지, 표면 반사 후에는 귀환합니다. 표면 반사 뒤 귀환 피해의 상세 규칙은 미기재입니다.");
            if(id==WeaponStanceMechanic.Verdilac&&projectiles.GroupBy(c=>c.Projectile).Any(g=>g.Count()>1))throw new CombatRuleUnavailableException("버디락 관통 후 비행 수명이 감소하지만 접촉당 감소량이 미기재입니다. 첫 명중과 지정한 소멸 폭발은 계산합니다.");
            if(WeaponStanceMechanic.IsQuassus(id)&&projectiles.GroupBy(c=>c.Target).Any(g=>g.Select(c=>c.Projectile).Distinct().Count()>1)&&!v.IndependentDaggers)throw new CombatRuleUnavailableException("콰서스 여러 단검의 타격·베기 상태가 간헐적으로 합쳐지는 규칙은 미기재입니다. 독립 타격 조건을 명시하거나 한 단검씩 비교하세요.");
            foreach(var repeated in projectiles.Where(c=>!c.Surface).GroupBy(c=>(c.Projectile,c.Target)).Where(g=>g.Count()>1))
            {
                var hits=repeated.OrderBy(c=>c.Time).ToArray();
                if(id!=WeaponStanceMechanic.Grigori||hits.Zip(hits.Skip(1),(a,b)=>!projectiles.Any(s=>s.Surface&&s.Projectile==a.Projectile&&s.Time>a.Time&&s.Time<b.Time)).Any(x=>x))throw new ArgumentException("같은 투사체의 재명중은 그리고리의 명시한 표면 반사 사이에서만 가능합니다.");
            }
            foreach(var group in projectiles.GroupBy(c=>c.Projectile))
            {
                if(group.Select(c=>c.Launched??v.Launch).Distinct().Count()>1)throw new ArgumentException("같은 투사체의 발사 시각은 같아야 합니다.");
                var path=group.OrderBy(c=>c.Time).ToArray();for(var i=1;i<path.Length;i++)if(path[i].Travel<path[i-1].Travel||path[i].PenetratedMeters<path[i-1].PenetratedMeters)throw new ArgumentException("같은 투사체의 누적 거리와 관통 두께는 뒤로 줄어들 수 없습니다.");
            }
            var detonations=contacts.Where(c=>c.Explosion).ToArray();if(detonations.GroupBy(c=>c.Projectile).Any(g=>g.Count()>1)||detonations.Any(d=>projectiles.Any(c=>c.Projectile==d.Projectile&&c.Time>d.Time)))throw new ArgumentException("같은 투사체는 한 번 소멸하며 이후에 직격할 수 없습니다.");
            if(contacts.Where(c=>c.Blade).GroupBy(c=>(c.BladeHit,c.Target)).Any(g=>g.Count()>1))throw new ArgumentException("같은 칼날 타격을 같은 대상에 중복 지정하지 마세요.");
            events.Add(new(v.Start,0,v));
        }
        return events.OrderBy(e=>e.Time).ThenBy(e=>e.Kind==0?0:e.Kind==3?1:e.Kind+1).ToArray();
    }
    public static StanceHit[] BladeHits(string id,int sequence)
    {
        if(id==VinquibusState.WeaponId)return [];
        if(!WeaponStanceMechanic.IsWarfan(id)&&id is not (VinquibusState.WeaponId or WeaponStanceMechanic.Verdilac or WeaponStanceMechanic.Korumm or WeaponStanceMechanic.Nepheri or WeaponStanceMechanic.Agendus or WeaponStanceMechanic.Grigori))return [];
        var key=WeaponStanceMechanic.Key(id);
        var name=id==WeaponStanceMechanic.QuassusPrime?"Quassus":MeleeRuleCatalog.Load().Weapon(id).Name;
        return StanceCatalog.Load().Stances.Single(s=>s.Id==name).Combos.Single(c=>c.Key==key).Hits.Where(h=>h.Group==sequence&&h.AttackType is not ("Ranged" or "Slam")).SelectMany(h=>Enumerable.Repeat(h with {Count=1},h.Count)).ToArray();
    }
    public static Build WithResources(Build current,Build source)=>current with {
        Scenario=current.Scenario with {Combo=source.Scenario.Combo},Melee=current.Melee! with {Tennokai=source.Melee!.Tennokai,Conditions=new(current.Melee.Conditions??[]) {
            ["BlockCharges"]=source.Melee.Conditions?.GetValueOrDefault("BlockCharges")??0,
            ["AnimosityStacks"]=Heavy(current.WeaponId)?source.Melee.Conditions?.GetValueOrDefault("AnimosityStacks")??0:current.Melee.Conditions?.GetValueOrDefault("AnimosityStacks")??0,["ProjectilesHit"]=0}}};
    public static Calculation Calculate(Catalog catalog,Build b,WeaponActionInput v,WeaponActionContact? contact=null,bool explosion=false,double distance=0)
    {
        if(v.Hologram>0)b=HallOfMirrorsRules.Copy(b,v.Hologram);
        if(b.WeaponId==VinquibusState.WeaponId)return VinquibusFlight.Calculate(catalog,b,v,explosion,distance);
        if(WarfanSlam.Active(v))return WarfanSlam.Calculate(catalog,b,v,contact,distance);
        if(b.WeaponId==WeaponStanceMechanic.Agendus)b=b with {Melee=b.Melee! with {Conditions=new(b.Melee.Conditions??[]){["AgendusFalloffModel"]=v.ExplosionModel=="Article"?1:v.ExplosionModel=="Module"?2:0}}};
        if(contact?.Blade==true)
        {
            var hit=BladeHits(b.WeaponId,v.Sequence)[contact.BladeHit-1];var attack=Heavy(b.WeaponId)?"Heavy Attack":"Normal Attack";
            var profile=MeleeRuleCatalog.Load().Weapon(b.WeaponId);var motion=hit.Multiplier;
            if(Heavy(b.WeaponId))motion/=MeleeClassRules.HeavyDamage(profile)/catalog.Weapons.Single(w=>w.Id==b.WeaponId).Damage.Values.Sum();
            return MeleeEngine.CalculateAttack(catalog,b with {Stance=null,Scenario=b.Scenario with {MotionMultiplier=motion},Melee=b.Melee! with {Attack=attack,Distance=distance,Finisher=null}},hit,useInitialComboFloor:false);
        }
        var name=Attack(b.WeaponId,v.Sequence,explosion);var rule=MeleeEngine.AttackPolicy(b.WeaponId,name);var profileRule=MeleeRuleCatalog.Load().Weapon(b.WeaponId);
        var single=new StanceHit(1,1,1,1,1,1,1,[],"",AttackOverride:name);
        var raw=WeaponStanceMechanic.Raw(profileRule,name,single)!.Value;
        if(b.WeaponId==WeaponStanceMechanic.Edun&&explosion&&v.ExplosionModel=="Article")
        {
            var node=JsonNode.Parse(raw.GetRawText())!.AsObject();node["Range"]=5;node["Falloff"]!["EndRange"]=5;raw=JsonSerializer.SerializeToElement(node);
        }
        return MeleeEngine.CalculateAttack(catalog,b with {Stance=null,Scenario=b.Scenario with {MotionMultiplier=Motion(b.WeaponId,v.Sequence,explosion)},Melee=b.Melee! with {Attack=name,Distance=distance,TargetIndex=1,Finisher=null,Tennokai=Heavy(b.WeaponId)&&b.Melee.Tennokai}},component:new(raw,rule,SingleProjectile:true),useInitialComboFloor:false);
    }
    public static void CheckTravel(Build b,WeaponActionInput input,WeaponActionContact c,Calculation payload)
    {
        if(c.Blade||c.Explosion||b.WeaponId==WeaponStanceMechanic.Korumm)return;
        double speed=b.WeaponId switch {WeaponStanceMechanic.Argo=>75,WeaponStanceMechanic.Edun=>50,WeaponStanceMechanic.Quassus or WeaponStanceMechanic.QuassusPrime=>46,WeaponStanceMechanic.Arum=>49,WeaponStanceMechanic.Nepheri=>100,WeaponStanceMechanic.Agendus=>80,WeaponStanceMechanic.Grigori=>1,_=>0};
        speed*=1+payload.Melee!.Effects.GetValueOrDefault("ProjectileSpeed");
        var launch=c.Launched??input.Launch;
        if(speed>0&&c.Time+1e-9<launch+c.Travel/speed)throw new ArgumentException("접촉 시각이 위키 투사체 속도로 계산한 최단 비행보다 빠릅니다.");
        if(!payload.Melee.HitConnects)throw new ArgumentException("지정한 비행이 무기 투사체 사거리를 넘었습니다.");
        if(b.WeaponId==WeaponStanceMechanic.Verdilac&&c.Time>input.Launch+1.3+1e-9)throw new ArgumentException("버디락 파동은1.3초 수명 안에 명중해야 합니다.");
        if(b.WeaponId==WeaponStanceMechanic.Nepheri&&c.Time>launch+.2+1e-9)throw new ArgumentException("네페리 화염구는0.2초 수명 안에 명중해야 합니다.");
    }
    internal static string Signature(Catalog catalog,Build b,WeaponActionInput input,WeaponActionContact? contact,bool explosion)
    {
        var m=b.Melee!;var conditions=(m.Conditions??[]).Where(p=>!CombatScene.EnemyConditionKeys.Contains(p.Key)).ToDictionary();conditions["HeavyAnimation"]=0;
        var neutral=b with {Scenario=b.Scenario with {Enemy=new(1e9,0),ExistingStatusTypes=0},Arcanes=(b.Arcanes??new()) with {Frozen=false},Melee=m with {Faction="Neutral",Statuses=[],Debuffs=[],Shields=0,Overguard=0,HeatArmorStrip=0,Conditions=conditions}};
        var c=Calculate(catalog,neutral,input,contact,explosion);return JsonSerializer.Serialize(new {c.Damage,c.CriticalChance,c.CriticalMultiplier,c.StatusChance,c.Melee!.StatusDamage},Catalog.Json);
    }
}
