namespace TennoForge.Core;

/// <summary>Range entry/exit is supplied by the scenario; no player path is inferred.</summary>
public sealed class JetStreamTimeline
{
    private readonly List<(double Time,bool Active)> changes=[];
    public JetStreamTimeline(MeleeExternalEquipment equipment,CombatInput[]? actions=null)
    {
        if(equipment.JetStreamRemaining is {} left)MeleeEngine.Range(left,0,3,"제트 스트림 범위 밖 잔여 시간");
        var inside=equipment.JetStream&&equipment.JetStreamRemaining is null;
        changes.Add((0,equipment.JetStream&&(equipment.JetStreamRemaining??1)>0));
        if(equipment.JetStreamRemaining is >0 and var remaining)changes.Add((remaining,false));
        foreach(var a in (actions??[]).OrderBy(a=>a.Time))
        {
            if(a.Kind is not ("JetStreamEnter" or "JetStreamLeave" or "JetStreamLost" or "AbilityNullified" or "FallOutOfBounds"))continue;
            if(!equipment.JetStream)continue;
            MeleeEngine.Range(a.Time,0,300,"제트 스트림 행동 시각");
            if(a.Kind=="JetStreamLeave")
            {
                if(!inside)throw new ArgumentException("제트 스트림 범위 밖에 있는 상태입니다. 다시 진입한 뒤 이탈을 지정하세요.");
                inside=false;changes.RemoveAll(c=>c.Time>a.Time);changes.Add((a.Time+3,false));
            }
            else
            {
                inside=a.Kind=="JetStreamEnter";changes.RemoveAll(c=>c.Time>=a.Time);changes.Add((a.Time,inside));
            }
        }
        changes.Sort((a,b)=>a.Time.CompareTo(b.Time));
    }
    public bool Active(double time)=>changes.LastOrDefault(c=>c.Time<=time).Active;
    public MeleeExternalEquipment At(MeleeExternalEquipment source,double time)=>source with {JetStream=Active(time),JetStreamRemaining=null};
    public double NextTransition(double time)=>changes.Where(c=>c.Time>time+1e-9).Select(c=>c.Time).DefaultIfEmpty(double.PositiveInfinity).Min();
    public void RequireConstantFlight(double launch,double impact)
    {
        var initial=Active(launch);
        if(changes.Any(c=>c.Time>launch+1e-9&&c.Time<=impact+1e-9&&c.Active!=initial))
            throw new CombatRuleUnavailableException("제트 스트림이 비행 도중 바뀌는 경우 이미 발사된 투사체 속도의 저장/갱신 규칙이 미기재입니다. 버프가 일정한 비행 구간은 위키 속도·감쇠 거리로 계산합니다. "+MeleeExternalRules.JetStreamSource);
    }
    public void ValidateFlights(EncounterSetup settings)
    {
        foreach(var f in settings.WeaponActions??[])foreach(var c in f.Contacts??[])if(!c.Blade&&!c.Surface)RequireConstantFlight(c.Launched??f.Launch,c.Time);
        foreach(var f in settings.GlaiveFlights??[])RequireConstantFlight(f.Launch,f.Returned);
        foreach(var f in settings.ShieldFlights??[])RequireConstantFlight(f.Launch,f.Returned);
        foreach(var f in settings.ExaltedWaves??[])RequireConstantFlight(f.Launch,f.Impact);
        foreach(var f in settings.IncarnonBlades??[])RequireConstantFlight(f.Launch,f.Impact);
        foreach(var f in settings.InnodemContacts??[])RequireConstantFlight(f.Launch,f.Impact);
        foreach(var f in settings.CeramicDaggers??[])RequireConstantFlight(f.Launch,f.Impact);
        foreach(var f in settings.ZenistarDiscs??[])RequireConstantFlight(f.Launch,f.Land);
    }
}
