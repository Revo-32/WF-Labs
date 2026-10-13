namespace TennoForge.Core;

/// <summary>Shared rifle/bayonet charges. Melee contacts of one input consume once.</summary>
public sealed class VinquibusState
{
    public const string WeaponId="/Lotus/Weapons/Tenno/Bayonet/TnBayonetRifleWeapon";
    public const string Source="https://wiki.warframe.com/w/Vinquibus?oldid=2811587";
    public int Precision {get;private set;}
    public int Ferocity {get;private set;}
    private long? consumedInput;
    public VinquibusState(int precision=0,int ferocity=0)
    {
        if(precision is <0 or >16||ferocity is <0 or >16)throw new ArgumentException("빈퀴부스 충전은 0~16입니다.");
        Precision=precision;Ferocity=ferocity;
    }
    public void Headshots(int count=1){if(count<1)throw new ArgumentException("헤드샷 수 오류");Precision=(int)Math.Min(16,Precision+2L*count);}
    public void MeleeKill(){Ferocity=Math.Min(16,Ferocity+4);}
    public bool PrimaryShot(){var efficient=Ferocity>0;Ferocity=Math.Max(0,Ferocity-1);return efficient;}
    public void FinishMeleeInput(long input,bool connected)
    {
        if(!connected||consumedInput==input)return;
        consumedInput=input;Precision=Math.Max(0,Precision-1);
    }
}
