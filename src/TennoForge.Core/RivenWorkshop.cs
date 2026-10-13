namespace TennoForge.Core;

public record RivenWorkshopState(RivenRoll Current, int Cycles = 0, long KuvaSpent = 0,
    RivenStat? Locked = null, bool PreserveSplice = true, RivenRoll? Pending = null, int SplicersSpent = 0);

/// <summary>Exact state/cost rules; example draws use an explicitly hypothetical uniform distribution.</summary>
public static class RivenWorkshop
{
    public const string SamplingAssumption = "예시 추첨: 옵션·구성·롤을 균등 추첨한다고 가정합니다. 접합 계승값은 가능한 구간의 중간값입니다. 실제 게임 확률 예측이 아닙니다.";
    private static readonly int[] Costs = [900,1000,1200,1400,1700,2000,2350,2750,3150,3500];
    public static int NextCost(int completed, bool locked)
    {
        if(completed<0)throw new ArgumentException("순환 횟수는 0 이상이어야 합니다.");
        return Costs[Math.Min(completed,9)]*(locked?2:1);
    }
    public static long Budget(int completed,int attempts,bool locked)
    {
        if(completed<0||attempts<0)throw new ArgumentException("횟수는 0 이상이어야 합니다.");
        long sum=0;var initial=Math.Min(attempts,Math.Max(0,10-completed));
        for(var i=0;i<initial;i++)sum+=NextCost(completed+i,locked);
        return sum+(long)(attempts-initial)*NextCost(10,locked);
    }
    public static void Validate(RivenWorkshopState state)
    {
        if(state is null||state.Current is null||state.Current.Traits is null)throw new ArgumentException("리벤 작업 데이터가 비어 있습니다.");
        RivenValues.Validate(state.Current);
        if(state.Cycles<0||state.KuvaSpent<0||state.SplicersSpent<0)throw new ArgumentException("리벤 작업 기록이 잘못되었습니다.");
        if(state.Locked is {} stat&&!state.Current.Traits.Any(t=>t.Stat==stat&&!t.Spliced))throw new ArgumentException("일반 옵션 하나만 고정할 수 있습니다.");
        if(state.Pending is {} pending)RivenValues.Validate(pending);
        if(state.Pending is {} next)
        {
            var old=state.Current;
            if(next.WeaponId!=old.WeaponId||next.Rank!=old.Rank||next.BaseDrain!=old.BaseDrain||next.Polarity!=old.Polarity)throw new ArgumentException("순환 결과의 무기·랭크·코스트·극성이 일치하지 않습니다.");
            var kept=old.Traits.Where(t=>t.Stat==state.Locked||t.Spliced&&state.PreserveSplice).ToArray();
            if(next.Traits.Any(t=>t.Spliced&&!kept.Contains(t)))throw new ArgumentException("순환만으로 새로운 접합 옵션을 얻을 수 없습니다.");
            if(kept.Any(t=>!next.Traits.Contains(t))||kept.Length>0&&(old.Traits.Count(t=>t.Negative)!=next.Traits.Count(t=>t.Negative)||old.Traits.Length!=next.Traits.Length))throw new ArgumentException("고정한 옵션 또는 옵션 구성이 바뀌었습니다.");
        }
    }
    public static RivenWorkshopState Lock(RivenWorkshopState state,RivenStat? stat)
    { EnsureIdle(state);var next=state with{Locked=stat};Validate(next);return next; }
    public static RivenWorkshopState Preserve(RivenWorkshopState state,bool preserve)
    {EnsureIdle(state);return state with{PreserveSplice=preserve};}
    private static void EnsureIdle(RivenWorkshopState state)
    {Validate(state);if(state.Pending is not null)throw new ArgumentException("먼저 현재 옵션 유지 또는 새 결과 채택을 선택하세요.");}
    public static RivenWorkshopState CycleExample(RivenWorkshopState state,Random random)
    {
        EnsureIdle(state);
        if(state.Cycles==int.MaxValue||state.KuvaSpent>long.MaxValue-7000)throw new ArgumentException("순환 작업 기록의 최대 범위를 초과했습니다.");
        var kept=state.Current.Traits.Where(t=>t.Stat==state.Locked||t.Spliced&&state.PreserveSplice).ToArray();
        var fixedShape=kept.Length>0;
        var positives=fixedShape?state.Current.Traits.Count(t=>!t.Negative):random.Next(2,4);
        var negatives=fixedShape?state.Current.Traits.Count(t=>t.Negative):random.Next(0,2);
        var traits=kept.ToList();
        Add(false,positives-traits.Count(t=>!t.Negative));Add(true,negatives-traits.Count(t=>t.Negative));
        var roll=state.Current with{Traits=traits.ToArray()};
        for(var i=kept.Length;i<traits.Count;i++)
        {var range=RivenValues.Range(roll,traits[i]);traits[i]=traits[i] with{Value=Round(range.Minimum+random.NextDouble()*(range.Maximum-range.Minimum))};}
        if(fixedShape)
        {
            var replacements=traits.Skip(kept.Length).ToList();
            traits=state.Current.Traits.Select(old=>
            {
                if(kept.Contains(old))return old;
                var index=replacements.FindIndex(t=>t.Negative==old.Negative);
                var value=replacements[index];replacements.RemoveAt(index);return value;
            }).ToList();
        }
        roll=roll with{Traits=traits.ToArray()};RivenValues.Validate(roll);
        return state with{Pending=roll,Cycles=checked(state.Cycles+1),KuvaSpent=checked(state.KuvaSpent+NextCost(state.Cycles,state.Locked.HasValue))};
        void Add(bool negative,int count)
        {
            for(var i=0;i<count;i++)
            {
                var pool=Pool(negative).Where(s=>traits.All(t=>t.Stat!=s)).ToArray();
                traits.Add(new(pool[random.Next(pool.Length)],0,negative));
            }
        }
    }
    public static RivenWorkshopState Resolve(RivenWorkshopState state,bool accept)
    {
        Validate(state);if(state.Pending is null)throw new ArgumentException("채택할 순환 결과가 없습니다.");
        return state with{Current=accept?state.Pending:state.Current,Pending=null};
    }
    public static RivenWorkshopState SpliceExample(RivenWorkshopState state,SpliceCandidate candidate,Random random)
    {
        EnsureIdle(state);
        if(state.SplicersSpent==int.MaxValue)throw new ArgumentException("접합 작업 기록의 최대 범위를 초과했습니다.");
        var predictions=RivenValues.Predict(state.Current);
        var prediction=predictions.SingleOrDefault(p=>p.Candidate==candidate)??throw new ArgumentException("현재 리벤에 유효하지 않은 접합입니다.");
        var remaining=state.Current.Traits.Where((_,i)=>i!=candidate.First&&i!=candidate.Second).ToList();
        var pool=Pool(candidate.RandomIsNegative).Where(s=>remaining.All(t=>t.Stat!=s)).ToArray();
        var replacement=new RivenTrait(pool[random.Next(pool.Length)],0,candidate.RandomIsNegative);
        var splice=new RivenTrait(candidate.Result,Round((prediction.Value.Low+prediction.Value.High)/2),Spliced:true);
        // Keep unaffected traits in their original positions; order matters for element combination.
        var traits=state.Current.Traits.ToArray();traits[candidate.First]=splice;traits[candidate.Second]=replacement;
        var roll=state.Current with{Traits=traits};var range=RivenValues.Range(roll,replacement);
        traits[candidate.Second]=replacement with{Value=Round(range.Minimum+random.NextDouble()*(range.Maximum-range.Minimum))};
        RivenValues.Validate(roll);
        return state with{Current=roll,Locked=null,PreserveSplice=true,SplicersSpent=checked(state.SplicersSpent+1)};
    }
    private static double Round(double value)=>Math.Round(value,1,MidpointRounding.AwayFromZero);
    public static RivenStat[] Pool(bool negative)=>Enum.GetValues<RivenStat>().Where(s=>!RivenValues.IsSpliced(s)&&
        (!negative||s is not(RivenStat.Damage or RivenStat.Heat or RivenStat.Cold or RivenStat.Electricity or RivenStat.Toxin))).ToArray();
}
