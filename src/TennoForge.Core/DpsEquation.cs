namespace TennoForge.Core;

public record DpsTerm(string Id,string Label,string Operator,double Value,string Detail);
public record DpsEquation(DpsTerm[] Terms,double Result,bool IsCombo,string[]? Limitations=null,bool DamageOnly=false,string? Context=null)
{
    public static DpsEquation FromTimeline(CombatTimelineResult result)
    {
        if(result.Unsupported.Length>0||result.Trials==0)throw new ArgumentException("완료된 전투 표본이 필요합니다.");
        var parts=new List<DpsTerm>{new("direct","누적 직접 피해","=",result.DirectDamage,"투사체 직격·부착·폭발을 포함한 실제 방어층 적용 후 직접 피해의 표본 평균"),new("status","누적 상태 피해","+",result.StatusDamage,"교전 구간에 실제 발생한 상태 피해 · 아직 발생하지 않은 미래 틱 제외")};
        if(result.Seconds>0)parts.Add(new("time","교전 시간","÷",result.Seconds,"선택한 전체 교전 구간 · 첫 접촉을 0초로 사용"));
        return new(parts.ToArray(),(result.DirectDamage+result.StatusDamage)/(result.Seconds>0?result.Seconds:1),false,Limitations:result.Notes.Where(n=>n.StartsWith("미검증:")||n.StartsWith("미반영:")).ToArray(),DamageOnly:result.Seconds==0,Context:result.Notes.Any(n=>n.StartsWith("미반영:"))?"일부 피해 성분 제외 · 아래 계산 범위 확인":result.Notes.Any(n=>n.StartsWith(ExaltedWaveFlight.Conditional))?ExaltedWaveFlight.Conditional:result.Notes.Any(n=>n.StartsWith(VitricaGlassState.Scope))?VitricaGlassState.Scope:result.Notes.Any(n=>n.StartsWith(InnodemFlight.Conditional))?InnodemFlight.Conditional:result.Notes.Any(n=>n.StartsWith(OrviusHoverState.Conflict))?"위키 데이터 표 조건부 계산 · 본문과 수치 상충 · 표본 평균":result.Notes.Any(n=>n.StartsWith("미검증:"))?"조건부 계산 · 명시한 사용자 조건/출처 분기 · 표본 평균":"입력한 교전 · 방어층·상태 전이 포함 · 표본 평균");
    }
    public double Evaluate()
    {
        var value=0d;
        foreach(var t in Terms)value=t.Operator switch {"="=>t.Value,"×"=>value*t.Value,"÷"=>value/t.Value,"+"=>value+t.Value,_=>throw new InvalidOperationException("계산식 연산자 오류")};
        return value;
    }
    public string[] ChangedTerms(DpsEquation other)=>Terms.Where(t=>other.Terms.SingleOrDefault(x=>x.Id==t.Id) is not {} previous||previous.Operator!=t.Operator||Math.Abs(t.Value-previous.Value)>1e-8*Math.Max(1,Math.Max(Math.Abs(t.Value),Math.Abs(previous.Value)))).Select(t=>t.Id).ToArray();
    public static DpsEquation From(Calculation calculation,StanceCalculation? stance=null)
    {
        if(calculation.Melee is null)throw new ArgumentException("확장 근접 계산 결과가 필요합니다.");
        var hits=stance is null?[(calculation,1)]:stance.Hits.Select(h=>(h.Calculation??throw new ArgumentException("타격별 근거 누락"),h.Count)).ToArray();
        var totals=new double[9];var extra=0d;var eclipseTotal=0d;
        foreach(var (c,count) in hits)
        {
            var m=c.Melee!;var b=m.Brackets??throw new ArgumentException("피해 브라켓 누락");
            var n=count*m.Projectiles;
            totals[0]+=b.BaseDamage*n;
            // The CO branch can exclude evolution damage, range falloff and combo independently.
            var additive=Math.Max(0,(b.BaseDamage*(1+b.AdditiveDamage+b.HeavyAttackDamage)+b.PostBaseDamage)*b.ComboMultiplier*b.RangeFalloff
                +Math.Max(0,b.BaseDamage-b.ConditionExcludedBase)*b.AdditiveConditionOverload*(b.ConditionUsesCombo?b.ComboMultiplier:1))*b.ConditionMultiplier;
            totals[1]+=additive*n;
            eclipseTotal+=Math.Max(0,(b.BaseDamage*(1+b.AdditiveDamage+b.HeavyAttackDamage)+b.PostBaseDamage)*b.EclipseMultiplier*b.ComboMultiplier*b.RangeFalloff
                +Math.Max(0,b.BaseDamage-b.ConditionExcludedBase)*b.AdditiveConditionOverload*(b.ConditionUsesCombo?b.ComboMultiplier:1))*b.ConditionMultiplier*n;
            totals[2]+=b.BeforeFaction*n;
            totals[3]+=m.ModdedBaseDamage*n;
            totals[4]+=c.Damage.Values.Sum()*n;
            var critical=c.Damage.Values.Sum()*MeleeStealth.Expected(c)*n;
            totals[5]+=critical;
            extra+=c.ExpectedHit*count-critical;
            totals[6]+=c.ExpectedHit*count;
            totals[7]+=c.TargetDps*c.HitInterval*count;totals[8]+=c.ExpectedHit*count*m.IncomingDamageMultiplier;
        }
        var parts=new List<DpsTerm>{new("base",stance is null?"기본 피해":"기본 피해 합","=",totals[0],stance is null?"무기 기본 피해 + 적용 가능한 진화 기본 피해":"각 타격의 기본 피해 × 타수 × 명중 투사체 수의 합")};
        void Factor(string id,string label,double before,double after,string detail)
        {parts.Add(before>0?new(id,label,"×",after/before,detail):new(id,label,"+",after,detail));}
        var first=hits[0].Item1.Melee!.Brackets!;
        var baseDetail=$"기본 {first.BaseDamage:G8} × (1 + 피해 {first.AdditiveDamage:G6} + 강공격 {first.HeavyAttackDamage:G6})\n이클립스 ×{first.EclipseMultiplier:G6} (가산 CO 제외) · 추가량 {first.PostBaseDamage:G6} · 가산 CO {first.AdditiveConditionOverload:G6}\nCO 제외 기본 피해 {first.ConditionExcludedBase:G6} · CO 별도 배수 {first.ConditionMultiplier:G6}\n콤보 {first.ComboMultiplier:G6} · 거리 감쇠 {first.RangeFalloff:G6}";
        Factor("damage","가산 · CO",totals[0],totals[1],(stance is null?"":"콤보 전체의 단계별 합계 비율입니다. 첫 타격 근거:\n")+baseDetail);
        if(hits.Any(h=>h.Item1.Melee!.Brackets!.EclipseMultiplier!=1))Factor("eclipse","이클립스",totals[1],eclipseTotal,"가산 컨오를 제외한 피해에 적용한 유효 배수입니다.\n컨테이전과 글레이브 폭발은 이클립스 두 번 적용\n상태 피해에는 이 단계 이외의 추가 배수를 곱하지 않습니다.");
        Factor("motion","동작 · 별도 배수",eclipseTotal,totals[2],"타격 배율 × 슬램 × 관통 유지 × 최종 피해 × 투척 피해\n텐노카이 피해 모드는 앞의 기본 피해 가산 단계에 포함\n각 타격의 적용 규칙으로 계산한 합계 비율 · 인노뎀 CO에는 동작/위치 배율 제외");
        Factor("faction","팩션",totals[2],totals[3],"1 + 대상 팩션 피해 모드 + 실제 받은 로어 증가율\n로어는 팩션 피해와 가산합니다.");
        Factor("types","원소 · 물리",totals[3],totals[4],"원소 조합과 물리 피해 보정 이후의 합계 비율\n물리 피해 모드는 상태 피해 기반을 증폭하지 않습니다.");
        var stealth=hits.Any(h=>h.Item1.Melee!.StealthBonus>0);
        Factor("critical",stealth?"치명타 · 은신":"치명타 기대",totals[4],totals[5],"1 + 은신 보너스 + 치명타 확률 × (치명 배수 − 1)\n은신은 치명타 보너스와 가산 · 30랭크에서 +700% (에테르 대거 +900%)\n듀플리케이트는 노란 치명타에서 발생하는 추가 공격까지 반영");
        if(extra>1e-8)parts.Add(new("extra","치명 이후 추가","+",extra,"에너지 채널처럼 치명타 배수를 받지 않는 추가 피해"));
        if(hits.Any(h=>Math.Abs(h.Item1.Melee!.IncomingDamageMultiplier-1)>1e-10))Factor("vulnerability","대상 취약도",totals[6],totals[8],"무기 피해와 별도 · 가산 취약 그룹과 독립 효과를 구분 · 상태 피해에 한 번 적용");
        Factor("target","방어력 · 대상",totals[8],totals[7],$"DR = 0.9 × √(A / 2700)\nA = 부식·화염 감소 후 방어력\n현재 DR = {calculation.ArmorReduction:P2}\n팩션 약점·저항 × 방어력 잔존 피해 × 바이러스\n실드에는 자성 적용 · 독성은 체력으로 우회\n오버가드는 방어력·팩션 약점 제외, 자성 적용\n실드·오버가드 파괴 이후는 TTK에서 별도로 계산");
        var limitations=calculation.Limitations.Concat(stance?.Stats.Limitations??[]).Distinct().Where(x=>x.StartsWith("미반영:")||x.StartsWith("미검증:")).ToList();
        if(stance is {Duration:null})return new(parts.ToArray(),totals[7],true,limitations.ToArray(),true);
        parts.Add(new("time",stance is null?"타격 간격":"콤보 시간","÷",stance?.Duration??calculation.HitInterval,stance is null?"사용자가 설정한 기본 간격 ÷ 최종 공격 속도":"위키 콤보 시간(또는 명시한 가정) ÷ 최종 공격 속도\n개별 타격 시각은 추정하지 않습니다."));
        var duration=stance?.Duration??calculation.HitInterval;
        if(extra>1e-8)limitations.Add("에너지 채널 충전이 있는 이번 입력의 피해/시간입니다. 저장량은 한 번 소비하며 지속 DPS·재충전은 시간별 교전에서 계산합니다.");
        return new(parts.ToArray(),totals[7]/duration,stance is not null,limitations.ToArray());
    }
}
