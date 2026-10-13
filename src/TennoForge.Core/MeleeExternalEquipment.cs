namespace TennoForge.Core;

public record MeleeExternalEquipment(string Primary="None",int Rank=5,bool JetStream=false,int JetStreamRank=3,double JetStreamStrength=1,WarcrySetup? Warcry=null,MetronomeSetup? Metronome=null,double? JetStreamRemaining=null,HallOfMirrorsSetup? HallOfMirrors=null,AtlasRubbleSetup? AtlasRubble=null);

public static class MeleeExternalRules
{
    public const string DaikyuSource="https://wiki.warframe.com/w/Amalgam_Daikyu_Target_Acquired?oldid=2800299";
    public const string ArgonakSource="https://wiki.warframe.com/w/Amalgam_Argonak_Metal_Auger?oldid=2807661";
    public const string JetStreamSource="https://wiki.warframe.com/w/Jet_Stream?oldid=2811808";
    public static EffectContribution[] Effects(string weaponClass,MeleeExternalEquipment? equipment)
    {
        if(equipment is null)return [];
        if(equipment.Primary is not ("None" or "Daikyu" or "Argonak"))throw new ArgumentException("외부 주무기 선택 오류");
        MeleeEngine.Range(equipment.Rank,0,5,"아말감 모드 랭크");MeleeEngine.Range(equipment.JetStreamRank,0,3,"제트 스트림 랭크");MeleeEngine.Range(equipment.JetStreamStrength,0,20,"제트 스트림 시전자 위력");
        if(equipment.AtlasRubble is {} rubble)AtlasRubbleState.Validate(rubble);
        var list=new List<EffectContribution>();
        if(equipment.Warcry is {} warcry)
        {
            WarcryRules.Validate(warcry);
            if(WarcryRules.Active(warcry))list.Add(new("external:warcry","워크라이",warcry.Rank,"AttackSpeed","공격 속도",WarcryRules.Speed(warcry),WarcryRules.Source));
        }
        if(equipment.Metronome is {} metronome)
        {
            MetronomeRules.Validate(metronome);
            if(MetronomeRules.Forte(metronome)>0)list.Add(new("external:forte","포르테",metronome.Rank,"Damage","기본 피해 가산",MetronomeRules.Forte(metronome),MetronomeRules.Source));
        }
        if(equipment.Primary=="Daikyu"&&weaponClass=="Nikana")list.Add(new("external:daikyu","아말감 다이큐 타겟 어콰이어드",equipment.Rank,"NikanaLifeSteal","회복",.005*(equipment.Rank+1),DaikyuSource));
        if(equipment.Primary=="Argonak"&&weaponClass=="Dagger")list.Add(new("external:argonak","아말감 아르고나크 메탈 어거",equipment.Rank,"DaggerArmorStrip","적 기본 방어력",equipment.Rank+1,ArgonakSource));
        if(equipment.JetStreamRemaining is {} left)MeleeEngine.Range(left,0,3,"제트 스트림 시작 잔여 시간");
        if(equipment.JetStream&&(equipment.JetStreamRemaining??1)>0)list.Add(new("external:jetstream","제트 스트림",equipment.JetStreamRank,"ProjectileSpeed","투사체 속도",new[]{.5,.65,.8,1}[equipment.JetStreamRank]*equipment.JetStreamStrength,JetStreamSource));
        return list.ToArray();
    }
    // These weapon articles explicitly tie maximum travel to projectile speed.
    // Do not turn an arbitrary weapon's fixed maximum range into a lifetime.
    internal static bool SpeedExtendsRange(string id)=>id is WeaponStanceMechanic.Nepheri or WeaponStanceMechanic.Grigori or WeaponStanceMechanic.Verdilac;
}
