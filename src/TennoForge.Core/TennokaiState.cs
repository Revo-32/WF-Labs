namespace TennoForge.Core;

/// <summary>Opportunity/input state, separate from animation contact times.</summary>
public sealed class TennokaiState
{
    public const string Source="https://wiki.warframe.com/w/Melee?oldid=2817350";
    public const string DisciplineSource="https://wiki.warframe.com/w/Discipline's_Merit?oldid=2651937";
    public const string TruthSource="https://wiki.warframe.com/w/Truth's_Flame?oldid=2812278";
    private double clock;
    private readonly Dictionary<long,HashSet<(int Shot,int Target)>> inputContacts=[];
    private readonly double chance,window;
    private readonly int every;
    private readonly int? truthRank;
    private bool chain,successful;
    private bool flightPaused;
    public bool Enabled=>chance>0||every>0;
    public int Progress {get;private set;}
    public int Generated {get;private set;}
    public int Consumed {get;private set;}
    public int Refunds {get;private set;}
    public int Failures {get;private set;}
    public bool Active {get;private set;}
    public bool ActiveBonus {get;private set;}
    public bool ActiveSuccessful=>Active&&successful;
    public double EndMinimum {get;private set;}
    public double EndMaximum {get;private set;}
    public double RawCurseDamage {get;private set;}
    public bool WindowAmbiguous=>EndMaximum>EndMinimum+1e-9;
    public TennokaiState(double chance,int every,double window,int? truthRank=null)
    {
        if(!double.IsFinite(chance)||chance is <0 or >1||every<0||!double.IsFinite(window)||window<=0||truthRank is <0 or >3)throw new ArgumentException("텐노카이 초기 조건 오류");
        this.chance=chance;this.every=every;this.window=window;this.truthRank=truthRank;
    }
    public void Advance(double time)
    {
        if(!double.IsFinite(time)||time<clock-1e-9)throw new ArgumentException("텐노카이 시각은 순서대로 진행해야 합니다.");
        if(flightPaused&&EndMaximum>clock)
        {
            var elapsed=Math.Max(0,time-clock);
            if(EndMinimum>clock)EndMinimum+=elapsed;
            EndMaximum+=elapsed;
        }
        clock=Math.Max(clock,time);
        if(clock>=EndMaximum){EndMinimum=EndMaximum=0;chain=false;}
    }
    public void SetFlightPaused(double time,bool paused){Advance(time);flightPaused=paused;}
    public double RemainingMinimum(double time){Advance(time);return Math.Max(0,EndMinimum-time);}
    public double RemainingMaximum(double time){Advance(time);return Math.Max(0,EndMaximum-time);}
    public void StartingOpportunity(double seconds)
    {
        if(!Enabled||clock!=0||!double.IsFinite(seconds)||seconds<=0||seconds>window)throw new ArgumentException("교전 시작 텐노카이 기회 설정 오류");
        EndMinimum=EndMaximum=seconds;
    }
    public bool Contact(double time,long input,int shot,int target,bool gunblade,bool eligible,double roll)
    {
        Advance(time);
        if(!Enabled||!eligible)return false;
        if(!inputContacts.TryGetValue(input,out var contacts)){contacts=[];inputContacts[input]=contacts;}
        if(!contacts.Add(gunblade?(shot,target):(0,0)))return false;
        if(every>0){Progress++;if(Progress<every)return false;Progress=0;}
        else if(roll>=chance)return false;
        Generated++;
        if(EndMaximum>time)
        {
            // Sources specify neither re-proc refresh nor suppression while an
            // opportunity is open. Preserve the possible end interval. Only a
            // use whose eligibility depends on that interval is refused.
            EndMinimum=EndMinimum>time?EndMinimum:Math.Min(EndMaximum,time+window);
            EndMaximum=Math.Max(EndMaximum,time+window);
        }
        else{EndMinimum=EndMaximum=time+window;chain=false;}
        return true;
    }
    public bool Begin(double time)
    {
        Advance(time);
        if(Active)throw new ArgumentException("이전 텐노카이 동작의 종료 또는 취소 시각이 필요합니다.");
        if(EndMaximum<=time)return false;
        if(EndMinimum<=time)throw new CombatRuleUnavailableException("기회가 열려 있을 때 텐노카이 재발동의 갱신 규칙은 위키 미기재입니다. 이 사용 시점의 기회 유지 여부를 확정할 수 없습니다.");
        Active=true;ActiveBonus=chain;successful=false;Consumed++;EndMinimum=EndMaximum=0;chain=false;return true;
    }
    public bool Kill(double time,bool eligible)
    {
        Advance(time);
        if(truthRank is null||!Active||successful||!eligible)return false;
        successful=true;chain=true;Refunds++;EndMinimum=EndMaximum=time+4;return true;
    }
    // Returns raw six-second self damage. Defense/status immunity are caller
    // inputs; this is never subtracted from target health or added to DPS.
    public double Finish(double time,bool cancel=false,bool statusImmune=false)
    {
        Advance(time);if(!Active)return 0;
        var failed=truthRank is not null&&!successful&&!cancel;
        Active=false;ActiveBonus=false;successful=false;
        if(!failed)return 0;
        Failures++;chain=false;EndMinimum=EndMaximum=0;
        var raw=statusImmune?0:new[]{25d,35,50,100}[truthRank!.Value]*6;RawCurseDamage+=raw;return raw;
    }
    public static bool Generates(MeleeWeaponRule profile,string attack,bool finisher=false)
    {
        var p=MeleeEngine.AttackPolicy(profile.Id,attack);
        if(finisher||p.Heavy||attack.Contains("Slam",StringComparison.Ordinal))return false;
        if(profile.Raw.Text("Class")=="Gunblade"||p.Code=="ExaltedWave")return true;
        return attack is "Normal Attack" or "Slide Attack" or "Aerial Attack" or "Wall Attack";
    }
    public static bool RefundEligible(MeleeWeaponRule profile,string attack)
    {
        if(profile.Raw.Text("Class")=="Glaive")return false;
        if(profile.Name=="Edun"&&attack.Contains("Explosion",StringComparison.Ordinal))return false;
        if(profile.Name=="Wolf Sledge"&&attack.Contains("Throw",StringComparison.Ordinal))return false;
        if(profile.Name=="Zenistar")return attack.Contains("Recall",StringComparison.Ordinal)||attack.Contains("Retrieval",StringComparison.Ordinal);
        return true;
    }
}
