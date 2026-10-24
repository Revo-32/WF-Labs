namespace TennoForge.Core;

public record RangedRivenWorkshopState(RangedRivenRoll Current, int Cycles = 0, long KuvaSpent = 0,
    string? Locked = null, bool PreserveSplice = true, RangedRivenRoll? Pending = null, int SplicersSpent = 0);

/// <summary>Exact state/cost rules; example draws use an explicitly hypothetical uniform distribution.</summary>
public static class RangedRivenWorkshop
{
    public const string SamplingAssumption = "예시 추첨: 옵션·구성·롤을 균등 추첨한다고 가정합니다. 접합 계승값은 가능한 구간의 중간값입니다. 실제 게임 확률 예측이 아닙니다.";
    public static int NextCost(int completed,bool locked)=>RivenWorkshop.NextCost(completed,locked);
    public static long Budget(int completed,int attempts,bool locked)=>RivenWorkshop.Budget(completed,attempts,locked);
    public static void Validate(RangedRivenWorkshopState state)
    {
        if(state is null||state.Current is null||state.Current.Traits is null)throw new ArgumentException("리벤 작업 데이터가 비어 있습니다.");
        ValidateRoll(state.Current);
        if(state.Cycles<0||state.KuvaSpent<0||state.SplicersSpent<0)throw new ArgumentException("리벤 작업 기록이 잘못되었습니다.");
        if(state.Locked is {} stat&&!state.Current.Traits.Any(t=>t.Stat==stat&&!t.Spliced))throw new ArgumentException("일반 옵션 하나만 고정할 수 있습니다.");
        if(state.Pending is {} pending)ValidateRoll(pending);
        if(state.Pending is {} next)
        {
            var old=state.Current;
            if(next.WeaponId!=old.WeaponId||next.Rank!=old.Rank||next.BaseDrain!=old.BaseDrain||next.Polarity!=old.Polarity)throw new ArgumentException("순환 결과의 무기·랭크·코스트·극성이 일치하지 않습니다.");
            var kept=old.Traits.Where(t=>t.Stat==state.Locked||t.Spliced&&state.PreserveSplice).ToArray();
            if(next.Traits.Any(t=>t.Spliced&&!kept.Contains(t)))throw new ArgumentException("순환만으로 새로운 접합 옵션을 얻을 수 없습니다.");
            if(kept.Any(t=>!next.Traits.Contains(t))||kept.Length>0&&(old.Traits.Count(t=>t.Negative)!=next.Traits.Count(t=>t.Negative)||old.Traits.Length!=next.Traits.Length))throw new ArgumentException("고정한 옵션 또는 옵션 구성이 바뀌었습니다.");
        }
    }
    public static RangedRivenWorkshopState Lock(RangedRivenWorkshopState state,string? stat)
    { EnsureIdle(state);var next=state with{Locked=stat};Validate(next);return next; }
    public static RangedRivenWorkshopState Preserve(RangedRivenWorkshopState state,bool preserve)
    {EnsureIdle(state);return state with{PreserveSplice=preserve};}
    private static void EnsureIdle(RangedRivenWorkshopState state)
    {Validate(state);if(state.Pending is not null)throw new ArgumentException("먼저 현재 옵션 유지 또는 새 결과 채택을 선택하세요.");}
    public static RangedRivenWorkshopState CycleExample(RangedRivenWorkshopState state,Random random)
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
        {var range=RangedRivens.Range(roll,traits[i]);traits[i]=traits[i] with{Value=Round(range.Minimum+random.NextDouble()*(range.Maximum-range.Minimum))};}
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
        roll=roll with{Traits=traits.ToArray()};ValidateRoll(roll);
        return state with{Pending=roll,Cycles=checked(state.Cycles+1),KuvaSpent=checked(state.KuvaSpent+NextCost(state.Cycles,state.Locked is not null))};
        void Add(bool negative,int count)
        {
            for(var i=0;i<count;i++)
            {
                var pool=Pool(state.Current,negative).Where(s=>traits.All(t=>t.Stat!=s)).ToArray();
                traits.Add(new(pool[random.Next(pool.Length)],0,negative));
            }
        }
    }
    public static RangedRivenWorkshopState Resolve(RangedRivenWorkshopState state,bool accept)
    {
        Validate(state);if(state.Pending is null)throw new ArgumentException("채택할 순환 결과가 없습니다.");
        return state with{Current=accept?state.Pending:state.Current,Pending=null};
    }
    public static RangedRivenWorkshopState SpliceExample(RangedRivenWorkshopState state,RangedSplicePreview candidate,Random random)
    {
        EnsureIdle(state);
        if(state.SplicersSpent==int.MaxValue)throw new ArgumentException("접합 작업 기록의 최대 범위를 초과했습니다.");
        var predictions=RangedRivens.Predict(state.Current);
        var prediction=predictions.SingleOrDefault(p=>p==candidate)??throw new ArgumentException("현재 리벤에 유효하지 않은 접합입니다.");
        var remaining=state.Current.Traits.Where((_,i)=>i!=candidate.First&&i!=candidate.Second).ToList();
        var pool=Pool(state.Current,candidate.ReplacementNegative).Where(s=>remaining.All(t=>t.Stat!=s)).ToArray();
        var replacement=new RangedRivenTrait(pool[random.Next(pool.Length)],0,candidate.ReplacementNegative);
        var splice=new RangedRivenTrait(candidate.Result,Round((prediction.Value.Low+prediction.Value.High)/2),Spliced:true);
        // Keep unaffected traits in their original positions; order matters for element combination.
        var traits=state.Current.Traits.ToArray();traits[candidate.First]=splice;traits[candidate.Second]=replacement;
        var roll=state.Current with{Traits=traits};var range=RangedRivens.Range(roll,replacement);
        traits[candidate.Second]=replacement with{Value=Round(range.Minimum+random.NextDouble()*(range.Maximum-range.Minimum))};
        ValidateRoll(roll);
        return state with{Current=roll,Locked=null,PreserveSplice=true,SplicersSpent=checked(state.SplicersSpent+1)};
    }
    private static double Round(double value)=>Math.Round(value,1,MidpointRounding.AwayFromZero);
    private static void ValidateRoll(RangedRivenRoll roll)=>RangedRivens.Validate(roll,requireMetadata:false);
    public static string[] Pool(RangedRivenRoll roll,bool negative)
    {
        var weapon=RangedEngine.Weapons.Single(w=>w.Id==roll.WeaponId);
        return RangedRivens.Table.Attributes.Where(a=>!a.Spliced&&(!negative||!a.PositiveOnly)&&a.Bases.ContainsKey(RangedEngine.ModCategory(weapon))).Select(a=>a.Key).ToArray();
    }
}
