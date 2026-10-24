namespace TennoForge.Core;

/// <summary>Map a displayed damage component to the firing action that produces it.</summary>
public static class RangedAttackSelection
{
    public static int FiringAttack(ReferenceWeapon weapon,int selected)
    {
        var attacks=RangedEngine.Attacks(weapon);var current=selected;
        for(var step=0;step<attacks.Length;step++)
        {
            if(current<0||current>=attacks.Length)return selected;
            int? parent=null;
            if(RangedStug.Applies(weapon)&&current is 1 or 3 or 4 or 5 or 6)return current is 1 or 5?0:2;
            if(RangedAzima.Applies(weapon)&&current==1)return 2;
            if(weapon.Id==RangedVileDischarge.WeaponId&&current==2)return 1;
            if(RangedClusters.Get(weapon.Id) is {} cluster&&(current==cluster.Contact||current==cluster.Explosion))parent=cluster.Parent;
            else if(RangedReloadGrenades.Get(weapon.Id) is not null&&current>0)parent=0;
            else if(RangedQuanta.Applies(weapon)&&current>=2)parent=1;
            else if(RangedInfestedOrbs.Applies(weapon)&&current>=2)parent=1;
            else if((RangedSimulor.Applies(weapon)||RangedOrdinarySimulor.Applies(weapon))&&current>0)parent=0;
            else if(RangedReturningBlades.Get(weapon.Id) is not null&&current==1)parent=0;
            else if(RangedPentaAugments.Applies(weapon)&&current==2)parent=0;
            else if(RangedFields.Get(weapon.Id) is {} field&&field.Child==current)parent=field.Parents[0];
            else if(RangedAttackLinks.Rules.FirstOrDefault(r=>r.WeaponId==weapon.Id&&r.Child==current) is {} link)parent=link.Parent;
            else if(!(weapon.Id==RangedPullWeapons.Scyotid&&current==1)&&RangedEngine.N(attacks[current],"AttackParentIndex") is {} rawParent)
            {
                var index=Array.FindIndex(attacks,a=>RangedEngine.N(a,"AttackIndex")==rawParent);
                if(index>=0)parent=index;
            }
            if(parent is null||parent==current)return current;
            current=parent.Value;
        }
        return selected;
    }
}
