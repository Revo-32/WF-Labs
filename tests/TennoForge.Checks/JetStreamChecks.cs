using TennoForge.Core;

internal static class JetStreamChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var stances=StanceCatalog.Load();
        void Yes(bool b){if(!b)throw new Exception("제트 스트림 검증 실패");}
        void Near(double a,double b){if(!double.IsFinite(a)||Math.Abs(a-b)>1e-6*Math.Max(1,Math.Abs(b)))throw new Exception($"{a} != {b}");}
        Build B(string name,double distance=0){var w=catalog.Weapons.Single(w=>w.EnglishName==name);return new(w.Id,new ModSlot?[8],new(BaseInterval:10,Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(Attack:"Ranged Attack",Distance:distance,ExternalEquipment:new(JetStream:true)),Encounter:new(EncounterMode.FiveSeconds));}
        check("제트스트림 Hitscan감쇠:리디머프라임10~30→20~60m·사르파20~40→40~80m",()=>{
            foreach(var (name,distance,start,end,reduction) in new[]{("Redeemer Prime",15d,10d,30d,.9375),("Sarpa",30d,20d,40d,.8571)})
            {
                var b=B(name,distance);var boosted=MeleeEngine.Calculate(catalog,b);var zero=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=0}});Near(boosted.ExpectedHit,zero.ExpectedHit);
                var normal=MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {ExternalEquipment=null}});Near(normal.ExpectedHit/zero.ExpectedHit,1-(distance-start)/(end-start)*reduction);
            }
        });
        check("제트스트림 스트로파:11/15/17m단계와최대사거리동시에배율적용",()=>{
            var b=B("Stropha");var full=MeleeEngine.Calculate(catalog,b).ExpectedHit;
            foreach(var (distance,factor) in new[]{(22d,1d),(30d,.5),(32d,.5-(16-15)/2d*(.5-10d/700)),(34d,10d/700),(34.01,0d)})
                Near(MeleeEngine.Calculate(catalog,b with {Melee=b.Melee! with {Distance=distance}}).ExpectedHit,full*factor);
            var slam=b with {Melee=b.Melee! with {Attack="Slam Attack",Distance=0}};Near(MeleeEngine.Calculate(catalog,slam).Melee!.AreaRadius!.Value,5);
        });
        check("제트스트림 시계:범위이탈뒤3초·재진입은이전만료취소·다시이탈후3초",()=>{
            var state=new JetStreamTimeline(new(JetStream:true),[new(1,"JetStreamLeave"),new(3,"JetStreamEnter"),new(4,"JetStreamLeave")]);
            Yes(state.Active(0)&&state.Active(4)&&state.Active(6.999)&&!state.Active(7));Near(state.NextTransition(4),7);
            var initial=new JetStreamTimeline(new(JetStream:true,JetStreamRemaining:.5));Yes(initial.Active(.49)&&!initial.Active(.5));
            var none=new JetStreamTimeline(new(JetStream:true,JetStreamRemaining:0),[new(2,"JetStreamEnter")]);Yes(!none.Active(0)&&none.Active(2));
        });
        check("제트스트림 시간DPS:잔여만료·재진입시감쇠캐시갱신·버프없음시작",()=>{
            var b=B("Redeemer Prime",15);b=b with {Melee=b.Melee! with {ExternalEquipment=new(JetStream:true,JetStreamRemaining:.5)},Encounter=b.Encounter! with {Inputs=[new(2,"JetStreamEnter")]}};
            var r=CombatTimeline.Calculate(catalog,b,stances,1);Yes(r.Unsupported.Length==0);var first=r.Points[0].SnapshotDps;Near(r.Points.Single(p=>p.Time==1).SnapshotDps/first,.765625);Near(r.Points.Single(p=>p.Time==2).SnapshotDps,first);
        });
        check("제트스트림 비행전환:이미발사된투사체속도변화를임의소급하지않음",()=>{
            var s=new JetStreamTimeline(new(JetStream:true,JetStreamRemaining:1));s.RequireConstantFlight(.1,.9);s.RequireConstantFlight(1.1,2);
            try{s.RequireConstantFlight(.5,1.1);throw new Exception("비행 중 버프 변화 허용");}catch(CombatRuleUnavailableException){}
            try{new JetStreamTimeline(new(JetStream:true,JetStreamRemaining:0),[new(1,"JetStreamLeave")]);throw new Exception("범위 밖 이탈로 버프 생성");}catch(ArgumentException){}
        });
    }
}
