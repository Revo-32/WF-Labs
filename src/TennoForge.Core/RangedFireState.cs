namespace TennoForge.Core;

/// <summary>Mutable firing state so a reload/status buff changes the next firing interval, not past events.</summary>
public sealed class RangedFireState
{
    public double Next {get;private set;}
    public double Ammo {get;private set;}
    public int MagazineIndex {get;private set;}
    public int ShotIndex {get;private set;}
    public double LastReloadAt {get;private set;}=-1;
    public double RoundsReloaded {get;private set;}
    public bool LastReloadFromEmpty {get;private set;}
    public double PendingReloadStart {get;private set;}=double.PositiveInfinity;
    private double uninterruptedNext;
    private int burstShot;
    public int BurstShot=>burstShot;
    private double beamStartAt,beamInitial;
    private double beamAtShot;
    private double ammoInterval,efficiencyRemainder,lastEfficiency=-1;
    public RangedFireState(RangedCadence cadence){Next=cadence.Charge;Ammo=cadence.Magazine;beamInitial=cadence.BeamStart;}
    public void CompleteMagazineChange(double capacity)
    {
        if(!double.IsFinite(capacity)||capacity<=0)throw new ArgumentException("탄창 크기 오류");
        Ammo=capacity;
    }
    public void SwitchMode(RangedCadence next,double now,double ammo){PendingReloadStart=double.PositiveInfinity;LastReloadAt=-1;Ammo=ammo;ShotIndex=0;Equip(next,now);}
    public void ScheduleAt(double time){if(!double.IsFinite(time)||time<0)throw new ArgumentException("발사 예약 시각 오류");Next=time;}
    public void WaitUntil(double time){Next=Math.Max(Next,time);}
    public void Stop(){Next=double.PositiveInfinity;PendingReloadStart=double.PositiveInfinity;}
    public void Holster(double now)
    {
        Stop();if(LastReloadAt>now)LastReloadAt=-1;burstShot=0;
    }
    public void Equip(RangedCadence cadence,double now)
    {
        burstShot=0;Next=now+cadence.Charge;beamStartAt=Next;beamInitial=cadence.BeamStart;
        if(Ammo<cadence.AmmoCost&&!cadence.NoReload){Next=double.PositiveInfinity;PendingReloadStart=now;}
    }
    public void ResumeAfterPickup(RangedCadence cadence,double now)
    {
        if(!double.IsPositiveInfinity(Next)||double.IsFinite(PendingReloadStart))return;
        if(cadence.NoReload||Ammo>=cadence.AmmoCost)Next=now+cadence.Charge;
        else PendingReloadStart=now;
    }
    public void SetLoadedAmmo(double amount)
    {
        if(!double.IsFinite(amount)||amount<0)throw new ArgumentException("장전 탄약 오류");Ammo=amount;
    }
    public double PreviewConsumption(double cost,double efficiency)=>ConsumeAmmo(cost,efficiency,false);
    public void InstantReload(double capacity,double now)
    {
        CompleteMagazineChange(capacity);
        if(double.IsFinite(PendingReloadStart)||LastReloadAt>now){PendingReloadStart=double.PositiveInfinity;Next=Math.Max(now,uninterruptedNext);if(LastReloadAt>now)LastReloadAt=-1;}
    }
    public void RequestReload(double time){PendingReloadStart=time;Next=double.PositiveInfinity;burstShot=0;}
    public void BeginReload(RangedCadence cadence)
    {
        if(!double.IsFinite(PendingReloadStart))throw new InvalidOperationException("예약된 재장전이 없습니다.");
        LastReloadFromEmpty=Ammo<=1e-9;RoundsReloaded=cadence.Magazine-Ammo;
        LastReloadAt=PendingReloadStart+cadence.Reload;Next=LastReloadAt+cadence.Charge;
        MagazineIndex++;ShotIndex=0;burstShot=0;
        beamInitial=Math.Max(cadence.BeamStart,beamAtShot-(1-cadence.BeamStart)*Math.Max(0,cadence.Reload-.8)/2);beamStartAt=Next;
        PendingReloadStart=double.PositiveInfinity;
    }
    public RangedShotTime Consume(RangedCadence cadence,double efficiency)
    {
        if(!double.IsFinite(efficiency)||efficiency<0)throw new ArgumentException("탄약 효율 범위 오류");
        beamAtShot=cadence.Beam?Math.Min(1,beamInitial+(1-cadence.BeamStart)*(Next-beamStartAt)/cadence.BeamRampSeconds):1;
        Ammo=Math.Max(0,Ammo-ConsumeAmmo(cadence.AmmoCost,efficiency,true));
        return new(Next,MagazineIndex,ShotIndex,Ammo,beamAtShot);
    }
    private double ConsumeAmmo(double cost,double efficiency,bool commit)
    {
        efficiency=Math.Clamp(efficiency,0,1);if(efficiency==1)return 0;if(efficiency==0)return cost;
        // Wiki Ammo: deterministic shots per consumed round; fractional intervals survive reloads.
        var interval=lastEfficiency==efficiency?ammoInterval:0;var remainder=efficiencyRemainder;double consumed=0;
        while(cost>1e-9)
        {
            if(interval<=1e-9){var length=1/(1-efficiency)+remainder;interval=Math.Floor(length+1e-9);remainder=length-interval;}
            var used=Math.Min(cost,interval);interval-=used;cost-=used;if(interval<=1e-9)consumed++;
        }
        if(commit){lastEfficiency=efficiency;ammoInterval=interval;efficiencyRemainder=remainder;}
        return consumed;
    }
    public void Transform(RangedCadence next,double animation)
    {
        if(!double.IsFinite(animation)||animation<0)throw new ArgumentException("변신 시간 오류");
        Next+=animation+next.Charge;Ammo=next.Magazine;MagazineIndex++;ShotIndex=0;burstShot=0;
        PendingReloadStart=double.PositiveInfinity;
        beamInitial=next.BeamStart;beamStartAt=Next;
    }
    public void Advance(RangedCadence cadence,double efficiency,double? nextAmmoCost=null,bool deferReload=false)
    {
        ShotIndex++;burstShot++;
        var delay=cadence.BurstCount>1&&burstShot<cadence.BurstCount?cadence.BurstDelay:cadence.Delay(ShotIndex-1);
        if(burstShot>=cadence.BurstCount)burstShot=0;
        if((Ammo<=1e-9&&efficiency<1||Ammo+1e-9<ConsumeAmmo(nextAmmoCost??cadence.AmmoCost,efficiency,false))&&!cadence.NoReload)
        {
            if(deferReload)
            {
                PendingReloadStart=Next+(cadence.NoShotDelay?0:cadence.Delay(ShotIndex-1));
                uninterruptedNext=Next+delay+cadence.Charge;Next=double.PositiveInfinity;return;
            }
            var gap=(cadence.NoShotDelay?0:cadence.Delay(ShotIndex-1))+cadence.Reload;
            LastReloadFromEmpty=Ammo<=1e-9;RoundsReloaded=cadence.Magazine-Ammo;LastReloadAt=Next+gap;
            Next+=gap+cadence.Charge;Ammo=cadence.Magazine;MagazineIndex++;ShotIndex=0;burstShot=0;
            beamInitial=Math.Max(cadence.BeamStart,beamAtShot-(1-cadence.BeamStart)*Math.Max(0,gap-.8)/2);beamStartAt=Next;
        }
        else{if(cadence.NoReload)Ammo=cadence.Magazine;Next+=delay+cadence.Charge;}
    }
}
