namespace TennoForge.Core;

// Collision times and travelled path lengths are scenario inputs. Gravity and
// an unspecified absolute projectile speed must not create invented timings.
public record ContagionCollision(double Time,int Projectile=1,int Target=1,double Travel=0,bool Surface=false);
public record ContagionEvent(double Time,int Projectile,int Target,double Travel,ContagionPhase Phase);

public sealed class ContagionFlightState
{
    private sealed record Flight(double Time,double Travel,bool Attached);
    private readonly Dictionary<int,Flight> flights=[];
    private readonly PriorityQueue<ContagionEvent,(double Time,long Order)> pending=new();
    private long order;
    public double NextTime=>pending.TryPeek(out _,out var priority)?priority.Time:double.PositiveInfinity;
    public static void Validate(ContagionCollision value,int targets)
    {
        MeleeEngine.Range(value.Time,0,300,"컨테이전 접촉 시각");MeleeEngine.Range(value.Travel,0,1000,"컨테이전 이동 거리");
        if(value.Projectile is <1 or >256||value.Target<1||value.Target>targets)throw new ArgumentException("투사체 번호는1~256, 대상 번호는 장면에 존재하는 적을 선택하세요.");
    }
    public ContagionEvent Contact(ContagionCollision value,int targets)
    {
        Validate(value,targets);
        if(flights.TryGetValue(value.Projectile,out var previous))
        {
            if(previous.Attached)throw new ArgumentException("이미 부착/폭발한 컨테이전 투사체입니다. 새 투사체에는 다른 번호를 지정하세요.");
            if(value.Time<previous.Time||value.Travel<previous.Travel)throw new ArgumentException("같은 투사체의 다음 충돌 시각과 누적 이동 거리가 이전보다 작습니다.");
        }
        flights[value.Projectile]=new(value.Time,value.Travel,value.Surface);
        return new(value.Time,value.Projectile,value.Target,value.Travel,value.Surface?ContagionPhase.Explosion:ContagionPhase.Contact);
    }
    public void ResolveContact(ContagionEvent contact,bool survived)
    {
        if(contact.Phase!=ContagionPhase.Contact)throw new ArgumentException("직격 결과에만 부착을 판정합니다.");
        if(!survived)return;
        flights[contact.Projectile]=new(contact.Time,contact.Travel,true);
        for(var second=1;second<=5;second++)
        {
            var e=contact with {Time=contact.Time+second,Phase=second==5?ContagionPhase.Explosion:ContagionPhase.Embedded};
            pending.Enqueue(e,(e.Time,order++));
        }
    }
    public ContagionEvent[] Due(double time)
    {
        var result=new List<ContagionEvent>();
        while(NextTime<=time+1e-9)result.Add(pending.Dequeue());
        return result.ToArray();
    }
}
