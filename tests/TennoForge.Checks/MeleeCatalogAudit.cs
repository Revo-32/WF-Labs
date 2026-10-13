using TennoForge.Core;

internal static class MeleeCatalogAudit
{
    internal static object Run()
    {
        var catalog=Catalog.LoadMelee();var rules=MeleeRuleCatalog.Load();var stances=StanceCatalog.Load();var capacity=CapacityCatalog.Load();
        var attacks=0;var combos=0;var rejected=new List<object>();var defects=new List<string>();
        void Verify(string key,Func<(Calculation C,StanceCalculation? S)> calculate,Action count)
        {
            try
            {
                var (c,s)=calculate();var e=DpsEquation.From(c,s);
                if(!double.IsFinite(e.Result)||e.Result<0||Math.Abs(e.Evaluate()-e.Result)>1e-8*Math.Max(1,e.Result))defects.Add(key+" · 수식/DPS 불일치");
                else count();
            }
            catch(ArgumentException ex){rejected.Add(new{key,reason=ex.Message});}
            catch(Exception ex){defects.Add(key+" · "+ex.GetType().Name+" · "+ex.Message);}
        }
        foreach(var w in catalog.Weapons)
        {
            var b=new Build(w.Id,new ModSlot?[8],new(Enemy:new(10000,2700)),catalog.Version,5,Capacity:capacity.Default(w.Id,null),Melee:new());
            foreach(var a in MeleeEngine.Attacks(w.Id))
            {
                var policy=MeleeEngine.AttackPolicy(w.Id,a);
                Verify(w.EnglishName+" / "+a,()=> (MeleeEngine.Calculate(catalog,b with {Melee=new(Attack:a,Incarnon:policy.Incarnon)}),null),()=>attacks++);
            }
            foreach(var stance in stances.ForWeapon(w))foreach(var combo in stance.Combos)
            {
                var cap=capacity.Default(w.Id,capacity.StanceMods.ContainsKey(stance.Id)?stance.Id:null);
                Verify(w.EnglishName+" / "+stance.Id+" / "+combo.Key,()=>{var c=StanceEngine.Calculate(catalog,b with {Stance=new(stance.Id,combo.Key,stances.Revision),Capacity=cap},stances);return(c.Stats,c);},()=>combos++);
            }
        }
        return new{rules=rules.Version,selectableWeapons=catalog.Weapons.Length,weaponProfiles=rules.Weapons.Length,modRecords=rules.Mods.Length,sourceRecords=rules.Sources.Length,incarnonDefinitions=rules.Incarnons.Length,enemyProfiles=rules.EnemyProfiles.Length,calculatedAttacks=attacks,calculatedCombos=combos,rejected,defects,pendingWeapons=rules.Weapons.Where(w=>w.PendingMechanics?.Length>0).Select(w=>new{w.Id,w.Name,w.PendingMechanics}),note="무모드·기본 조건에서 전체 선택 항목의 예외/유한값/표시식 일치 검사. 모든 무기 기믹·상태 시뮬레이션·모드 조합의 위키 수식 검증 완료를 뜻하지 않습니다."};
    }
}
