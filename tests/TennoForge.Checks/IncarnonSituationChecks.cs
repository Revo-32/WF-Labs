using TennoForge.Core;

internal static class IncarnonSituationChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool v){if(!v)throw new Exception("인카논 상황 전이 불일치");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-8*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(string name,string? condition=null)
        {
            var w=catalog.Weapons.Single(w=>w.EnglishName==name);var p=IncarnonEngine.ForWeapon(w.Id)!.Perks.SingleOrDefault(p=>condition is not null&&p.Condition==condition);
            return new(w.Id,new ModSlot?[8],new(BaseInterval:60,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Evolutions:p is null?[]:[p.Id]),Encounter:new(EncounterMode.TenSeconds));
        }
        CombatTimelineResult Run(Build b){var r=CombatTimeline.Calculate(catalog,b,stances,1);if(r.Unsupported.Length>0)throw new Exception(string.Join(" · ",r.Unsupported));return r;}
        check("세라믹 대거:실제 내 실드 파괴 후8초 기본 피해+80·정확한 만료",()=>{
            var b=B("Ceramic Dagger","ShieldBreak");b=b with {Arcanes=new(MaximumShields:100,Shields:100),Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"PlayerShieldDamage",100)])};
            var r=Run(b);var control=Run(b with {Encounter=new(EncounterMode.TenSeconds)});
            double D(CombatTimelineResult t,double time)=>t.Points.Single(p=>p.Time==time).SnapshotDps;
            Near(D(r,0),D(control,0));Near(D(r,1),D(control,1)*340/260);Near(D(r,8.9),D(control,8.9)*340/260);Near(D(r,9),D(control,9));
            Yes(r.Events.Count(e=>e.Kind=="incarnonPerk")==1);
            var empty=Run(b with {Arcanes=b.Arcanes! with {Shields=0,ShieldRechargeEnabled=false},Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"PlayerShieldBreak")])});Yes(!empty.Events.Any(e=>e.Kind=="incarnonPerk"));
        });
        check("애크 앤 브런트:실드 파괴11초·막기 콤보+300%·다른 무기 피해로 치환하지 않음",()=>{
            var b=B("Ack & Brunt","ShieldBreak");b=b with {Encounter=new(EncounterMode.TenSeconds,Inputs:[new(.1,"PlayerShieldBreak"),new(1,"BlockedHit")])};
            var r=Run(b);Yes(r.Events.Any(e=>e.Kind=="incarnonPerk"&&e.Description.Contains("11초")));Near(r.Events.Single(e=>e.Kind=="combo"&&e.Description.Contains("막기")).Amount,4);
        });
        check("세라믹 투사체:실드 파괴+80 예외·상시+120 제외·폭발은 레드 라이트 핸드 제외",()=>{
            var b=B("Ceramic Dagger","ShieldBreak");var red=IncarnonEngine.ForWeapon(b.WeaponId)!.Perks.Single(p=>p.Condition=="FirstQuickMelee");
            b=b with {Melee=b.Melee! with {Incarnon=true,Evolutions=b.Melee!.Evolutions!.Append(red.Id).ToArray()}};
            foreach(var attack in new[]{"Spectral Dagger","Spectral Dagger Explosion"})
            {
                var off=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Attack=attack}});
                var active=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Attack=attack,Conditions=new(){["ShieldBreak"]=1,["FirstQuickMelee"]=1}}});
                Near(off.Melee!.Brackets!.BaseDamage,350);Near(active.Melee!.Brackets!.BaseDamage,430);
                Near(active.CriticalMultiplier-off.CriticalMultiplier,attack.Contains("Explosion")?0:2);
                foreach(var (type,amount) in off.Damage)Near(active.Damage[type]/amount,430d/350);
            }
        });
        check("퓨랙스 킬 조이:살아 있는 대상의6m 경계·5명 상한·수동 중첩 대신 장면",()=>{
            var b=B("Furax","NearbyEnemies");var plain=MeleeEngine.Calculate(catalog,b).TargetDps;
            var scene=b with {Melee=b.Melee! with {Conditions=new(){["NearbyEnemies"]=5}},Encounter=new(EncounterMode.TenSeconds,NearbyEnemies:[new("edge",6),new("outside",6.001)])};
            Near(Run(scene).Points[0].SnapshotDps,plain*1.2);
            var crowded=b with {Encounter=new(EncounterMode.TenSeconds,NearbyEnemies:Enumerable.Range(1,7).Select(n=>new CombatEnemy(n.ToString(),1)).ToArray())};Near(Run(crowded).Points[0].SnapshotDps,plain*1.5);
        });
        check("레드 라이트 핸드:첫 입력 사용 후 해제·주무기로 전환해야 다시 준비",()=>{
            var b=B("Sibear","FirstQuickMelee");var control=Run(b);
            b=b with {Melee=b.Melee! with {Conditions=new(){["FirstQuickMelee"]=1}},Encounter=new(EncounterMode.TenSeconds,Inputs:[new(1,"EquipPrimary"),new(2,"EquipSecondary"),new(3,"EquipPrimary"),new(4,"EquipMelee")])};
            var r=Run(b);
            double D(CombatTimelineResult t,double time)=>t.Points.Single(p=>p.Time==time).SnapshotDps;
            Near(D(r,0),D(control,0));Yes(D(r,1)>D(control,1));Near(D(r,2),D(control,2));Yes(D(r,3)>D(control,3));Near(D(r,4),D(control,4));
        });
        check("콤보 수납 정지:남은 시간을 보존·초기 콤보40/초 회복은 계속",()=>{
            var c=new MeleeComboCounter(0,4,20,12,5);c.SetPaused(2,true);c.Advance(100);Near(c.Points,60);c.SetPaused(100,false);c.Advance(102.999);Near(c.Points,60);c.Advance(103);Near(c.Points,0);
            var regen=new MeleeComboCounter(220,12,20,12,5);regen.SpendHeavy(0,0,false);regen.SetPaused(0,true);regen.Advance(2);Near(regen.Points,80);regen.Advance(6);Near(regen.Points,220);
        });
        check("앙쿠:변신 슬라이드 명중6초·후속 일반 입력은 갱신하지 않음",()=>{
            var b=B("Anku");b=b with {Scenario=b.Scenario with {BaseInterval=2},Melee=b.Melee! with {Incarnon=true},Encounter=new(EncounterMode.TenSeconds,AttackOrder:["Slide Attack","Normal Attack","Normal Attack","Normal Attack","Normal Attack","Normal Attack"])};
            var r=Run(b);var buffs=r.Events.Where(e=>e.Kind=="buff"&&e.Description.Contains("앙쿠")).ToArray();Yes(buffs.Length==1&&buffs[0].Time==0&&buffs[0].Description.Contains("6초"));
            Near(r.Events.Single(e=>e.Kind=="buffEnd"&&e.Description.Contains("앙쿠")).Time,6);
            foreach(var e in r.Events.Where(e=>e.Kind=="hit"&&e.Time>0&&e.Time<6))Yes(r.Events.Any(p=>p.Kind=="proc"&&p.Time==e.Time&&p.Description.Contains("베기")));
            var no=Run(b with {Melee=b.Melee! with {Incarnon=false}});Yes(!no.Events.Any(e=>e.Kind=="buff"&&e.Description.Contains("앙쿠")));
        });
    }
}
