using TennoForge.Core;

internal static class DestrezaChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();
        void Yes(bool v){if(!v)throw new Exception("데스트레자 투사체 불일치");}
        void Near(double x,double y){if(!double.IsFinite(x)||Math.Abs(x-y)>1e-8*Math.Max(1,Math.Abs(y)))throw new Exception($"{x} != {y}");}
        Build B(string name="Destreza Prime")=>new(catalog.Weapons.Single(w=>w.EnglishName==name).Id,new ModSlot?[8],new(Combo:12,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(catalog.Weapons.Single(w=>w.EnglishName==name).Id,null),Melee:new(Incarnon:true));
        void Mod(Build b,int slot,string name){var m=catalog.Mods.Single(m=>m.EnglishName==name);b.Slots[slot]=new(m.Id,m.MaxRank);}
        check("데스트레자:711/765 개별 투사체·변신 피해·강제 베기·미기재 상확 분리",()=>{
            foreach(var (name,damage,cc,cd) in new[]{("Destreza",711d,.28,2d),("Destreza Prime",765d,.32,3d)}){
                var c=DestrezaMechanic.Calculate(catalog,B(name),false);Yes(c.Damage.Exact&&c.StatusChance is null&&c.Count==5&&c.Lifetime==2);
                Near(c.Damage.Minimum,damage*2*(1+cc*(cd-1)));Near(c.ForcedSlashTick.Minimum,damage*2*.35*(1+cc*(cd-1)));Near(c.TargetDamage.Minimum,c.Damage.Minimum);
            }
        });
        check("데스트레자: 진화II 기본피해 제외·컨오 가산계수2·피해 브라켓 손계산",()=>{
            var b=B();Mod(b,0,"Condition Overload");Mod(b,1,"Pressure Point");
            var evo=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.First(p=>p.Stage==2);
            b=b with {Melee=b.Melee! with {Evolutions=[evo.Id],Statuses=new(){[DamageType.Impact]=1,[DamageType.Slash]=1}}};
            var c=DestrezaMechanic.Calculate(catalog,b,false);Near(c.Damage.Minimum,765*(1+1+1.2+2*.8*2)*(1+.32*2));
            Near(c.Damage.Minimum,DestrezaMechanic.Calculate(catalog,b with {Melee=b.Melee with {Evolutions=[]}},false).Damage.Minimum);
        });
        check("데스트레자: 명중 순간 준비 동작만 콤보12·트루스틸2배·킬링블로우",()=>{
            var b=B();Mod(b,0,"True Steel");Mod(b,1,"Killing Blow");
            var normal=DestrezaMechanic.Calculate(catalog,b,false);var windup=DestrezaMechanic.Calculate(catalog,b,true);
            Near(normal.Damage.Minimum,765*2*(1+.32*(1+1.2)*2));Near(windup.Damage.Minimum,765*(2+1.2)*12*(1+.32*(1+2*1.2)*2));
            Near(DestrezaMechanic.Calculate(catalog,b with {Melee=b.Melee! with {TargetIndex=9,Distance=90}},false).Damage.Minimum,normal.Damage.Minimum);
        });
        check("데스트레자: 상확 미기재를0%로 표시하지 않고 디사이시브·다우티 상한 분리",()=>{
            var b=B();var evo=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Effects.ContainsKey("StatusToBaseCritical"));var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Doughty");
            b=b with {Melee=b.Melee! with {Evolutions=[evo.Id]},Arcanes=new(Melee:new(a.Id,5,true))};var c=DestrezaMechanic.Calculate(catalog,b,false);
            Yes(!c.Damage.Exact&&c.StatusChance is null);Near(c.CriticalChance.Minimum,.32);Near(c.CriticalChance.Maximum,.67);Near(c.CriticalMultiplier.Minimum,3);Near(c.CriticalMultiplier.Maximum,53);
            var duplicate=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Duplicate");var naked=B();Near(DestrezaMechanic.Calculate(catalog,naked with {Arcanes=new(Melee:new(duplicate.Id,5,true))},false).Damage.Minimum,DestrezaMechanic.Calculate(catalog,naked,false).Damage.Minimum);
        });
    }
}
