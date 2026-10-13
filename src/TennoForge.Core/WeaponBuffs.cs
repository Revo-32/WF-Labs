namespace TennoForge.Core;

/// <summary>Received buff values, held for the selected encounter; not cast/ability simulation.</summary>
public record WeaponBuffs(double Roar=0,double Eclipse=0)
{
    public const string RoarSource="https://wiki.warframe.com/w/Roar?oldid=2816179";
    public const string EclipseSource="https://wiki.warframe.com/w/Eclipse?oldid=2816132";
    public bool Active=>Roar>0||Eclipse>0;
    public void Validate()
    {MeleeEngine.Range(Roar,0,1000,"실제 로어 버프");MeleeEngine.Range(Eclipse,0,1000,"실제 이클립스 버프");}
    public EffectContribution[] Contributions()=>[
        ..Roar>0?new[]{new EffectContribution("buff:roar","로어",0,"Roar","팩션 피해 가산",Roar,RoarSource)}:[],
        ..Eclipse>0?new[]{new EffectContribution("buff:eclipse","이클립스",0,"Eclipse","가산 컨오를 제외한 피해 배수",Eclipse,EclipseSource)}:[]];
}
