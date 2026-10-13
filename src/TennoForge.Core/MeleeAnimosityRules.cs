namespace TennoForge.Core;

public static class MeleeAnimosityRules
{
    public const string Source="https://wiki.warframe.com/w/Melee_Animosity?oldid=2784208";
    public static bool NonManualGlaiveThrow(string weaponId,string attack)
        =>MeleeRuleCatalog.Load().Weapon(weaponId).Raw.Text("Class")=="Glaive"&&attack.Contains("Throw",StringComparison.Ordinal)&&!attack.Contains("Recall",StringComparison.Ordinal);
    public static bool Receives(string weaponId,MeleeSetup setup,bool heavy)
        =>heavy||NonManualGlaiveThrow(weaponId,setup.Attack)&&!GlaiveCriticalRules.Airborne(setup);
    public static bool Builds(string weaponId,MeleeSetup setup)
        =>!MeleeEngine.AttackPolicy(weaponId,setup.Attack).Heavy&&!setup.Attack.Contains("Slam",StringComparison.Ordinal)&&!NonManualGlaiveThrow(weaponId,setup.Attack);
}
