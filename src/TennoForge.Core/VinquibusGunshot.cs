namespace TennoForge.Core;

/// <summary>The melee shot's child attack is defined in the pinned weapon module.</summary>
public static class VinquibusGunshot
{
    public const string StanceSource="https://wiki.warframe.com/w/Harrowing_Spire?oldid=2795015";
    public static double Delay
    {
        get
        {
            var raw=MeleeRuleCatalog.Load().Weapon(VinquibusState.WeaponId).Raw.GetProperty("Attacks");
            var parent=raw.EnumerateArray().Single(a=>a.Text("AttackName")=="Gunshot");
            var child=raw.EnumerateArray().Single(a=>a.Text("AttackName")=="Gunshot Explosion");
            if(child.Number("AttackParentIndex")!=parent.Number("AttackIndex"))throw new InvalidOperationException("빈퀴부스 지연 폭발의 원본 부모 연결 오류");
            return child.Number("ExplosionDelay");
        }
    }
    public static bool Applies(string weaponId,string attack)=>weaponId==VinquibusState.WeaponId&&attack=="Gunshot";
}
