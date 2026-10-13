using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    private record CatalogRow(string Name,string EnglishName,string Category,string Source,object Item)
    { public string Display=>Name+(Name==EnglishName?"":"  ·  "+EnglishName)+"\n"+Category; }
    private FrameworkElement? catalogPage;
    private readonly ComboBox catalogKind=new(){ItemsSource=new[]{"무기","스탠스·공통 동작","아케인","모드","검증 기록"},SelectedIndex=0,Width=160};
    private readonly ComboBox catalogCategory=new(){Width=170};
    private readonly TextBox catalogSearch=new(){MinWidth=180};
    private readonly ListBox catalogList=new(){DisplayMemberPath="Display",BorderThickness=new(0)};
    private readonly TextBlock catalogCount=T("",13,"#596B85"),catalogDetail=T("",14);
    private readonly StackPanel catalogDetailPanel=new();
    private CatalogRow[] catalogRows=[];
    private void ShowCatalog()
    {catalogPage??=CreateCatalogPage();page.Content=catalogPage;SelectPage(2);}
    private FrameworkElement CreateCatalogPage()
    {
        var root=new Grid();root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var toolbar=new DockPanel();DockPanel.SetDock(catalogKind,Dock.Left);toolbar.Children.Add(catalogKind);DockPanel.SetDock(catalogCategory,Dock.Left);catalogCategory.Margin=new(8,0,8,0);toolbar.Children.Add(catalogCategory);catalogSearch.ToolTip="한국어·영문 이름 또는 무기군 검색";
        var searchHost=new Grid();searchHost.Children.Add(catalogSearch);
        var placeholder=T("이름 · 무기군 검색",14,"#596B85");placeholder.Margin=new(11,0,0,0);placeholder.VerticalAlignment=VerticalAlignment.Center;placeholder.IsHitTestVisible=false;searchHost.Children.Add(placeholder);
        catalogSearch.TextChanged+=(_,_)=>placeholder.Visibility=catalogSearch.Text.Length==0?Visibility.Visible:Visibility.Collapsed;toolbar.Children.Add(searchHost);
        root.Children.Add(Stack(T("전체 자료",20),toolbar,Spacer(10),catalogCount));
        catalogList.SetResourceReference(Control.BackgroundProperty,"SolidSurfaceBrush");catalogList.SetResourceReference(Control.ForegroundProperty,"TextBrush");
        VirtualizingPanel.SetIsVirtualizing(catalogList,true);VirtualizingPanel.SetVirtualizationMode(catalogList,VirtualizationMode.Recycling);ScrollViewer.SetCanContentScroll(catalogList,true);
        catalogList.SetResourceReference(ItemsControl.ItemContainerStyleProperty,typeof(ListBoxItem));
        catalogDetail.TextWrapping=TextWrapping.Wrap;
        var body=Columns(catalogList,Scroll(catalogDetailPanel),1,1.2);Grid.SetRow(body,1);root.Children.Add(body);
        catalogKind.SelectionChanged+=(_,_)=>LoadCatalogRows();catalogCategory.SelectionChanged+=(_,_)=>FilterCatalogRows();catalogSearch.TextChanged+=(_,_)=>FilterCatalogRows();catalogList.SelectionChanged+=(_,_)=>ShowCatalogDetail();
        LoadCatalogRows();return root;
    }
    private void LoadCatalogRows()
    {
        var source=ReferenceCatalog.Load();
        catalogRows=catalogKind.SelectedIndex switch{
            0=>source.Weapons.Select(w=>new CatalogRow(w.Name,w.EnglishName,Category(w.Category)+" / "+w.WeaponType,w.Source,w)).ToArray(),
            1=>stanceCatalog.Stances.Select(s=>new CatalogRow(s.Name,s.Id,s.WeaponType+(s.ConclaveOnly?" / 콘클레이브":s.IsDefault?" / 기본":s.UniqueToWeapon?" / 무기 전용":" / 스탠스"),s.Source,s)).ToArray(),
            2=>source.Arcanes.Select(a=>new CatalogRow(a.Name,a.EnglishName,Category(a.Category),a.Source,a)).ToArray(),
            3=>source.Mods.Select(m=>new CatalogRow(m.Name,m.EnglishName,m.Category,m.Source,m)).ToArray(),
            _=>source.Sources.Concat(MeleeRuleCatalog.Load().Sources).DistinctBy(s=>(s.Title,s.Revision)).Select(s=>new CatalogRow(s.Title,s.Title,"고정 위키 원본",s.Url,s)).ToArray()};
        catalogCategory.ItemsSource=new[]{"전체"}.Concat(catalogRows.Select(r=>r.Category).Distinct().OrderBy(x=>x)).ToArray();catalogCategory.SelectedIndex=0;FilterCatalogRows();
    }
    private void FilterCatalogRows()
    {
        var query=catalogSearch.Text.Trim();var category=catalogCategory.SelectedItem as string;
        var filtered=catalogRows.Where(r=>(category is null or "전체"||r.Category==category)&&(r.Name.Contains(query,StringComparison.OrdinalIgnoreCase)||r.EnglishName.Contains(query,StringComparison.OrdinalIgnoreCase)||r.Category.Contains(query,StringComparison.OrdinalIgnoreCase))).OrderBy(r=>r.Name).ToArray();
        catalogList.ItemsSource=filtered;catalogCount.Text=$"{filtered.Length:N0} / {catalogRows.Length:N0}개 · {ReferenceCatalog.Load().Version} · 자료 등록과 효과 계산 지원은 별도 표시";if(filtered.Length>0)catalogList.SelectedIndex=0;else{catalogDetailPanel.Children.Clear();catalogDetailPanel.Children.Add(T("검색 결과가 없습니다.",14));}
    }
    private void ShowCatalogDetail()
    {
        catalogDetailPanel.Children.Clear();if(catalogList.SelectedItem is not CatalogRow row)return;
        catalogDetailPanel.Children.Add(T(row.Name,20));catalogDetailPanel.Children.Add(T(row.EnglishName+" · "+row.Category,13,"#596B85"));
        var text=new StringBuilder();
        switch(row.Item)
        {
            case ReferenceWeapon w:
                text.AppendLine(ExaltedRules.Find(w.InternalId) is not null?"익절티드 어빌리티별 피해·랭크·위력 계산 지원":w.Calculable?"일반 근접 공격 계산 지원":"자료 열람 · "+string.Join(" ",w.Limitations));
                if(MeleeRuleCatalog.Load().Weapons.SingleOrDefault(p=>p.Id==w.InternalId) is {} profile)
                {
                    foreach(var passive in profile.Passives)text.AppendLine("조건 수치 · "+passive.Label);
                    var coverage=MeleeMechanicCoverage.For(profile);
                    foreach(var detail in coverage.Implemented)text.AppendLine("계산 연결 · "+detail);
                    foreach(var detail in coverage.Remaining)text.AppendLine("남은 구현 · "+detail);
                    foreach(var detail in coverage.Deferred??[])text.AppendLine("다음 단계 · "+detail);
                    foreach(var detail in coverage.SourceIssues)text.AppendLine("원문 근거 제한 · "+detail);
                    if(coverage.Implemented.Length+coverage.Remaining.Length+coverage.SourceIssues.Length>0)text.AppendLine("위 범위는 고유 효과별 지원입니다. 기본 공격 지원이 무기 전체 구현 완료를 뜻하지는 않습니다.");
                }
                if(!w.Localized)text.AppendLine("공식 한국어 식별자 대응 없음 · 원문 이름 표시");
                foreach(var (key,label) in new[]{("Mastery","마스터리"),("MaxRank","최대 랭크"),("Disposition","리벤 기질"),("MeleeRange","근접 사거리"),("Magazine","탄창"),("Reload","재장전")})if(w.Raw.TryGetProperty(key,out var v))text.AppendLine(label+": "+v);
                if(w.Raw.TryGetProperty("Attacks",out var attacks)&&attacks.ValueKind==JsonValueKind.Array)
                foreach(var a in attacks.EnumerateArray())
                {
                    text.AppendLine("\n"+(a.TryGetProperty("AttackName",out var n)?n.ToString():"공격"));
                    if(a.TryGetProperty("Damage",out var d)&&d.ValueKind==JsonValueKind.Object)text.AppendLine(string.Join(" · ",d.EnumerateObject().Select(x=>(Enum.TryParse<DamageType>(x.Name,out var type)?Labels.Damage(type):x.Name)+" "+x.Value)));
                    foreach(var (key,label) in new[]{("CritChance","치명타 확률"),("CritMultiplier","치명타 배수"),("StatusChance","상태 확률"),("FireRate","공격 속도"),("Multishot","멀티샷")})if(a.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number)text.Append(label+" "+(key is "CritChance" or "StatusChance"?v.GetDouble().ToString("P1"):v.GetDouble().ToString("0.###"))+"   ");
                    text.AppendLine();
                }
                if(w.Calculable||ExaltedRules.Find(w.InternalId) is not null)catalogDetailPanel.Children.Add(Button("모드에서 열기",()=>{weaponClass.SelectedIndex=0;weapon.SelectedItem=catalog.Weapons.Single(x=>x.Id==w.InternalId);ShowModding();}));
                break;
            case Stance s:
                foreach(var c in s.Combos){text.AppendLine($"{c.InputLabel} · {c.Name}\n{c.HitCount}타 · 배율 합 {c.MotionSum:0.##}× · 시간 {(c.Duration is {} duration?duration.ToString("0.###")+"초":"미기재 · 계산하려면 시간 가정 필요")}\n타격별 배율·상태는 확장 근접 규칙에서 처리합니다. 투사체 연결·CO 보정·TTK 지원은 선택한 무기와 조건에 따라 확인합니다.\n");}
                catalogDetailPanel.Children.Add(Button("타격별 배율·강제 상태 보기",()=>new StanceWindow(s,stanceCatalog){Owner=this}.ShowDialog()));break;
            case ArcaneDefinition a:
                var arcaneRule=MeleeRuleCatalog.Load().Arcanes.SingleOrDefault(r=>r.Id==a.Id);
                text.AppendLine(arcaneRule is null?"자료 열람 · 근접 효과 계산 범위 밖":arcaneRule.Code=="Utility"?"비공격 효과 · 일반 근접 직접 피해 변화 없음":"확장 근접 조건 규칙 · 발동 여부와 중첩을 지정해 계산");
                if(arcaneRule?.Code is "Influence" or "Afflictions" or "Epidemic" or "Contagion")text.AppendLine("전파·추가 상태·별도 투사체의 피해는 기본 직접 DPS·TTK와 구분합니다. 현재 구현 범위는 결과의 발동·범위 효과 및 지원 범위에서 확인합니다.");
                text.AppendLine("최대 랭크 "+a.MaxRank);AppendRanks(text,a.RankText,a.Description);break;
            case ReferenceMod m:
                var modRule=MeleeRuleCatalog.Load().Mods.SingleOrDefault(x=>x.Id==m.Id);
                text.AppendLine(modRule is null?"자료 열람":modRule.Unsupported.Length>0?string.Join(" ",modRule.Unsupported):"확장 근접 규칙 · 랭크와 조건을 브라켓별로 적용");
                if(modRule is not null)
                {
                    var effects=(modRule.Effects??[]).Concat(modRule.Conditional??[]).GroupBy(p=>p.Key).ToDictionary(g=>g.Key,g=>g.Sum(p=>p.Value));
                    foreach(var effect in effects)text.AppendLine(EffectBrackets.Name(effect.Key)+" · "+effect.Value.ToString("G6"));
                    foreach(var note in MeleeEffectCoverage.Notes(effects))text.AppendLine(note);
                }
                AppendRanks(text,m.RankText,m.Description);break;
            case WikiSource s:
                text.AppendLine($"개정 {s.Revision}\n원본 변경 시각 {s.Timestamp}\nSHA-256\n{s.Sha256}\n");
                var audit=ReferenceCatalog.Load().Audit;text.AppendLine($"원본 구문 주의 항목 {audit.GetProperty("sourceIssues").GetArrayLength()}개\n공식 Export 수치 차이 {audit.GetProperty("officialDifferences").GetArrayLength()}개\n위키를 계산 기준으로 유지하고 차이를 검증 보고서에 보존했습니다.\n\n콘클레이브·엑절티드·조립식 부품·인카논 공격을 일반 무기에 합쳐 계산하지 않습니다.");break;
        }
        catalogDetail.Text=text.ToString();catalogDetailPanel.Children.Add(catalogDetail);
        catalogDetailPanel.Children.Add(Button("위키 원문",()=>Process.Start(new ProcessStartInfo(row.Source){UseShellExecute=true})));
        foreach(var action in catalogDetailPanel.Children.OfType<Button>())action.Margin=new(0,8,0,16);
    }
    private static void AppendRanks(StringBuilder text,string[][] ranks,string fallback)
    {if(ranks.Length==0){text.AppendLine(fallback);return;}for(var i=0;i<ranks.Length;i++)text.AppendLine($"\n랭크 {i}\n"+string.Join("\n",ranks[i]));}
    private static string Category(string category)=>category switch{"primary" or "Primary"=>"주무기","secondary" or "Secondary"=>"보조무기","melee" or "Melee"=>"근접","archwing"=>"아크윙","companion"=>"동반자","railjack"=>"레일잭","modular"=>"조립식 부품","misc"=>"기타","Warframe"=>"워프레임","Operator"=>"오퍼레이터",_=>category};
    public void CatalogPreviewForCheck(int kind){ShowCatalog();catalogKind.SelectedIndex=kind;catalogSearch.Text="";}
    private void RunExpandedSmokeChecks()
    {
        var original=ReadBuild();
        CatalogPreviewForCheck(0);if(catalogList.Items.Count!=911)throw new Exception("전체 무기 목록 누락");
        catalogSearch.Text="Nikana Prime";if(catalogList.Items.Count!=1)throw new Exception("자료실 영문 검색 실패");
        CatalogPreviewForCheck(1);if(catalogList.Items.Count!=134)throw new Exception("전체 스탠스 자료 누락");
        CatalogPreviewForCheck(2);if(catalogList.Items.Count!=166)throw new Exception("전체 아케인 자료 누락");
        catalogSearch.Text="Melee Duplicate";if(catalogList.Items.Count!=1||!catalogDetail.Text.Contains("랭크 5"))throw new Exception("아케인 랭크 설명 누락");
        CatalogPreviewForCheck(3);if(catalogList.Items.Count!=1550)throw new Exception("전체 모드 자료 누락");
        ShowModding();
        weaponClass.SelectedItem="Hammer";if(weapon.Items.Cast<Weapon>().Any(w=>w.WeaponType!="Hammer"))throw new Exception("무기군 필터 실패");ApplyBuild(original);
        var forward=((Stance)stance.SelectedItem).Combos.Single(c=>c.Key=="Forward");stanceCombo.SelectedItem=forward;
        if(currentStance?.Combo.Key!="Forward"||currentStance.RawDps==0)throw new Exception("전진 콤보 계산 연결 실패");ApplyBuild(original);
        var a=ReferenceCatalog.Load().Arcanes.Single(a=>a.EnglishName=="Melee Exposure");var withArcane=original with{Encounter=new(EncounterMode.FirstHit),Arcanes=new(Melee:new(a.Id,5),ExposureStacks:4)};ApplyBuild(withArcane);
        if(current?.Damage.ContainsKey(DamageType.Corrosive)!=true||ReadBuild().Arcanes?.ExposureStacks!=4)throw new Exception("아케인 입력/계산 연결 실패");
        ApplyBuild(JsonSerializer.Deserialize<Build>(JsonSerializer.Serialize(ReadBuild(),Catalog.Json),Catalog.Json)!);
        if(currentStance is null||Math.Abs(hitPlot.Points.Sum(p=>p.Y)-currentStance.TargetDamage)>.001)throw new Exception("아케인·팩션 차트/저장 복원 실패");
        ApplyBuild(original);
        var unsupported=catalog.Mods.First(m=>m.CalculationIssues is {Length:>0});board.DropOnSlot(new(unsupported.Id),0);
        if(current is not null||capacityResult is null)throw new Exception("미지원 모드 경고 또는 수용량 보존 실패");ApplyBuild(original);
    }
}
