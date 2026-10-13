using TennoForge.Core;

internal static class MagusAggressChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string name="Galatine Prime",int rank=5){var w=catalog.Weapons.Single(w=>w.EnglishName==name);return new(w.Id,new ModSlot?[8],new(BaseInterval:1,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(),Arcanes:new(Operator:new(MagusAggressState.Id,rank)));}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-6)throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("마구스 어그레스 검증 불일치");}
        void Reject(Action action,string text){try{action();}catch(ArgumentException e){if(!e.Message.Contains(text))throw;return;}throw new Exception("마구스 잘못된 입력 허용: "+text);}
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        check("마구스 어그레스:50~300% CD는오르건셔터와가산·치확/피해브라켓불변",()=>{
            for(var rank=0;rank<=5;rank++)foreach(var name in new[]{"Galatine Prime","Fragor Prime"})
            {
                var b=B(name,rank);var m=catalog.Mods.Single(m=>m.EnglishName=="Organ Shatter");b.Slots[0]=new(m.Id,m.MaxRank);var plain=MeleeEngine.Calculate(catalog,b with {Arcanes=null});var c=MeleeEngine.Calculate(catalog,b);Near(c.CriticalMultiplier,plain.CriticalMultiplier/1.9*(1.9+.5*(rank+1)));Near(c.CriticalChance,plain.CriticalChance);Near(c.Damage.Values.Sum(),plain.Damage.Values.Sum());
            }
        });
        check("마구스 어그레스:다른무기군·전용/간접익절티드 CD미적용·전용충전보존",()=>{
            foreach(var name in new[]{"Nikana Prime","Ghoulsaw","Hespar"}){var b=B(name);Near(MeleeEngine.Calculate(catalog,b).CriticalMultiplier,MeleeEngine.Calculate(catalog,b with {Arcanes=null}).CriticalMultiplier);}
            var s=new MagusAggressState();s.Transfer(0);var blade=catalog.Weapons.First(w=>ExaltedRules.Find(w.Id)?.Family==ExaltedFamily.Blade).Id;var whip=catalog.Weapons.First(w=>ExaltedRules.Find(w.Id)?.Family==ExaltedFamily.Whipclaw).Id;
            Yes(!s.Consume(blade,4));Near(s.Charges,4);Yes(!s.Consume(whip,4.9));Yes(s.Consume(whip,5));Near(s.Charges,3);
        });
        check("마구스 어그레스:4공격·20초미만재발동불가·기산점미기재경계확정안함",()=>{
            var s=new MagusAggressState();var b=B();Yes(s.Transfer(0));for(var i=1;i<=4;i++)Yes(s.Consume(b.WeaponId,i));Near(s.Charges,0);Yes(!s.Transfer(19.9));Reject(()=>s.Transfer(22),"기산점");Yes(s.Transfer(24));Near(s.Charges,4);
        });
        check("마구스 어그레스 시간교전:근접전이전미발동·4입력만강화·다중적당중복소모없음",()=>{
            var b=B();b=b with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"WarframeMeleeTransfer")])};var r=Run(b);Yes(r.Events.Where(e=>e.Kind=="magusCharge").Select(e=>e.Amount).SequenceEqual(new[]{3d,2,1,0}));Near(r.Points.Last().MagusAggressCharges!.Value,0);Yes(r.Points.First().SnapshotDps>r.Points.Last().SnapshotDps);
            var idle=Run(b with {Encounter=b.Encounter! with {Inputs=[]}});Near(idle.Points.First().MagusAggressCharges!.Value,0);Yes(idle.Points.First().SnapshotDps<r.Points.First().SnapshotDps);
        });
        check("마구스 어그레스:다중타격스탠스 입력단위·4타격이아닌4동작",()=>{
            var b=B();b=b with {Stance=new("Cleaving Whirlwind","Neutral",stances.Revision),Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"WarframeMeleeTransfer")],HitTimes:[0,.1,.2,.3])};
            // Use the authored combo's actual hit count with explicit timing;
            // verify charges track input groups, not contact rows.
            b=b with {Capacity=CapacityCatalog.Load().Default(b.WeaponId,"Cleaving Whirlwind")};var resolved=StanceEngine.Calculate(catalog,b,stances);var count=resolved.Hits.Sum(h=>h.Count);b=b with {Encounter=b.Encounter! with {HitTimes=Enumerable.Range(0,count).Select(i=>i*.1).ToArray()}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="magusCharge")==4);Yes(r.Events.Count(e=>e.Kind=="hit")>4);
        });
        check("마구스 어그레스:오퍼레이터슬롯 검증·제니스타근사소모를정확주기로날조하지않음",()=>{
            var b=B();Reject(()=>MeleeEngine.Calculate(catalog,b with {Arcanes=new(Warframe1:new(MagusAggressState.Id,5))}),"슬롯");Reject(()=>MeleeEngine.Calculate(catalog,b with {Arcanes=new(Operator:new(MagusAggressState.Id,6))}),"랭크");
            b=B("Zenistar");b=b with {Encounter=new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true,Inputs:[new(0,"WarframeMeleeTransfer")],ZenistarDiscs:[new(.1,.2,AuraTimes:[.5])])};Reject(()=>Run(b),"약2초");
        });
    }
}
