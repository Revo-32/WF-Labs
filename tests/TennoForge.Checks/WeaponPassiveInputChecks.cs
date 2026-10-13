using TennoForge.Core;

internal static class WeaponPassiveInputChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string name){var w=catalog.Weapons.Single(w=>w.EnglishName==name);return new(w.Id,new ModSlot?[8],new(Combo:12,BaseInterval:10,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(),Encounter:new(EncounterMode.TenSeconds));}
        void Yes(bool v){if(!v)throw new Exception("무기 고유 입력 검증 실패");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-7*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        check("케라티노스:본문7타와표8타·11/14배·출처분기명시",()=>{
            var b=B("Keratinos");var w=catalog.Weapons.Single(w=>w.Id==b.WeaponId);
            foreach(var s in stances.ForWeapon(w))foreach(var key in new[]{"Heavy","Heavy Article"})
            {
                var c=StanceEngine.Calculate(catalog,b with {Stance=new(s.Id,key,stances.Revision),Capacity=CapacityCatalog.Load().Default(w.Id,s.Id)},stances);
                Near(c.Hits.Sum(h=>h.Count),key=="Heavy"?8:7);Near(c.Hits.Sum(h=>h.Damage.Values.Sum()*h.Count),w.Damage.Values.Sum()*12*(key=="Heavy"?14:11));Yes(c.Stats.Limitations.Any(n=>n.StartsWith("미검증:")));Yes(DpsEquation.From(c.Stats,c).Limitations!.Any(n=>n.Contains("케라티노스")));
                if(key=="Heavy Article")Near(c.Hits.Where(h=>h.Calculation!.Melee!.ForcedProcs.Contains("Slash")).Sum(h=>h.Count),4);
            }
        });
        check("케라티노스:12배허공강공격도180초사거리·11배는발동안함",()=>{
            var b=B("Keratinos");b=b with {Encounter=b.Encounter! with {Inputs=[new(.5,"HeavyAttackMiss")]}};
            var r=Run(b);Yes(r.Events.Any(e=>e.Kind=="buff"&&e.Description.Contains("180초")));Yes(r.Events.Count(e=>e.Kind=="heavyMiss")==1);Near(r.Points.Last().MeleeRange!.Value,MeleeEngine.Calculate(catalog,b).Melee!.Range+2.8);
            var low=Run(b with {Scenario=b.Scenario with {Combo=11}});Yes(!low.Events.Any(e=>e.Kind=="buff"&&e.Description.Contains("케라티노스")));
        });
        check("사이베어:허공강공격4초버프·에크하인은명중필요",()=>{
            foreach(var name in new[]{"Sibear","Ekhein"})
            {
                var b=B(name);b=b with {Encounter=b.Encounter! with {Inputs=[new(.5,"HeavyAttackMiss")]}};var r=Run(b);
                Yes(r.Events.Count(e=>e.Kind=="heavyMiss")==1);Yes(r.Events.Any(e=>e.Kind=="buff"&&e.Description.Contains(name=="Sibear"?"사이베어":"에크하인"))==(name=="Sibear"));
            }
        });
        check("케라티노스:허공입력은피해나애니모시티명중소모를만들지않음",()=>{
            var b=B("Keratinos");var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Animosity");b=b with {Arcanes=new(Melee:new(a.Id,5,true)),Melee=b.Melee! with {Conditions=new(){["AnimosityStacks"]=10}},Encounter=b.Encounter! with {Inputs=[new(.5,"HeavyAttackMiss")]}};
            var r=Run(b);Yes(!r.Events.Any(e=>e.Time==.5&&e.Kind is "hit" or "animosity"));Yes(r.Points.Last().Combo==1);
        });
        check("잿쿠사르:첫접촉수치와계산식에제외된폭발범위공개",()=>{
            var b=B("Jat Kusar");b=b with {Melee=b.Melee! with {Attack="Heavy Attack"},Encounter=new(EncounterMode.FirstHit)};
            var r=EncounterEngine.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Length==0&&r.Timeline is not null);Yes(r.Notes.Any(n=>n.StartsWith("미반영:")&&n.Contains("218")));
            var eq=DpsEquation.FromTimeline(r.Timeline!);Yes(eq.Limitations!.Any(n=>n.Contains("218"))&&eq.Context!.Contains("일부 피해"));
        });
        check("잿쿠사르:218폭발은슬램피해만·칼날피해/강공격/컨오/팩션제외",()=>{
            var b=B("Jat Kusar");foreach(var (name,i) in new[]{("Pressure Point",0),("Killing Blow",1),("Condition Overload",2),("Smite Grineer",3)}){var m=catalog.Mods.Single(m=>m.EnglishName==name);b.Slots[i]=new(m.Id,m.MaxRank);}
            b=b with {Melee=b.Melee! with {Attack="Heavy Attack",Faction="Grineer",Statuses=new(){[DamageType.Impact]=1}}};Near(JatKusarMechanic.ExplosionBase(MeleeEngine.Calculate(catalog,b)),218);
            var seismic=catalog.Mods.Single(m=>m.EnglishName=="Seismic Wave");b.Slots[4]=new(seismic.Id,seismic.MaxRank);Near(JatKusarMechanic.ExplosionBase(MeleeEngine.Calculate(catalog,b)),654);
            var note=JatKusarMechanic.Describe(MeleeEngine.Calculate(catalog,b));Yes(note.Radius==0&&note.Chance is null&&note.Note.Contains("미기재"));
        });
    }
}
