using TennoForge.Core;

internal static class UnresolvedComponentChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();
        void Yes(bool v){if(!v)throw new Exception("성분 근거 구분 실패");}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-8)throw new Exception($"{a} != {b}");}
        Build B(string id)=>new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Attack:"Heavy Attack"));
        check("패소시스트:일반형0확률과미기재피해구분·코다에복사하지않음",()=>{
            var normal=PathocystMaggots.Describe(B(PathocystMaggots.Normal));var coda=PathocystMaggots.Describe(B(PathocystMaggots.Coda));
            Near(normal.Lifetime,7);Near(normal.CriticalChance!.Value,0);Near(normal.StatusChance!.Value,0);Yes(normal.Damage is null&&normal.Radius is null&&normal.Level==30);Yes(coda.CriticalChance is null&&coda.StatusChance is null&&coda.Level is null);
            var b=B(PathocystMaggots.Normal);b=b with {Melee=b.Melee! with {Attack="Normal Attack"}};Yes(MeleeEngine.Calculate(catalog,b).Limitations.Any(n=>n.StartsWith("미반영:")&&n.Contains("마고트")));
            var r=CombatTimeline.Calculate(catalog,b with {Encounter=new(EncounterMode.FiveSeconds)},StanceCatalog.Load(),1);Yes(r.Unsupported.Length==0&&DpsEquation.FromTimeline(r).Limitations!.Any(n=>n.Contains("마고트")));
        });
        check("삼포테스/엑제크:직격기본피해를파동에전용하지않음·형상과예외구분",()=>{
            foreach(var id in new[]{UnresolvedMeleeComponents.Exec,UnresolvedMeleeComponents.Sampotes})foreach(var heavy in new[]{false,true})
            {
                var p=UnresolvedMeleeComponents.Pattern(id,heavy);Yes(p.BaseDamage is null&&p.ComboMods&&!p.ConditionOverload);Near(p.Spacing,5);
                if(id==UnresolvedMeleeComponents.Sampotes){Near(p.ExplosionCount,heavy?12:8);Near(p.Radius!.Value,heavy?7:5);Near(p.StatusChance!.Value,.1);Yes(!p.HeavyCombo&&p.Falloff is null);}
                else {Near(p.ExplosionCount,heavy?9:3);Near(p.Falloff!.Value,heavy?.5:.7);Yes(p.HeavyCombo==heavy&&p.Radius is null&&p.StatusChance is null);}
                var b=B(id);Yes(UnresolvedMeleeComponents.Describe(b,MeleeEngine.Calculate(catalog,b)).All(s=>s.Damage is null));
            }
        });
        check("아조테인:85와슬램50%원문충돌분리·슬램피해만적용",()=>{
            var b=B(UnresolvedMeleeComponents.Azothane);var c=MeleeEngine.Calculate(catalog,b);Near(UnresolvedMeleeComponents.AzothaneHeat(c,false),85);Near(UnresolvedMeleeComponents.AzothaneHeat(c,true),170);
            foreach(var (name,index) in new[]{("Seismic Wave",0),("Pressure Point",1),("Killing Blow",2),("Fever Strike",3)}){var m=catalog.Mods.Single(m=>m.EnglishName==name);b.Slots[index]=new(m.Id,m.MaxRank);}
            c=MeleeEngine.Calculate(catalog,b);Near(UnresolvedMeleeComponents.AzothaneHeat(c,false),255);Near(UnresolvedMeleeComponents.AzothaneHeat(c,true),510);
        });
        check("스태틱 디스차지:최대랭크시작동작별24/800·80/240·4m·0.2초",()=>{
            var p=UnresolvedMeleeComponents.Discharge(5);Near(p.SpinFirstTick,.24);Near(p.OverheadFollowup,8);Near(p.OverheadFirstTick,.8);Near(p.SpinFollowup,2.4);Near(p.Radius,4);Near(p.TickInterval,.2);
            var b=B(catalog.Weapons.Single(w=>w.EnglishName=="Prova").Id);var m=catalog.Mods.Single(m=>m.EnglishName=="Static Discharge");b.Slots[0]=new(m.Id,m.MaxRank);var entries=UnresolvedMeleeComponents.Describe(b,MeleeEngine.Calculate(catalog,b));Yes(entries.Length==2&&entries.All(e=>e.Damage is null&&e.Note.Contains("미기재")));
        });
    }
}
