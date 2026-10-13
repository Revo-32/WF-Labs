using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;
public record StanceHit(int Group,double Multiplier,int Count,double Impact,double Puncture,double Slash,double StatusMultiplier,string[] ForcedProcs,string AttackType,double ComponentFraction=1,double ProcDamageMultiplier=1,string? AttackOverride=null);
public record StanceCombo(string Key,string Name,double? Duration,StanceHit[] Hits,string[] Unsupported,string? AttackOverride=null,string? ActionOverride=null,string? ResolutionNote=null,string? InheritedFrom=null)
{
    public string ActionKind=>ActionOverride??Key;
    public int HitCount=>Hits.Where(h=>h.Multiplier>0).Sum(h=>h.Count);
    public double MotionSum=>Hits.Sum(h=>h.Multiplier*h.Count);
    public string? FinisherKind=>Key.StartsWith("Finisher ",StringComparison.Ordinal)?Key[9..]:Key=="Finisher"?"Ground":null;
    public string InputLabel=>FinisherKind is {} f?FinisherCatalog.Label(f):AttackOverride is not null?Name:Key switch{"Neutral"=>"정지","Forward"=>"전진","Block"=>"방어 + 정지","Forward Block"=>"방어 + 전진","Heavy" when ResolutionNote?.StartsWith("미검증: 케라티노스")==true=>Name,"Heavy Article"=>Name,"Heavy"=>"강공격","Heavy Sliding"=>"슬라이딩 강공격 · 칼날","Slide"=>"슬라이드","Aerial"=>"공중","Wall"=>"벽 타기","Slam"=>"슬램",_=>Key};
    public string SelectionLabel=>Duration is null?$"{InputLabel} · 타격 피해":InputLabel;
}
public record Stance(string Id,string Name,string WeaponType,string Source,StanceCombo[] Combos,bool IsDefault=false,bool ConclaveOnly=false,bool UniqueToWeapon=false,string? ModId=null,bool AbilityOnly=false)
{
    public string SelectionLabel=>IsDefault?$"기본 동작 · {Name}":Name;
}
public record StanceCatalog(long Revision,string RevisionTime,string RetrievedAt,string Source,string Authority,string SourceSha256,Stance[] Stances)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,Stance[]> resolved=new();
    public static StanceCatalog Load()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.stances.json")??throw new InvalidOperationException("스탠스 자료가 없습니다.");
        return JsonSerializer.Deserialize<StanceCatalog>(stream,Catalog.Json)??throw new InvalidOperationException("스탠스 자료 형식 오류");
    }
    public Stance[] ForWeapon(Weapon w)=>resolved.GetOrAdd(w.Id,_=>
    {
        var exalted=ExaltedRules.Find(w.Id);
        var available=exalted is not null?Stances.Where(s=>s.Id==exalted.Stance).ToArray():Stances.Where(s=>!s.ConclaveOnly&&!s.AbilityOnly&&s.WeaponType==w.WeaponType&&(!s.UniqueToWeapon||s.Id==w.EnglishName)).OrderBy(s=>s.IsDefault).ThenBy(s=>s.Name,StringComparer.Ordinal).ToArray();
        var finishers=FinisherCatalog.ForWeapon(w);
        available=available.Select(s=>UniqueWeaponStances.Resolve(w.Id,s,Stances)).ToArray();
        if(exalted is not null)available=available.Select(s=>ExaltedRules.ResolveStance(exalted,s)).ToArray();
        available=available.Select(s=>s with {Combos=s.Combos.Concat(finishers).ToArray()}).ToArray();
        if(w.EnglishName=="Tak & Lug")
        {
            var source=MeleeRuleCatalog.Load().Weapon(w.Id).Source?.Url;
            // Weapon article rev.2789936: 591 x4, three pulses with forced
            // Blast. Raw HeavyAttack=2364 is the total, not one contact.
            return available.Select(s=>s with {Combos=s.Combos.Select(c=>c.Key!="Heavy"?c:c with {
                Name="강공격 · 방패 투척 4타",Duration=null,AttackOverride="Heavy Attack",ActionOverride="Heavy",
                Hits=[new(1,1,1,1,1,1,1,[],"",.25),new(1,1,3,1,1,1,1,["Blast"],"",.25)],
                ResolutionNote="탁 앤 러그 고유 강공격: 기본 피해 591 × 4타, 뒤의 3타에 강제 폭발 상태. 같은 입력의 콤보를 사용하며 공격이 끝나면 한 번 소모합니다. 방패를 고정해 모든 타격이 같은 대상에 명중하는 조건입니다. 회수·개별 타격 시간은 위키 미기재입니다. 출처: "+source}).ToArray()}).ToArray();
        }
        if(ShieldThrowMechanic.Applies(w.Id))available=available.Select(s=>ShieldThrowMechanic.Resolve(w.Id,s,Stances)).ToArray();
        if(WeaponStanceMechanic.Applies(w.Id))available=available.Select(s=>WeaponStanceMechanic.Resolve(w.Id,s,Stances)).ToArray();
        if(w.Id==JatKusarMechanic.WeaponId)available=available.Select(JatKusarMechanic.Resolve).ToArray();
        if(w.Id==KeratinosMechanic.WeaponId)available=available.Select(KeratinosMechanic.Resolve).ToArray();
        if(WarfanSlam.Applies(w.Id))available=available.Select(s=>WarfanSlam.Resolve(w.Id,s)).ToArray();
        if(w.WeaponType!="Glaive")return available;
        var profile=MeleeRuleCatalog.Load().Weapon(w.Id);
        // The generic stance table retains an old ranged-heavy animation row.
        // Current weapon pages define the heavy input as an in-flight recall
        // explosion. Preserve the raw table; resolve the actual weapon actions
        // as explicitly named uncharged/charged alternatives, never add both.
        return available.Select(s=>s with {Combos=s.Combos.SelectMany(c=>c.Key!="Heavy"?new[]{c}:new[]{
            Recall(c,"Heavy","강공격 · 일반 투척 회수","Throw Recall Explosion"),
            Recall(c,"Heavy Charged","강공격 · 충전 투척 회수","Charged Throw Recall Explosion")}).ToArray()}).ToArray();
        StanceCombo Recall(StanceCombo original,string key,string label,string attack)
        {
            if(!profile.Attacks.Any(a=>a.Key==attack))throw new InvalidOperationException("글레이브 회수 공격 원본 누락");
            return original with {Key=key,Name=label,Duration=null,Hits=[new(1,1,1,1,1,1,1,[],"")],AttackOverride=attack,ActionOverride="Heavy",
                ResolutionNote=$"글레이브 강공격은 비행 중 회수 폭발 1회입니다. {label}의 무기별 수치·강제 상태를 사용하며 투척 접촉·반사 피해는 합산하지 않습니다. 일반 스탠스의 과거 강공격 행은 원본 자료로 보존합니다. 출처: {profile.Source?.Url}"};
        }
    });
}
