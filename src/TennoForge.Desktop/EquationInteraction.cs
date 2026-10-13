using System.Windows;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private readonly DpsEquationView equationView=new();
    private Build? equationBuild;
    private (string Kind,string Id,string Name)? equationSource;
    private readonly Dictionary<(string Kind,string Id),(string[] Terms,string Hint)> equationHoverCache=[];
    private void UpdateEquation(Build build,Calculation calculation,StanceCalculation? selectedStance)
    {
        equationBuild=build;equationHoverCache.Clear();
        if(build.Encounter?.ScriptedAttacksOnly==true){equationBuild=null;equationView.SetEquation(null);return;}
        equationView.SetEquation(DpsEquation.From(calculation,selectedStance));RefreshEquationHover();
    }
    private void WatchEquationSource(FrameworkElement owner,string kind,Func<(string Id,string Name)?> read)
    {
        void Enter(){if(read() is {} value)SetEquationSource(kind,value.Id,value.Name);}
        void Leave(){if(read() is {} value&&equationSource is {} currentSource&&currentSource.Kind==kind&&currentSource.Id==value.Id){equationSource=null;equationView.Highlight([]);}}
        owner.MouseEnter+=(_,_)=>Enter();owner.MouseLeave+=(_,_)=>Leave();owner.GotKeyboardFocus+=(_,_)=>Enter();owner.LostKeyboardFocus+=(_,_)=>Leave();owner.Unloaded+=(_,_)=>Leave();
    }
    private void SetEquationSource(string kind,string id,string name)
    {equationSource=(kind,id,name);RefreshEquationHover();}
    private void RefreshEquationHover()
    {
        if(equationSource is not {} source||equationBuild is not {} b||equationView.Equation is not {} equation)return;
        var cacheKey=(source.Kind,source.Id);
        if(!equationHoverCache.TryGetValue(cacheKey,out var change))
        {
            try
            {
                Build? without=null;var hint="";
                if(source.Kind=="mod")
                {
                    var index=Array.FindIndex(b.Slots,s=>s?.ModId==source.Id);
                    if(index>=0)
                    {var slots=(ModSlot?[])b.Slots.Clone();slots[index]=null;without=b with {Slots=slots};if(MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==source.Id).Effects?.ContainsKey("Riven")==true)without=without with {Riven=null};}
                    else if(b.Melee?.Exilus?.ModId==source.Id)without=b with {Melee=b.Melee with {Exilus=null}};
                    else hint=source.Name+" · 미장착";
                }
                else if(source.Kind=="arcane"&&b.Arcanes is {} arcanes)
                {
                    ArcaneSlot? Inactive(ArcaneSlot? slot)=>slot?.Id==source.Id?slot with {Active=false}:slot;
                    without=b with {Arcanes=arcanes with {Melee=Inactive(arcanes.Melee),Warframe1=Inactive(arcanes.Warframe1),Warframe2=Inactive(arcanes.Warframe2),Operator=Inactive(arcanes.Operator)}};
                }
                else if(source.Kind=="stance"&&b.Stance is not null)
                    without=b with {Stance=null,Melee=b.Melee! with {Attack="Normal Attack",Tennokai=false},Scenario=b.Scenario with {MotionMultiplier=1,BaseInterval=1}};
                else if(source.Kind=="evolution"&&b.Melee is {} melee)
                    without=b with {Melee=melee with {Evolutions=(melee.Evolutions??[]).Where(id=>id!=source.Id).ToArray()}};
                else if(source.Kind=="buff"&&b.Melee is {} buffMelee)
                    without=b with {Melee=buffMelee with {Buffs=source.Id=="roar"?(buffMelee.Buffs??new()) with {Roar=0}:(buffMelee.Buffs??new()) with {Eclipse=0}}};
                else if(source.Kind=="condition")
                    without=source.Id switch
                    {
                        "armor"=>b with {Scenario=b.Scenario with {Enemy=(b.Scenario.Enemy??new()) with {Armor=0}}},
                        "combo"=>b with {Scenario=b.Scenario with {Combo=1}},
                        "status"=>b with {Scenario=b.Scenario with {ExistingStatusTypes=0},Melee=b.Melee! with {Statuses=[],Conditions=(b.Melee.Conditions??[]).Where(p=>p.Key!="Lifted").ToDictionary()},Arcanes=b.Arcanes is {} a?a with {Frozen=false}:null},
                        "faction"=>b with {Melee=b.Melee! with {Faction="Neutral"}},
                        "shields"=>b with {Melee=b.Melee! with {Shields=0}},
                        "overguard"=>b with {Melee=b.Melee! with {Overguard=0}},
                        "tennokai"=>b with {Melee=b.Melee! with {Tennokai=false}},
                        _=>null
                    };
                string[] terms=[];
                if(without is not null)
                {
                    var evaluated=EvaluateBuild(without);terms=equation.ChangedTerms(DpsEquation.From(evaluated.Stats,evaluated.Stance));
                    hint=source.Name+(terms.Length>0?"  →  "+string.Join(" · ",equation.Terms.Where(t=>terms.Contains(t.Id)).Select(t=>t.Label)):" · 현재 조건의 직접 DPS 변화 없음");
                    if(terms.Length==0&&source.Kind=="mod")
                    {
                        var rule=MeleeRuleCatalog.Load().Mods.Single(m=>m.Id==source.Id);
                        if(rule.Effects?.ContainsKey("StatusDamage")==true||rule.Conditional?.ContainsKey("StatusVulnerability")==true)hint=source.Name+" → 상태 피해 · 직접 DPS 합산 대상 아님";
                        else if(rule.Effects?.ContainsKey("ConditionOverload")==true&&TargetStatusRules.Count(b)==0)hint=SelectedEncounter is EncounterMode.FirstHit or EncounterMode.Primer?"컨디션 오버로드 · 첫 타격 전에 걸린 대상 상태 없음":"컨디션 오버로드 → 교전 중 상태에 따라 갱신 · 위 수식은 시작 상태";
                        else if(rule.Condition is {} condition&&(b.Melee?.Conditions?.GetValueOrDefault(condition)??0)==0)hint=source.Name+" · 발동 조건 0 / "+rule.Cap;
                    }
                }
                else if(hint.Length==0)hint=source.Name+" · 현재 계산에 선택되지 않음";
                change=(terms,hint);equationHoverCache[cacheKey]=change;
            }
            catch(ArgumentException){change=([],source.Name+" · 단독 효과를 분리할 수 없는 조건");}
        }
        equationView.Highlight(change.Terms,change.Hint);
    }
}
