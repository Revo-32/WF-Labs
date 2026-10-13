namespace TennoForge.Core;

public record ZenistarDeployment(double Launch,double Land,int CenterTarget=1,int Disc=1,double? Retrieve=null,double[]? AuraTimes=null,bool Contact=false,double WrathfulCriticalPercent=0,double? RetrieveStarted=null,int Hologram=0,double HologramDistance=0);
public sealed class ZenistarDiscState
{
    public const string WeaponId="/Lotus/Weapons/Tenno/Melee/SunDialAxe/SundialAxeWeapon";
    public const string Source="https://wiki.warframe.com/w/Zenistar?oldid=2793361";
    public const string WrathfulSource="https://wiki.warframe.com/w/Wrathful_Advance?oldid=2816114";
    public record Disc(ZenistarDeployment Input,double EarliestEnd,double LatestEnd,int Combo,int HallGeneration=0);
    private readonly Dictionary<int,Disc> discs=[];
    private readonly HallOfMirrorsState? mirrors;
    public ZenistarDiscState(HallOfMirrorsState? mirrors=null){this.mirrors=mirrors;}
    public static void Validate(ZenistarDeployment[] items,int targets)
    {
        if(items.Length>64||items.GroupBy(i=>i.Disc).Any(g=>g.Count()>1))throw new ArgumentException("제니스타 원반 번호는 중복 없이 최대64개입니다.");
        foreach(var d in items)
        {
            MeleeEngine.Range(d.Hologram,0,4,"원반 공격 주체");MeleeEngine.Range(d.HologramDistance,0,9,"분신에서 원반 배치점까지 거리");
            MeleeEngine.Range(d.Launch,0,300,"원반 발사 시각");MeleeEngine.Range(d.Land,d.Launch,300,"원반 배치 시각");MeleeEngine.Range(d.CenterTarget,1,targets,"원반 중심 대상");MeleeEngine.Range(d.Disc,1,10000,"원반 번호");MeleeEngine.Range(d.WrathfulCriticalPercent,0,10000,"발사 시 래스풀 치명타 확률");
            if(d.Retrieve is {} r)MeleeEngine.Range(r,d.Land,300,"원반 회수 완료 시각");
            if(d.RetrieveStarted is {} start)
            {
                if(d.Retrieve is not {} end)throw new ArgumentException("회수 동작 시작을 지정하면 회수 완료 시각도 지정하세요.");
                MeleeEngine.Range(start,d.Land,end,"원반 회수 동작 시작");if(start==end)throw new ArgumentException("회수 완료는 회수 동작 시작보다 뒤여야 합니다.");
            }
            if(d.AuraTimes is {Length:>512})throw new ArgumentException("원반 오라 명중은 최대512회입니다.");
            foreach(var t in d.AuraTimes??[])MeleeEngine.Range(t,d.Land,300,"원반 오라 시각");
            if((d.AuraTimes??[]).Distinct().Count()!=(d.AuraTimes??[]).Length)throw new ArgumentException("한 원반의 같은 오라 명중 시각이 중복되었습니다.");
        }
    }
    public Disc Launch(ZenistarDeployment input,int combo)
    {
        MeleeEngine.Range(combo,1,13,"원반 발사 콤보");
        if(discs.Values.Any(d=>d.Input.Hologram==input.Hologram&&Active(d.Input.Disc,input.Launch)))throw new ArgumentException("기존 원반을 회수한 뒤 다음 원반을 발사하세요.");
        if(input.Hologram>0)combo=1;
        var life=Math.Min(100,10*combo);
        // The source states the duration and 100 s return bug, but not the
        // launch-vs-landing origin. Preserve both until that boundary matters.
        var disc=new Disc(input,Math.Min(input.Retrieve??double.PositiveInfinity,input.Launch+life),Math.Min(input.Retrieve??double.PositiveInfinity,input.Land+life),combo,mirrors?.Generation??0);
        discs.Add(input.Disc,disc);return disc;
    }
    public Disc Get(int id)=>discs.TryGetValue(id,out var d)?d:throw new ArgumentException("아직 발사하지 않은 제니스타 원반입니다.");
    public bool Active(int id,double time)
    {
        var d=Get(id);if(d.Input.Hologram>0&&mirrors is not null&&(!mirrors.Active(d.Input.Hologram,time)||d.HallGeneration!=mirrors.Generation))return false;
        if(time<d.Input.Launch||time>=d.LatestEnd)return false;
        if(time>=d.EarliestEnd)throw new CombatRuleUnavailableException("제니스타 지속시간의 시작이 발사/배치 중 어느 시점인지는 원문 미기재입니다. 이 경계의 원반 유지 여부를 확정할 수 없습니다. "+Source);
        return true;
    }
    public bool Deployed(double time,int hologram=0)=>discs.Values.Any(d=>d.Input.Hologram==hologram&&Active(d.Input.Disc,time));
    public bool Retrieving(double time)=>discs.Values.Any(d=>d.Input.Hologram==0&&d.Input.RetrieveStarted is {} start&&time>=start&&time<d.Input.Retrieve&&Active(d.Input.Disc,time));
    public bool Aura(int id,double time)
    {
        var d=Get(id);if(time<d.Input.Land)throw new ArgumentException("원반 배치 전 오라 명중은 불가능합니다.");return Active(id,time);
    }
    public double NextBoundary(double time)=>discs.Values.SelectMany(d=>new[]{d.EarliestEnd,d.LatestEnd}).Where(t=>t>time+1e-9).DefaultIfEmpty(double.PositiveInfinity).Min();
    public static Calculation Calculate(Catalog catalog,Build build,string attack="Disc Aura",double distance=0,double wrathfulCriticalPercent=0,bool useInitialComboFloor=true)
    {
        if(build.WeaponId!=WeaponId||attack is not ("Disc Impact" or "Disc Explosion" or "Disc Aura"))throw new ArgumentException("제니스타 원반의 피해 성분이 필요합니다.");
        MeleeEngine.Range(wrathfulCriticalPercent,0,10000,"원반 저장 치확");
        var raw=MeleeRuleCatalog.Load().Weapon(WeaponId).Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.Text("AttackName")==attack);
        var input=build with {Stance=null,Scenario=build.Scenario with {MotionMultiplier=1},Melee=build.Melee! with {Attack=attack,Distance=distance,Tennokai=false,Finisher=null,TargetIndex=1}};
        return MeleeEngine.CalculateAttack(catalog,input,useInitialComboFloor:useInitialComboFloor,component:new(raw,MeleeEngine.AttackPolicy(WeaponId,attack),NoEnergyChannel:true,FlatCriticalBonus:attack=="Disc Aura"?wrathfulCriticalPercent/100:0));
    }
}
