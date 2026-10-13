namespace TennoForge.Core;

/// <summary>Four primary-element charges, spent by the next attack input.
/// Elemental percentages add to mods; blocked damage magnitude is irrelevant.</summary>
public sealed class AckBruntElementState
{
    public const string Source="https://wiki.warframe.com/w/Ack_%26_Brunt?oldid=2817288";
    public const string ShieldingMod="/Lotus/Upgrades/Mods/Melee/Event/ProjectNightwatch/AckBruntNightwatchMod";
    public static readonly DamageType[] Elements=[DamageType.Heat,DamageType.Cold,DamageType.Electricity,DamageType.Toxin];
    private readonly Dictionary<DamageType,int> charges=[];
    public int Total=>charges.Values.Sum();
    public AckBruntElementState(IReadOnlyDictionary<string,double>? conditions=null)
    {
        foreach(var type in Elements)
        {
            var value=conditions?.GetValueOrDefault("Block"+type)??0;
            MeleeEngine.Range(value,0,4,"흡수한 원소 충전");
            if(value!=Math.Truncate(value))throw new ArgumentException("흡수한 원소 충전은 정수입니다.");
            charges[type]=(int)value;
        }
        if(Total>4)throw new ArgumentException("흡수한 원소 충전 합계는4개까지입니다.");
    }
    public int Absorb(DamageType element,int count)
    {
        if(!Elements.Contains(element)||count<1||count>10000)throw new ArgumentException("흡수 원소 또는 막은 공격 횟수 오류");
        var accepted=Math.Min(4-Total,count);charges[element]+=accepted;return accepted;
    }
    public int Consume(){var count=Total;foreach(var t in Elements)charges[t]=0;return count;}
    public void WriteTo(IDictionary<string,double> conditions){foreach(var type in Elements)conditions["Block"+type]=charges[type];}
}
