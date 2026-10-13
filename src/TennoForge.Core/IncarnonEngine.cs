namespace TennoForge.Core;

public static class IncarnonEngine
{
    public static IncarnonDefinition? ForWeapon(string weaponId)=>MeleeRuleCatalog.Load().Incarnons.SingleOrDefault(x=>x.WeaponId==weaponId);
    internal static IncarnonDefinition? Apply(Build build,Action<string,double> add,List<string> notes)
    {
        var setup=build.Melee!;var definition=ForWeapon(build.WeaponId);var selections=setup.Evolutions??[];
        if(definition is null)
        {
            if(setup.Incarnon||selections.Length>0)throw new ArgumentException("이 무기에는 인카논 진화가 없습니다.");
            return null;
        }
        if(selections.Distinct().Count()!=selections.Length)throw new ArgumentException("진화가 중복 선택되었습니다.");
        if(setup.IncarnonSecondsRemaining is not null)_=new IncarnonFormState(build);
        var perks=selections.Select(id=>definition.Perks.SingleOrDefault(p=>p.Id==id)??throw new ArgumentException("무기와 진화 식별자가 일치하지 않습니다.")).ToArray();
        if(perks.GroupBy(x=>x.Stage).Any(g=>g.Count()>1))throw new ArgumentException("각 진화 단계는 하나만 선택할 수 있습니다.");
        var conditions=setup.Conditions??[];
        foreach(var perk in perks)
        {
            foreach(var (key,value) in perk.Effects)add(key,value);
            add("CoExcludedBase",perk.Effects.GetValueOrDefault("BaseDamage"));
            var count=perk.Condition is null?0:conditions.GetValueOrDefault(perk.Condition);
            if(perk.Condition=="Impaled"&&setup.Overguard>0)count=0;
            MeleeEngine.Range(count,0,perk.Cap,perk.Name+" 발동 중첩");
            foreach(var (key,value) in perk.Conditional)add(key,value*count);
            add("CoExcludedBase",perk.Conditional.GetValueOrDefault("BaseDamage")*count);
            notes.Add($"진화 {perk.Stage} · {perk.Name}"+(perk.Condition is null?"":$" · 발동 {count}/{perk.Cap}"));
        }
        if(setup.Incarnon)
        {
            foreach(var (key,value) in definition.Form)add(key,value);
            if(definition.Code=="Anku"&&conditions.GetValueOrDefault("SlideHitBuff")>0){add("Range",3);add("ForcedSlash",1);}
            if(definition.Code=="Destreza")add("PunctureDamage",.1*Math.Clamp(conditions.GetValueOrDefault("IncarnonHeavyKills"),0,30));
            notes.Add("인카논 변신 상태 적용 · 변신 전후를 분리해서 계산합니다.");
        }
        return definition;
    }
}
