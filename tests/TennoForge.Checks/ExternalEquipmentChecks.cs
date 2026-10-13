using TennoForge.Core;

internal static class ExternalEquipmentChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        Build B(string name,string attack="Normal Attack") {var w=catalog.Weapons.Single(w=>w.EnglishName==name);return new(w.Id,new ModSlot?[8],new(BaseInterval:10,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:attack),Encounter:new(EncounterMode.FiveSeconds));}
        void Yes(bool v){if(!v)throw new Exception("외부 장비 효과 검증 실패");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-6*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        CombatTimelineResult Run(Build b,int samples=1){var r=CombatTimeline.Calculate(catalog,b,stances,samples);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        check("다이큐 아말감:0~5랭크0.5~3%·니카나전용·다른무기군제외",()=>{
            foreach(var rank in Enumerable.Range(0,6))
            {
                foreach(var name in new[]{"Nikana Prime","Syam","Tatsu","Sun & Moon","Skana"})
                {
                    var b=B(name);b=b with {Melee=b.Melee! with {ExternalEquipment=new("Daikyu",rank)}};var c=MeleeEngine.Calculate(catalog,b);var yes=name is "Nikana Prime" or "Syam";
                    Near(c.Melee!.Effects.GetValueOrDefault("NikanaLifeSteal"),yes?.005*(rank+1):0);Near(c.Melee.Healing,yes?c.TargetDps*c.HitInterval*.005*(rank+1):0);
                }
            }
        });
        check("다이큐:직접피해흡혈·강제베기지속피해제외·적방어층적용후",()=>{
            var b=B("Nikana Prime","Heavy Attack");b=b with {Melee=b.Melee! with {ExternalEquipment=new("Daikyu")}};
            var r=Run(b,64);Yes(r.StatusDamage>0&&r.Recovery?.NikanaHealing is not null);Near(r.Recovery!.NikanaHealing!.Mean,r.DirectDamage*.03);
        });
        check("다이큐:샤암파동 직접회복·무기지정입력에서도복원",()=>{
            var b=B("Syam");b=b with {Melee=b.Melee! with {ExternalEquipment=new("Daikyu",2)},Encounter=b.Encounter! with {ScriptedAttacksOnly=true,WeaponActions=[new(0,0,Contacts:[new(.2)])]}};
            var r=Run(b,64);Near(r.Recovery!.NikanaHealing!.Mean,r.DirectDamage*.015);Yes(r.Events.Any(e=>e.Kind=="nikanaHealing"));
        });
        check("아르고나크:단검6기본방어력·충격없어도직접및상태틱·쌍단검제외",()=>{
            var b=B("Rakta Dark Dagger","Heavy Attack");Mods(b,"Voltaic Strike");b=b with {Scenario=b.Scenario with {Enemy=new(1e9,1000)},Melee=b.Melee! with {ExternalEquipment=new("Argonak")},Encounter=b.Encounter! with {BaseArmor=500}};
            var r=Run(b,64);Yes(r.Events.Count(e=>e.Kind=="daggerArmor")>1);Yes(r.Events.Any(e=>e.Kind=="daggerArmor"&&e.Description.Contains("상태")));Yes(r.Points.Last().Armor<988);
            var dual=B("Nepheri");Near(MeleeEngine.Calculate(catalog,dual with {Melee=dual.Melee! with {ExternalEquipment=new("Argonak")}}).Melee!.Effects.GetValueOrDefault("DaggerArmorStrip"),0);
        });
        check("아르고나크:인플루언스전파·주변전기틱도각대상기본방어력감소",()=>{
            var b=B("Rakta Dark Dagger","Heavy Attack");Mods(b,"Voltaic Strike","Shocking Touch");var influence=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Influence");
            b=b with {Scenario=b.Scenario with {Enemy=new(1e9,1000)},Melee=b.Melee! with {ExternalEquipment=new("Argonak")},Arcanes=new(Melee:new(influence.Id,5,true)),Encounter=b.Encounter! with {BaseArmor=500,NearbyEnemies=[new("전파대상",8,Defense:new(1e9,1000))]}};
            var r=Run(b,128);Yes(r.Enemies![1].StatusDamage>0);Yes(r.Events.Any(e=>e.Kind=="daggerArmor"&&e.Description.Contains("상태")));
            var plain=Run(b with {Melee=b.Melee! with {ExternalEquipment=null}},128);Yes(r.Enemies[1].StatusDamage>plain.Enemies![1].StatusDamage);
        });
        check("아르고나크·섀터링:중복6+6·방어력없는적에는기본방어력불필요",()=>{
            var b=B("Karyst");Mods(b,"Shattering Impact");b=b with {Scenario=b.Scenario with {Enemy=new(1e9,1000)},Melee=b.Melee! with {ExternalEquipment=new("Argonak")},Encounter=new(EncounterMode.FirstHit,BaseArmor:500)};
            var r=Run(b);Near(r.Points.Last().Armor,976);Yes(r.Events.Count(e=>e.Kind is "daggerArmor" or "armor")==2);
            Run(b with {Scenario=b.Scenario with {Enemy=new(1e9,0)},Encounter=b.Encounter! with {BaseArmor=null}});
        });
        check("제트스트림:랭크/시전자위력·네페리속도와19/20m범위·공속별도",()=>{
            foreach(var rank in Enumerable.Range(0,4))
            {
                var b=B("Nepheri","Fireballs");b=b with {Melee=b.Melee! with {ExternalEquipment=new(JetStream:true,JetStreamRank:rank,JetStreamStrength:2)}};var c=MeleeEngine.Calculate(catalog,b);Near(c.Melee!.Effects["ProjectileSpeed"],new[]{.5,.65,.8,1}[rank]*2);
            }
            var n=B("Nepheri");n=n with {Melee=n.Melee! with {ExternalEquipment=new(JetStream:true)},Encounter=n.Encounter! with {ScriptedAttacksOnly=true,WeaponActions=[new(0,0,Sequence:2,Contacts:[new(.15,Travel:30)])]}};Yes(Run(n).Events.Any(e=>e.Kind=="weaponProjectileImpact"));
            var plain=MeleeEngine.Calculate(catalog,n with {Melee=n.Melee! with {Attack="Fireballs",ExternalEquipment=null,Distance=30}});Near(plain.ExpectedHit,0);
        });
        check("제트스트림:버디락/그리고리거리·칼날과폭발반경은증가하지않음",()=>{
            foreach(var (name,attack,range) in new[]{("Verdilac","Energy Wave",35d),("Tenet Grigori","Energy Disk",10d)})
            {
                var b=B(name,attack);var ordinary=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=range*1.5}});
                var buffed=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=range*1.5,ExternalEquipment=new(JetStream:true)}});Near(ordinary.ExpectedHit,0);Yes(buffed.ExpectedHit>0);
            }
            var fan=B("Gunsen","Heavy Slam Attack");var a=MeleeEngine.Calculate(catalog,fan);var z=MeleeEngine.Calculate(catalog,fan with {Melee=fan.Melee! with {ExternalEquipment=new(JetStream:true)}});Near(a.Melee!.AreaRadius!.Value,z.Melee!.AreaRadius!.Value);
        });
        check("상한방어력:기본값감소후레벨성장·2700상한·200하한재적용없음",()=>{
            var def=EnemyCatalog.Load().Enemies.Single(e=>e.EnglishName=="Heavy Gunner");var b=EnemyScaling.Apply(B("Karyst"),new(def.Id,200));var stats=EnemyScaling.Calculate(b.EnemySelection!);
            Near(FlatArmorReduction.Remaining(b,6),2700);Near(FlatArmorReduction.Remaining(b,499),stats.UncappedArmor/500);Near(FlatArmorReduction.Remaining(b,500),0);Yes(FlatArmorReduction.Remaining(b,499)<200);
            b=b with {Melee=b.Melee! with {ExternalEquipment=new("Argonak")}};Yes(Run(b).Unsupported.Length==0);
        });
        check("충전글레이브:충격피해가있어도섀터링제외·일반투척유지",()=>{
            foreach(var attack in new[]{"Charged Throw","Throw"})
            {
                var b=B("Glaive",attack);Mods(b,"Shattering Impact");b=b with {Scenario=b.Scenario with {Enemy=new(1e9,1000)},Encounter=new(EncounterMode.FirstHit,BaseArmor:500)};var r=Run(b);
                Yes(r.Events.Any(e=>e.Kind=="armor")== (attack=="Throw"));
            }
        });
    }
}
