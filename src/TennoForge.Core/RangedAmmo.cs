namespace TennoForge.Core;

/// <summary>Reserve ammunition is distinct from the magazine and Incarnon charge.</summary>
public sealed class RangedAmmo
{
    public static double Efficiency(IReadOnlyDictionary<string,double> effects)=>effects.GetValueOrDefault("InfiniteMagazine")>0?1:Math.Min(.75,effects.GetValueOrDefault("AmmoEfficiency"));
    public double Maximum {get;}
    public double Reserve {get;private set;}
    public bool Infinite {get;}
    public bool DirectReserve {get;}
    public RangedAmmo(ReferenceWeapon weapon,RangedResult result,double? initial=null,bool replenish=false)
    {
        var effects=result.Modifiers!;
        var native=RangedEngine.N(weapon.Raw,"AmmoMax")??0;
        var basis=effects.GetValueOrDefault("BaseAmmoCapacity",native);
        Maximum=Math.Max(0,Math.Round((basis+effects.GetValueOrDefault("BaseAmmoAdd"))*Math.Max(0,1+effects.GetValueOrDefault("AmmoMaximum")),MidpointRounding.AwayFromZero));
        Infinite=RangedHema.Applies(weapon)||effects.GetValueOrDefault("InfiniteAmmo")>0||replenish||RangedEngine.S(weapon.Raw,"ReloadStyle")=="Regenerate"||RangedEngine.S(weapon.Raw,"AmmoType")=="None";
        DirectReserve=weapon.WeaponType=="Bow"||result.Cadence!.NoReload&&RangedAlternates.Get(weapon.Id)?.NoAmmo!=true&&!RangedVileDischarge.Applies(weapon);
        if(initial is {} value&&(!double.IsFinite(value)||value<0||value>Maximum))throw new ArgumentException("시작 예비 탄약은 최대 보유량 이내로 입력하세요.");
        Reserve=initial??Maximum;
    }
    public double PickupDrop(ReferenceWeapon weapon,RangedResult result,string kind,double count,bool otherAmmoFull,bool holstered)
    {
        if(!double.IsFinite(count)||count<1||count!=Math.Floor(count))throw new ArgumentException("탄약 상자 개수는 1 이상의 정수여야 합니다.");
        if(RangedEngine.S(weapon.Raw,"ReloadStyle")=="Regenerate")return 0;
        var pickup=RangedEngine.N(weapon.Raw,"AmmoPickup")??(weapon.WeaponType switch
        {
            "Rifle" or "Crossbow"=>80,"Speargun"=>60,"Sniper Rifle" or "Bow"=>15,
            "Shotgun Sidearm" or "Dual Shotguns"=>20,"Pistol" or "Dual Pistols" or "Thrown"=>40,
            _=>throw new CombatRuleUnavailableException("이 무기의 탄약 습득량 원문 수치를 연결해야 합니다.")
        });
        var matches=kind=="UniversalAmmoDrop"||kind==(weapon.Category=="primary"?"PrimaryAmmoDrop":"SecondaryAmmoDrop");
        if(!matches)
        {
            if(holstered||!otherAmmoFull)return 0;
            // Primed Sniper Ammo Mutation's explicit 1-ammo example documents rounding up.
            pickup=Math.Ceiling(pickup*result.Modifiers!.GetValueOrDefault("AmmoMutation")-1e-9);
        }
        return Pickup(Math.Max(0,pickup)*count);
    }
    public double Spend(double requested)
    {
        if(!double.IsFinite(requested)||requested<0)throw new ArgumentException("탄약 소비량 오류");
        if(Infinite)return requested;
        var used=Math.Min(requested,Reserve);Reserve-=used;return used;
    }
    public double Pickup(double amount)
    {
        if(!double.IsFinite(amount)||amount<0)throw new ArgumentException("탄약 획득량 오류");
        var before=Reserve;Reserve=Math.Min(Maximum,Reserve+amount);return Reserve-before;
    }
}
