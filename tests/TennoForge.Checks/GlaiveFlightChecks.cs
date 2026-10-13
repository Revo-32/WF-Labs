using System.Text.Json;
using TennoForge.Core;

internal static class GlaiveFlightChecks
{
    internal static void Run(Action<string,Action> check)
    {
        var catalog=Catalog.LoadMelee();var rules=MeleeRuleCatalog.Load();var stances=StanceCatalog.Load();
        Build B(string name="Glaive") {var w=catalog.Weapons.Single(w=>w.EnglishName==name);return new(w.Id,new ModSlot?[8],new(Enemy:new(1e9,0)),catalog.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds,ScriptedAttacksOnly:true));}
        void Mods(Build b,params string[] names){for(var i=0;i<names.Length;i++){var m=catalog.Mods.Single(m=>m.EnglishName==names[i]);b.Slots[i]=new(m.Id,m.MaxRank);}}
        void Near(double a,double b,double tolerance=1e-6){if(!double.IsFinite(a)||Math.Abs(a-b)>tolerance)throw new Exception($"{a} != {b}");}
        void Yes(bool v){if(!v)throw new Exception("글레이브 비행 검증 불일치");}
        void Reject(Action a,string text){try{a();}catch(ArgumentException e){if(!e.Message.Contains(text))throw;return;}throw new Exception("잘못된 글레이브 입력 허용: "+text);}
        CombatTimelineResult Run(Build b,int trials=1){var r=CombatTimeline.Calculate(catalog,b,stances,trials);if(r.Unsupported.Length>0)throw new Exception(string.Join(";",r.Unsupported));return r;}
        check("글레이브12종:지상기폭 치확모드2배·공중1배·볼래틸도구분·KB/콤보는불변",()=>{
            foreach(var w in rules.Weapons.Where(w=>w.Raw.GetProperty("Class").GetString()=="Glaive"))
            {
                var b=B(w.Name);Mods(b,"True Steel","Killing Blow","Volatile Quick Return");b=b with {Scenario=b.Scenario with {Combo=4}};
                foreach(var thrown in new[]{"Throw","Charged Throw"})
                {
                    var ground=GlaiveFlightState.Calculate(catalog,b,thrown+" Recall Explosion");var air=b with {Melee=b.Melee! with {Conditions=new(){["AirborneThrow"]=1}}};var flying=GlaiveFlightState.Calculate(catalog,air,thrown+" Recall Explosion");
                    var raw=w.Raw.GetProperty("Attacks").EnumerateArray().Single(a=>a.GetProperty("AttackName").GetString()==thrown+" Recall Explosion");var baseCrit=raw.GetProperty("CritChance").GetDouble();Near(ground.CriticalChance,baseCrit*3.4);Near(flying.CriticalChance,baseCrit*2.2);Near(ground.Damage.Values.Sum(),flying.Damage.Values.Sum());Near(ground.Melee!.Brackets!.ComboMultiplier,4);
                    var bounce=GlaiveBounceMechanic.Calculate(catalog,b,thrown);var airBounce=GlaiveBounceMechanic.Calculate(catalog,air,thrown);Near(bounce.CriticalChance/airBounce.CriticalChance,3.4/2.2);Near(bounce.Melee!.Brackets!.HeavyAttackDamage,0);Near(bounce.Melee.Brackets.ComboMultiplier,1);
                }
            }
        });
        check("텐노카이 비행정지:남은시간보존·공중재발동불확실성도보존·귀환후만감소",()=>{
            var s=new TennokaiState(1,0,2);s.StartingOpportunity(2);s.SetFlightPaused(.5,true);Near(s.RemainingMinimum(5),1.5);s.SetFlightPaused(5,false);Near(s.RemainingMinimum(6),.5);Yes(s.Begin(6.49));
            var t=new TennokaiState(1,0,2);t.SetFlightPaused(0,true);t.Contact(1,1,0,0,false,true,0);Near(t.RemainingMinimum(8),2);t.SetFlightPaused(8,false);Yes(!t.Begin(10));
            var u=new TennokaiState(1,0,2);u.Contact(0,0,0,0,false,true,0);u.Contact(1,1,0,0,false,true,0);u.SetFlightPaused(2.5,true);Near(u.RemainingMinimum(10),0);Near(u.RemainingMaximum(10),.5);Reject(()=>u.Begin(10),"갱신");
        });
        check("글레이브 경로:기본3반사·귀환관통·볼래틸관통제외·지형폭발은직접명중아님",()=>{
            var s=new GlaiveFlightState();var f=new GlaiveFlight(0,3);s.Launch(f);for(var i=1;i<=3;i++)s.Contact(new(i*.1),3,1,false);Reject(()=>s.Contact(new(.4),3,1,false),"귀환");s.Contact(new(.4,Kind:GlaiveContactKind.Return),3,0,true);
            var p=new GlaiveFlightState();p.Launch(f);Reject(()=>p.Contact(new(.1,Kind:GlaiveContactKind.PunchThrough),3,2,true),"관통");Reject(()=>p.Contact(new(.1,Kind:GlaiveContactKind.PunchThrough,Thickness:3),3,2,false),"두께");
            var b=B();Mods(b,"Volatile Quick Return");b=b with {Encounter=b.Encounter! with {GlaiveFlights=[new(0,2,Contacts:[new(.5,Surface:new())])]}};var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="glaiveSurface")==1&&r.Events.Count(e=>e.Kind=="glaiveBounce")==1);Near(r.Points.Last().ComboPoints,0);Yes(!r.Events.Any(e=>e.Kind=="heavyCombo"));
        });
        check("글레이브 실제교전:볼래틸 후 수동기폭·대상별 방어층·폭발 한번만 콤보소모",()=>{
            var b=B();Mods(b,"Volatile Quick Return");b=b with {Scenario=b.Scenario with {Combo=4},Encounter=b.Encounter! with {GlaiveFlights=[new(0,2.5,Contacts:[new(.5)],Detonation:new(1))],NearbyEnemies=[new("near",2,Defense:new(1e9,2700))]}};
            var r=Run(b);Yes(r.Events.Count(e=>e.Kind=="glaiveContact")==1&&r.Events.Count(e=>e.Kind=="glaiveBounce")==2&&r.Events.Count(e=>e.Kind=="glaiveDetonation")==2);Yes(r.Events.Count(e=>e.Kind=="heavyCombo")==1);Near(r.Events.Single(e=>e.Kind=="heavyCombo").Amount,61);Near(r.Points.Last().ComboPoints,0);Yes(r.Enemies![1].DirectDamage>0&&r.Enemies[1].DirectDamage<r.DirectDamage);
            var saved=JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(b,Catalog.Json),Catalog.Json)!;Yes(saved.Encounter!.GlaiveFlights![0].Detonation!.Time==1);
        });
        check("글레이브 연속투척:회수전버퍼만0/1/2/3중첩·늦은입력거부·PP별도곱",()=>{
            var b=B();Mods(b,"Power Throw","Pressure Point");var flights=Enumerable.Range(0,5).Select(i=>new GlaiveFlight(i*.5,i*.5+.4,Throw:i+1,BufferedAt:i is >0 and <4?i*.5-.2:null,Contacts:[new(i*.5+.1)])).ToArray();b=b with {Encounter=b.Encounter! with {GlaiveFlights=flights}};
            var r=Run(b);Yes(r.Events.Where(e=>e.Kind=="powerThrow").Select(e=>e.Amount).SequenceEqual(new[]{0d,1,2,3,0}));
            var baseC=GlaiveFlightState.Calculate(catalog,b,"Charged Throw");var three=GlaiveFlightState.Calculate(catalog,b with {Melee=b.Melee! with {Conditions=new(){["ConsecutiveThrows"]=3}}},"Charged Throw");Near(three.Damage.Values.Sum()/baseC.Damage.Values.Sum(),4);
            Reject(()=>GlaiveFlightState.Validate([flights[0],flights[1] with {BufferedAt=.4}],1,b.WeaponId),"회수 직전");Reject(()=>Run(b with {Encounter=b.Encounter! with {Inputs=[new(.1,"GlaiveChainThrow")]}}),"중복");
        });
        check("글레이브 속도:최대파워스로우60%·첫경로검증·낮은랭크수치조작없음",()=>{
            var b=B();Mods(b,"Power Throw");var f=new GlaiveFlight(0,2,Charged:false,Contacts:[new(.9)],FirstLegDistance:28);GlaiveFlightState.CheckFirstLeg(b,f,GlaiveFlightState.Calculate(catalog,b,"Throw"));
            Reject(()=>GlaiveFlightState.CheckFirstLeg(b,f with {Contacts=[new(.5)]},GlaiveFlightState.Calculate(catalog,b,"Throw")),"최단");b.Slots[0]=b.Slots[0]! with {Rank=0};Reject(()=>GlaiveFlightState.CheckFirstLeg(b,f,GlaiveFlightState.Calculate(catalog,b,"Throw")),"최대 랭크");
        });
        check("글레이브 경로검증:동시투척·비행중검공격·오르비우스일반투척혼동거부",()=>{
            var b=B();Reject(()=>GlaiveFlightState.Validate([new(0,2),new(1,3,Throw:2)],1,b.WeaponId),"이전 글레이브");
            Reject(()=>Run(b with {Encounter=b.Encounter! with {ScriptedAttacksOnly=false,GlaiveFlights=[new(0,2)]}}),"비행 중");
            Reject(()=>GlaiveFlightState.Validate([new(0,2,Charged:false)],1,OrviusHoverState.WeaponId),"매달기");
        });
        check("케스트렐프라임:기본8회반사·VQR후4회·일반글레이브강제충격/몸체1m보완",()=>{
            var b=B("Kestrel Prime");Near(GlaiveFlightState.BaseBounces(b.WeaponId),8);Mods(b,"Volatile Quick Return");b=b with {Encounter=b.Encounter! with {GlaiveFlights=[new(0,2,Contacts:[new(.1),new(.2),new(.3),new(.4),new(.5,Kind:GlaiveContactKind.Return)])]}};
            var r=Run(b);Near(r.Events.Count(e=>e.Kind=="glaiveContact"),5);Near(r.Events.Count(e=>e.Kind=="glaiveBounce"),4);Near(r.Points.Last().ComboPoints,5);
            var plain=B();Yes(GlaiveFlightState.Calculate(catalog,plain,"Throw").Melee!.ForcedProcs.Contains("Impact"));Near(GlaiveFlightState.InnatePunchThrough(plain.WeaponId),1);Near(GlaiveFlightState.InnatePunchThrough(B("Pathocyst").WeaponId),0);
        });
    }
}
