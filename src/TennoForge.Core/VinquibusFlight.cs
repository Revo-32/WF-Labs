namespace TennoForge.Core;

/// <summary>Explicit collision path; no unspecified projectile speed or impact time is synthesized.</summary>
public static class VinquibusFlight
{
    public const string ModuleSource="https://wiki.warframe.com/w/Module:Weapons/doc?oldid=2797286";
    public static double Motion(WeaponActionInput input)=>input.Sequence==2?3:1;
    public static WeaponActionEvent[] Schedule(WeaponActionInput input,int targets)
    {
        if(input.Mode!="Special"||input.Sliding)throw new ArgumentException("빈퀴부스 사격 입력의 동작 오류");
        var path=(input.Contacts??[]).OrderBy(c=>c.Time).ToArray();
        if(path.Length is <1 or >2)throw new ArgumentException("빈퀴부스 사격은 처치 관통1회까지입니다. 첫 접촉과 필요하면 후속 접촉을 지정하세요.");
        foreach(var c in path)
        {
            MeleeEngine.Range(c.Time,input.Launch,input.Launch+VinquibusGunshot.Delay,"사격 접촉 · 발사 후0.8초 이내");
            MeleeEngine.Range(c.Target,1,targets,"접촉 대상/표면 위치");MeleeEngine.Range(c.Travel,0,1000,"누적 비행 거리");
            if(c.Blade||c.Explosion||c.Projectile!=1||c.PenetratedMeters!=0||c.Launched is not null)throw new ArgumentException("빈퀴부스 한 발의 적 직격 또는 표면 충돌을 지정하세요. 폭발은 발사 후0.8초에 자동 계산합니다.");
        }
        if(path.Length==2&&(path[0].Surface||path[1].Time<=path[0].Time||path[1].Travel<path[0].Travel||!path[1].Surface&&path[1].Target==path[0].Target))throw new ArgumentException("빈퀴부스 관통은 처치한 첫 대상 이후의 서로 다른 후속 접촉입니다.");
        return [new(input.Start,0,input),..path.Select(c=>new WeaponActionEvent(c.Time,1,input,c)),new(input.Launch+VinquibusGunshot.Delay,2,input,path[^1])];
    }
    public static Calculation Calculate(Catalog catalog,Build build,WeaponActionInput input,bool explosion,double distance)
        =>MeleeEngine.CalculateAttack(catalog,build with {Stance=null,Scenario=build.Scenario with {MotionMultiplier=Motion(input)},Melee=build.Melee! with {Attack=explosion?"Gunshot Explosion":"Gunshot",Distance=distance,TargetIndex=1,Tennokai=false,Finisher=null}},useInitialComboFloor:false);
}

public sealed class VinquibusProjectileState
{
    public double? Precision {get;set;}
    public int KilledContacts {get;private set;}
    public bool Stopped {get;private set;}
    public int? AttachedTarget {get;private set;}
    public void Contact(int target,bool aliveBefore,bool aliveAfter,bool surface)
    {
        if(Stopped)throw new ArgumentException("빈퀴부스 탄환은 생존 대상 또는 표면에 멈췄으므로 후속 관통 접촉을 처리할 수 없습니다.");
        if(surface){Stopped=true;return;}
        if(!aliveBefore)throw new CombatRuleUnavailableException("빈퀴부스 도착 전에 대상이 이미 처치되었습니다. 살아 있는 대상 또는 실제 표면 접촉을 지정하세요.");
        if(aliveAfter){Stopped=true;AttachedTarget=target;return;}
        KilledContacts++;
        if(KilledContacts>=2)Stopped=true;
    }
}
