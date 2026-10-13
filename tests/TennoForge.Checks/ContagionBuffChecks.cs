using System.Text.Json;
using TennoForge.Core;

internal static class ContagionBuffChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var id=ZawCatalog.Id("Sepfahn","Peye","Vargeet II Jai");var original=Catalog.LoadMelee();
        var catalog=original with {Weapons=original.Weapons.Select(w=>w.Id==id?w with {Damage=new(){[DamageType.Impact]=100},CriticalChance=0,StatusChance=0}:w).ToArray()};
        var exodia=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Exodia Contagion");
        Build B(ContagionDamageBuffs? buffs=null)=>new(id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(id,null),Melee:new(Exodia:new(exodia.Id,3),ContagionBuffs:buffs),Encounter:new(EncounterMode.TenSeconds,Contagion:[new(0)],ScriptedAttacksOnly:true));
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-6*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("컨테이전 외부 버프 브라켓 오류");}
        check("컨테이전:벡스/데스브링어/앰프/리프트/리썰은 근접·무기 브라켓에 각각 가산",()=>{
            var plain=ExodiaContagionMechanic.Calculate(catalog,B(),0);
            foreach(var buffs in new[]{new ContagionDamageBuffs(VexArmor:1),new(Deathbringer:1),new(Amp:1),new(RiftTorrent:1),new(LethalLevitation:1)})
            {var c=ExodiaContagionMechanic.Calculate(catalog,B(buffs),0);Near(c.Contact.ExpectedDamage,plain.Contact.ExpectedDamage*4);Near(c.EmbeddedTick.ExpectedDamage,plain.EmbeddedTick.ExpectedDamage*4);Near(c.Explosion.ExpectedDamage,plain.Explosion.ExpectedDamage*4);Near(c.Contact.Brackets.AdditiveDamage,1);Near(c.Contact.Brackets.WeaponDamageMultiplier,2);}
        });
        check("컨테이전:보이드 스트라이크는 한 번·이클립스/데스 게이트는 서로 독립 제곱",()=>{
            Near(ExodiaContagionMechanic.Calculate(catalog,B(new(VoidStrike:10)),0).Contact.ExpectedDamage,200*11);
            var buffs=new ContagionDamageBuffs(Eclipse:2,DeathGate:1);var c=ExodiaContagionMechanic.Calculate(catalog,B(buffs),0);Near(c.Contact.ExpectedDamage,200*3*3*2*2);Near(c.Contact.Brackets.AdditiveDamage,0);Near(c.Contact.Brackets.WeaponDamageMultiplier,1);
        });
        check("컨테이전:프레셔·아라크네·컨오와 외부 피해 버프의 교차항 독립 검산",()=>{
            var buffs=new ContagionDamageBuffs(VexArmor:3.5,Deathbringer:.5,VoidStrike:10,Eclipse:2,DeathGate:1);var b=B(buffs);
            foreach(var (name,index) in new[]{("Pressure Point",0),("Condition Overload",1)}){var m=catalog.Mods.Single(m=>m.EnglishName==name);b.Slots[index]=new(m.Id,m.MaxRank);}
            var arachne=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Arcane Arachne");b=b with {Arcanes=new(Warframe1:new(arachne.Id,5)),Melee=b.Melee! with {Statuses=new(){[DamageType.Viral]=1,[DamageType.Heat]=1}}};
            var c=ExodiaContagionMechanic.Calculate(catalog,b,0);
            Near(c.Contact.ExpectedDamage,200*(1+1.2+3.5+.5+10)*(1+1.5+3.5+.5)*(1+.8*2)*36);
            Near(c.EmbeddedTick.ExpectedDamage,100*(1+1.2+3.5+.5+10)*(1+1.5+3.5+.5)*36);
        });
        check("컨테이전:외부 버프를 실제 직격·4틱·폭발에 연결·저장·검 자동 반복과 분리",()=>{
            var b=B(new(VexArmor:1,Eclipse:1));var copy=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Yes(copy.Melee!.ContagionBuffs==b.Melee!.ContagionBuffs);
            var r=CombatTimeline.Calculate(catalog,copy,StanceCatalog.Load(),1);Yes(r.Unsupported.Length==0);Near(r.DirectDamage,1100*16);Near(DpsEquation.FromTimeline(r).Evaluate(),110*16);Yes(r.Notes.Any(n=>n.Contains("외부 버프")));
            try{CombatTimeline.Calculate(catalog,b with {Encounter=b.Encounter! with {ScriptedAttacksOnly=false}},StanceCatalog.Load(),1);throw new Exception("혼합 경로의 버프 누락을 허용");}catch(ArgumentException e){Yes(e.Message.Contains("투사체만"));}
            try{new ContagionDamageBuffs(Eclipse:double.NaN).Validate();throw new Exception("NaN 허용");}catch(ArgumentException){}
        });
    }
}
