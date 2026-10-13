using TennoForge.Core;

internal static class AckBruntChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var c=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool value){if(!value)throw new Exception("액크 & 브런트 흡수 규칙 불일치");}
        void Near(double a,double b){if(Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("잘못된 원소 충전을 허용함");}
        Build B(){var id=c.Weapons.Single(w=>w.EnglishName=="Ack & Brunt").Id;return new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),c.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(),Encounter:new(EncounterMode.FirstHit));}
        check("액크 & 브런트 흡수: 기본 원소만·4충전 상한·다음 공격 전부 소모",()=>{
            var state=new AckBruntElementState();Yes(state.Absorb(DamageType.Heat,3)==3);Yes(state.Absorb(DamageType.Cold,3)==1);Yes(state.Absorb(DamageType.Toxin,1)==0);var values=new Dictionary<string,double>();state.WriteTo(values);Near(values["BlockHeat"],3);Near(values["BlockCold"],1);Yes(state.Consume()==4&&state.Total==0);Reject(()=>state.Absorb(DamageType.Viral,1));Reject(()=>new AckBruntElementState(new Dictionary<string,double>{{"BlockHeat",.5}}));
        });
        check("액크 & 브런트: 충전0의 원소는 조합 자리를 만들지 않음·막은 화염은 전기 모드와 결합",()=>{
            var b=B();var mod=c.Mods.Single(m=>m.EnglishName=="Shocking Touch");b.Slots[0]=new(mod.Id,mod.MaxRank);var original=MeleeEngine.Calculate(c,b);Yes(original.Damage.GetValueOrDefault(DamageType.Electricity)>0&&!original.Damage.ContainsKey(DamageType.Radiation));
            var absorbed=MeleeEngine.Calculate(c,b with {Melee=b.Melee! with {Conditions=new(){["BlockHeat"]=4}}});var baseDamage=MeleeEngine.Calculate(c,B()).Damage.Values.Sum();Near(absorbed.Damage[DamageType.Radiation],baseDamage*(.9+.7));Near(absorbed.Damage.GetValueOrDefault(DamageType.Heat),0);
        });
        check("액크 & 브런트: 실제 원소 막기→첫 타에만 적용→두 번째 타격 이전 소비",()=>{
            var b=B();b=b with {Encounter=new(EncounterMode.FiveSeconds,Inputs:[new(0,"BlockedElement",4,DamageType.Heat)])};var result=CombatTimeline.Calculate(c,b,stances,1);Yes(result.Unsupported.Length==0);Yes(result.Events.Single(e=>e.Kind=="absorbedElement").Amount==4);Yes(result.Events.Single(e=>e.Kind=="absorbedElementSpent").Time==0);var plain=CombatTimeline.Calculate(c,B() with {Encounter=new(EncounterMode.FiveSeconds)},stances,1);var once=CombatTimeline.Calculate(c,B() with {Encounter=new(EncounterMode.FirstHit,Inputs:[new(0,"BlockedElement",4,DamageType.Heat)])},stances,1);var noBuff=CombatTimeline.Calculate(c,B(),stances,1);Near(once.DirectDamage,noBuff.DirectDamage*1.7);Yes(result.DirectDamage>plain.DirectDamage);
        });
        check("액크 & 브런트: 실딩 전달은 원소 저장만·직접 막기 콤보와 구분",()=>{
            var b=B();Reject(()=>CombatTimeline.Calculate(c,b with {Encounter=new(EncounterMode.FirstHit,Inputs:[new(0,"ShieldingElement",1,DamageType.Heat)])},stances,1));b.Slots[0]=new(AckBruntElementState.ShieldingMod,5);b=b with {Encounter=new(EncounterMode.FirstHit,Inputs:[new(0,"ShieldingElement",1,DamageType.Heat)])};var result=CombatTimeline.Calculate(c,b,stances,1);Yes(result.Unsupported.Length==0&&result.Events.Any(e=>e.Kind=="absorbedElement"));Yes(!result.Events.Any(e=>e.Kind=="combo"&&e.Description.Contains("막기")));
        });
    }
}
