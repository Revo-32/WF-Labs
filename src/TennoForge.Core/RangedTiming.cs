using System.Reflection;
using System.Text.Json;

namespace TennoForge.Core;

public record RangedTimingRule(string Id,WikiSource? Source,double? SpoolStart,double? SpoolStep,double? ShellDelay,double? ShellTime,double BeamStart,bool NoReload,bool NoShotDelay,double? ReloadOverride,Dictionary<int,string> TriggerOverrides,int PelletSpoolMaximum=0,double PelletAmmoDivisor=0,Dictionary<int,double>? ChargeOverrides=null,Dictionary<int,double>? BeamStartOverrides=null,double BeamRampSeconds=.6);
public record RangedTimingCatalog(WikiSource[] Sources,RangedTimingRule[] Weapons)
{
    public static readonly RangedTimingCatalog Data=Load();
    private static RangedTimingCatalog Load(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TennoForge.Core.ranged-timing.json")!;return JsonSerializer.Deserialize<RangedTimingCatalog>(s,Catalog.Json)!;}
}
public record RangedShotTime(double Time,int MagazineIndex,int ShotIndex,double AmmoAfter,double BeamScale);
public record RangedCadence(string Trigger,double Rate,double Charge,double ShotDelay,int BurstCount,double BurstDelay,double AmmoCost,int Magazine,double Reload,bool Beam,double BeamStart,double? SpoolStart,double? SpoolStep,bool NoReload,bool NoShotDelay,string[] Issues,double SpoolFloor=.05,double BeamRampSeconds=.6)
{
    public double Delay(int shot)=>SpoolStart is {} initial&&SpoolStep is {} step?1/(Rate*Math.Clamp(initial+step*shot,SpoolFloor,1)):ShotDelay;
    public IEnumerable<RangedShotTime> Shots(double horizon,CancellationToken token=default)
    {
        if(Issues.Length>0)throw new CombatRuleUnavailableException(string.Join("\n",Issues));
        if(!double.IsFinite(horizon)||horizon<=0||horizon>300)throw new ArgumentException("교전 시간은 0~300초 범위입니다.");
        double time=Charge,ammo=Magazine,beamStartAt=0,beamAtStart=BeamStart;int cycle=0,shot=0,burstShot=0;
        while(time<=horizon)
        {
            token.ThrowIfCancellationRequested();
            var scale=Beam?Math.Min(1,beamAtStart+(1-BeamStart)*(time-beamStartAt)/BeamRampSeconds):1;
            ammo-=AmmoCost;yield return new(time,cycle,shot,Math.Max(0,ammo),scale);shot++;burstShot++;
            var delay=BurstCount>1&&burstShot<BurstCount?BurstDelay:Delay(shot-1);
            if(burstShot>=BurstCount)burstShot=0;
            if(ammo+1e-9<AmmoCost&&!NoReload)
            {
                var gap=(NoShotDelay?0:Delay(shot-1))+Reload;time+=gap+Charge;cycle++;shot=0;burstShot=0;ammo=Magazine;
                beamAtStart=Math.Max(BeamStart,scale-(1-BeamStart)*Math.Max(0,gap-.8)/2);beamStartAt=time;
            }
            else{if(NoReload)ammo=Magazine;time+=delay+Charge;}
        }
    }
    public double MagazineCycle()
    {
        if(Issues.Length>0)return double.NaN;
        int shots=(int)Math.Floor(Magazine/AmmoCost);double duration=0;
        for(int i=0;i<shots;i++)duration+=Charge+(i==shots-1?(NoShotDelay?0:Delay(i)):BurstCount>1&&(i+1)%BurstCount!=0?BurstDelay:Delay(i));
        return duration+(NoReload?0:Reload);
    }
}
public static class RangedTiming
{
    public static RangedCadence Calculate(ReferenceWeapon w,JsonElement attack,double fireBonus,double reloadBonus,double magazineBonus,double baseMagazineBonus=0,double duplexReleaseDelay=0)
    {
        var rule=(RangedTimingCatalog.Data.Weapons.SingleOrDefault(x=>x.Id==w.Id)??RangedKitguns.Timing(w.Id));var issues=new List<string>();
        var trigger=RangedEngine.S(attack,"Trigger");if(trigger=="")trigger=RangedEngine.S(w.Raw,"Trigger");
        var index=(int)(RangedEngine.N(attack,"AttackIndex")??1)-1;
        trigger=rule.TriggerOverrides.GetValueOrDefault(index,trigger);
        if(trigger=="Active"&&RangedDeployments.Get(w.Id)?.FireModes.Contains(index)==true)trigger="Semi-Auto";
        if(RangedSimulor.Applies(w)||RangedOrdinarySimulor.Applies(w))trigger="Semi-Auto";
        if(trigger=="Semi")trigger="Semi-Auto";
        if(trigger=="Auto-Burst")trigger="Burst";
        if(trigger=="Charged Auto")trigger="Auto-Charge";
        var rawRate=RangedEngine.N(attack,"FireRate");var rawCharge=rule.ChargeOverrides?.GetValueOrDefault(index)??0;
        if(rawCharge==0)rawCharge=RangedEngine.N(attack,"ChargeTime")??0;
        var chargeOnly=rule.NoShotDelay&&rawCharge>0;
        if(chargeOnly&&rawRate is null)rawRate=1/rawCharge;
        var magazine=Math.Max(1,(int)Math.Round(((RangedEngine.N(w.Raw,"Magazine")??1)+baseMagazineBonus)*(1+magazineBonus),MidpointRounding.AwayFromZero));
        if(RangedEngine.N(attack,"IncarnonCharges") is {} charges)magazine=(int)charges;
        if(w.WeaponType=="Bow"||rule.NoReload)magazine=(int)(RangedEngine.N(w.Raw,"Magazine")??1);
        var bow=w.WeaponType=="Bow";var speed=1+fireBonus*(bow?2:1);var rate=Math.Max(.05,(rawRate??0)*speed);
        if(trigger=="Semi-Auto")rate=Math.Min(10,rate);
        if(rawRate is null or <=0)issues.Add("위키 발사 속도 수치 확인 필요");
        var charge=rawCharge/Math.Max(.1,speed);var burst=(int)(RangedEngine.N(attack,"BurstCount")??1);var burstDelay=(RangedEngine.N(attack,"BurstDelay")??0)/Math.Max(1,1+fireBonus);
        if(trigger=="Mag Burst")burst=magazine;
        if(trigger=="Duplex"){burst=2;burstDelay=duplexReleaseDelay;}
        if(burst<1)issues.Add("점사 발수 오류");
        if(trigger.Contains("Burst")&&burst>1&&RangedEngine.N(attack,"BurstDelay") is null)issues.Add("점사 내부 발사 간격 원문 확인 필요");
        if(trigger.Contains('/')||trigger is "Active" or "Auto-Burst" or "Charged Auto")issues.Add("공격별 입력 동작 시간 연결 필요: "+trigger);
        if(trigger.Contains("Charge")&&rawCharge<=0)issues.Add("충전 시간 원문 확인 필요");
        if(trigger=="Auto-Spool"&&(rule.SpoolStart is null||rule.SpoolStep is null))issues.Add("발사 가속 시작 속도·증가율 원문 확인 필요");
        var reloadFactor=1+reloadBonus;if(reloadFactor<=0)throw new ArgumentException("재장전 속도 합계가 0 이하입니다.");
        var reload=(RangedEngine.N(attack,"Reload")??rule.ReloadOverride??RangedEngine.N(w.Raw,"Reload")??0)/reloadFactor;
        switch(RangedEngine.S(w.Raw,"ReloadStyle"))
        {
            case "Regenerate":
                var recharge=RangedEngine.N(attack,"ReloadRate")??RangedEngine.N(w.Raw,"ReloadRate");var delay=RangedEngine.N(attack,"ReloadDelayEmpty")??RangedEngine.N(attack,"ReloadDelay")??RangedEngine.N(w.Raw,"ReloadDelayEmpty")??RangedEngine.N(w.Raw,"ReloadDelay");
                if(recharge is null or <=0||delay is null)issues.Add("배터리 충전 수치 원문 확인 필요");
                else reload=Math.Max(.5,delay.Value/reloadFactor)+magazine/recharge.Value;break;
            case "ByRound":
                if(rule.ShellDelay is null||rule.ShellTime is null)issues.Add("탄당 재장전·시작/종료 지연 원문 확인 필요");
                else reload=(rule.ShellDelay.Value+rule.ShellTime.Value*magazine)/reloadFactor;break;
        }
        var ammo=RangedEngine.N(attack,"AmmoCost")??1;var alternate=RangedAlternates.Get(w.Id);if(alternate is {NoAmmo:true}&&alternate.Alternate.Contains(index)){ammo=1;}if(RangedPassives.Get(w.Id)?.Kind=="Cedo"&&index is 1 or 2)ammo=1;if(ammo<=0||ammo>magazine)issues.Add("공격 탄약 소비 원문 확인 필요");
        var noDelay=rule.NoShotDelay||bow;var shotDelay=rule.NoReload?Math.Max(0,1/rate-charge):bow||rule.NoShotDelay&&rawCharge>0?0:1/rate;
        if(trigger=="Duplex")shotDelay=Math.Max(0,1/rate-duplexReleaseDelay);
        if(rule.NoReload)reload=0;
        return new(trigger,rate,charge,shotDelay,burst,burstDelay,ammo,magazine,reload,trigger=="Held",rule.BeamStartOverrides?.GetValueOrDefault(index,rule.BeamStart)??rule.BeamStart,trigger=="Auto-Spool"?rule.SpoolStart:null,trigger=="Auto-Spool"?rule.SpoolStep:null,rule.NoReload,noDelay,issues.ToArray(),BeamRampSeconds:rule.BeamRampSeconds);
    }
}
