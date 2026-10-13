namespace TennoForge.Core;

public record TimedIncarnonPerk(string Condition,string Trigger,double Seconds,string Label,string Source);

/// <summary>Only source-confirmed single-state timers. Stacking expiry rules are separate.</summary>
public static class IncarnonCombatPerks
{
    public static TimedIncarnonPerk[] ForBuild(Build build)
        =>ForSelections(build.WeaponId,build.Melee?.Evolutions??[]);
    public static TimedIncarnonPerk[] ForSelections(string weaponId,IEnumerable<string> selections)
    {
        var definition=IncarnonEngine.ForWeapon(weaponId);
        if(definition is null)return [];
        var selected=selections.ToHashSet();
        return definition.Perks.Where(p=>selected.Contains(p.Id)).Select(p=>(definition.Code,p.Condition) switch
        {
            ("NamiSolo","OnEquip")=>new TimedIncarnonPerk("OnEquip","EquipMelee",4,p.Name,"https://wiki.warframe.com/w/Nami_Solo_Incarnon_Genesis?oldid=2773651"),
            ("CeramicDagger","ShieldBreak")=>new("ShieldBreak","ShieldBroken",8,p.Name,"https://wiki.warframe.com/w/Ceramic_Dagger_Incarnon_Genesis?oldid=2773637"),
            ("AckBrunt","ShieldBreak")=>new("ShieldBreak","ShieldBroken",11,p.Name,"https://wiki.warframe.com/w/Ack_&_Brunt_Incarnon_Genesis?oldid=2787344"),
            ("Innodem","FinisherBuff")=>new("FinisherBuff","Finisher",40,p.Name,"https://wiki.warframe.com/w/Innodem?oldid=2816908"),
            ("Ruvox","FinisherBuff")=>new("FinisherBuff","Finisher",20,p.Name,"https://wiki.warframe.com/w/Ruvox?oldid=2816913"),
            ("Praedos","SlideKill")=>new("SlideKill","SlideMeleeKill",30,p.Name,"https://wiki.warframe.com/w/Praedos?oldid=2817314"),
            ("Destreza","HeavyKill")=>new("HeavyKill","HeavyMeleeKill",8,p.Name,"https://wiki.warframe.com/w/Destreza_Incarnon_Genesis?oldid=2816743"),
            ("DualIchor","NeutralFinalHit")=>new("NeutralFinalHit","NeutralFinalMove",33,p.Name,"https://wiki.warframe.com/w/Dual_Ichor_Incarnon_Genesis?oldid=2812453"),
            _=>null
        }).Where(p=>p is not null).Cast<TimedIncarnonPerk>().ToArray();
    }
}
