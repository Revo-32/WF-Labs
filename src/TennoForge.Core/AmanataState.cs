namespace TennoForge.Core;

/// <summary>One current blessing; 30 qualifying contacts replace it. The caller
/// supplies the die outcome, so the state rules do not assume its distribution.</summary>
public sealed class AmanataState
{
    public const string Source="https://wiki.warframe.com/w/Amanata?oldid=2789907";
    public const string PressureSource="https://wiki.warframe.com/w/Amanata_Pressure?oldid=2782354";
    public static readonly DamageType[] PressureTypes=[DamageType.Impact,DamageType.Puncture,DamageType.Slash,DamageType.Heat,DamageType.Cold,DamageType.Electricity,DamageType.Toxin,DamageType.Blast,DamageType.Radiation,DamageType.Gas,DamageType.Magnetic,DamageType.Viral,DamageType.Corrosive];
    public int Roll {get;private set;}
    public int Contacts {get;private set;}
    public int Rerolls {get;private set;}
    private long pressureInput=-1,slamInput=-1;
    public AmanataState(int initialRoll,int contacts=0)
    {
        MeleeEngine.Range(initialRoll,1,6,"아마나타 시작 주사위");MeleeEngine.Range(contacts,0,29,"다음 주사위까지 누적 명중");
        Roll=initialRoll;Contacts=contacts;
    }
    public bool Contact(long input,bool slam,bool connected,bool duplicate,Func<int> draw)
    {
        // Wiki explicitly records a slam counting even without an enemy.
        if(duplicate)return false;
        if(slam){if(slamInput==input)return false;slamInput=input;}
        else if(!connected)return false;
        Contacts++;if(Contacts<30)return false;
        var result=draw();MeleeEngine.Range(result,1,6,"아마나타 주사위 결과");
        Contacts=0;Roll=result;Rerolls++;return true;
    }
    public bool Pressure(long input,bool heavy,double combo,bool connected)
    {
        if(!connected||!heavy||combo<8||pressureInput==input)return false;
        pressureInput=input;return true;
    }
}
