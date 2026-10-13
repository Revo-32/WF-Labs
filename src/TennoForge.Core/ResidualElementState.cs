namespace TennoForge.Core;

/// <summary>Demulcent: element-specific stacks; actual area ticks are explicit events.</summary>
public sealed class ResidualElementState
{
    public const string Source="https://wiki.warframe.com/w/Theorem_Demulcent?oldid=2651493";
    public static readonly DamageType[] Elements=[DamageType.Heat,DamageType.Cold,DamageType.Electricity,DamageType.Toxin];
    private sealed class State{public int Stacks;public bool Inside;public double End;}
    private readonly Dictionary<DamageType,State> states=Elements.ToDictionary(t=>t,_=>new State());
    public double NextExpiry=>states.Values.Where(s=>!s.Inside&&s.Stacks>0).Select(s=>s.End).DefaultIfEmpty(double.PositiveInfinity).Min();
    public ResidualElementState(Dictionary<string,double>? conditions=null)
    {
        foreach(var type in Elements)
        {
            var count=conditions?.GetValueOrDefault("Residual"+type)??0;
            var remaining=conditions?.GetValueOrDefault("Residual"+type+"Remaining",20)??20;
            if(!double.IsFinite(count)||count!=Math.Floor(count)||count is <0 or >15||!double.IsFinite(remaining)||remaining is <0 or >20)throw new ArgumentException("레지듀얼 시작 중첩은0~15·잔여 시간은0~20초입니다.");
            states[type].Stacks=(int)count;states[type].End=remaining;
        }
    }
    public void Advance(double time)
    {
        if(!double.IsFinite(time)||time<0)throw new ArgumentException("레지듀얼 시각 오류");
        foreach(var s in states.Values)if(!s.Inside&&s.End<=time+1e-9)s.Stacks=0;
    }
    public int Count(DamageType type,double time){Advance(time);return Get(type).Stacks;}
    private State Get(DamageType type)=>states.TryGetValue(type,out var s)?s:throw new ArgumentException("레지듀얼은 화염·냉기·전기·독성 중 하나입니다.");
    public void Enter(DamageType type,double time){Advance(time);Get(type).Inside=true;}
    public void Tick(DamageType type,double time,int areas=1)
    {
        if(areas<1)throw new ArgumentException("레지듀얼 틱 발생 영역 수 오류");
        Advance(time);var s=Get(type);if(!s.Inside)throw new ArgumentException("레지듀얼 틱에 앞서 해당 속성 영역 진입을 입력하세요.");s.Stacks=Math.Min(15,s.Stacks+areas);
    }
    public void Leave(DamageType type,double time){Advance(time);var s=Get(type);s.Inside=false;s.End=time+20;}
}
