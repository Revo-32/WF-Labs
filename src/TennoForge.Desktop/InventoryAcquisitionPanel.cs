using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class InventoryPage
{
    internal readonly Button RefreshQuoteButton;
    internal readonly TextBlock QuoteStatus=Text("시세는 판매자의 등록 호가입니다. 거래 부품만 조회합니다.",13,"MutedBrush");
    internal Func<RelicItem,CancellationToken,Task<MarketQuote>>? QuoteOverride {get;set;}
    private CancellationTokenSource? quoteCancellation;
    private string? quoteItemId;

    private Expander BatchView()
    {
        var body=new StackPanel();var expander=new Expander{Header="현재 재료로 함께 준비할 장비",Content=body,Margin=new(0,16,0,0)};
        expander.Expanded+=(_,_)=>
        {
            if(body.Children.Count>0)return;
            var batch=MasteryBatchPlanner.Recommend(CatalogData,Snapshot,prices);
            body.Children.Add(Text($"최대 12개 추천 · 마스터리 +{batch.MasteryPoints:N0} · 추가 {batch.Credits:N0} 크레딧",16,"AccentBrush"));
            body.Children.Add(Text("동일 재료·크레딧을 중복 사용하지 않습니다. 설계도 구매·해금 조건은 각 획득처를 확인하세요. 재료 수집·실제 랭작 시간은 제작 대기시간과 별도입니다.",14,"MutedBrush"));
            body.Children.Add(Text($"동시 제작 시 가장 긴 제작 대기 {TimeSpan.FromSeconds(batch.ParallelBuildSeconds):g} · 제작시간의 가중치는 낮게 적용",14,"MutedBrush"));
            if(batch.Plans.Length==0)body.Children.Add(Text("현재 재료로 준비가 끝나는 새 장비가 없습니다. 보유 장비 랭작과 위 추천의 부족한 재료를 먼저 확인하세요.",15));
            foreach(var plan in batch.Plans)
            {
                var button=ActionButton($"{plan.Item.Name} · +{plan.AvailablePoints:N0} · {plan.Credits:N0} Cr",()=>
                {
                    Search.Text="";Category.SelectedIndex=0;Ownership.SelectedIndex=0;
                    MasteryList.SelectedItem=MasteryList.Items.Cast<InventoryRow>().FirstOrDefault(r=>r.Id==plan.Item.Id);MasteryList.ScrollIntoView(MasteryList.SelectedItem);
                });button.Margin=new(0,8,0,0);button.HorizontalContentAlignment=HorizontalAlignment.Left;
                button.Content=Text($"{plan.Item.Name} · +{plan.AvailablePoints:N0}\n{plan.Credits:N0} 크레딧 · {TimeSpan.FromSeconds(plan.BuildSeconds):g}",14);body.Children.Add(button);
            }
        };return expander;
    }

    private FrameworkElement AcquisitionView(string id)
    {
        var panel=new StackPanel();var entry=InventoryAcquisition.Find(id);
        if(entry is null||entry.Routes.Length==0)
        {
            panel.Children.Add(Text(items.GetValueOrDefault(id)?.Category=="Resources"?"필요한 자원은 해당 행성·미션에서 수집하세요. 보유하지 않은 자원 설계도를 구매 경로로 가정하지 않습니다.":"인게임 코덱스의 획득 경로와 연결된 원문을 확인하세요.",14,"MutedBrush"));return panel;
        }
        FrameworkElement Route(AcquisitionRoute route)
        {
            var stack=new StackPanel{Margin=new(0,0,0,12)};
            stack.Children.Add(Text(route.Location,16));
            if(route.Costs.Length>0)stack.Children.Add(Text(string.Join(" + ",route.Costs.Select(c=>$"{c.Amount:N0} {c.Currency}")),16,"AccentBrush"));
            if(route.Kind=="market"&&route.Costs.Any(c=>c.Currency=="플래티넘"))stack.Children.Add(Text("상점 표준가 · 할인·개인 쿠폰 별도",13,"MutedBrush"));
            if(!string.IsNullOrWhiteSpace(route.Detail))stack.Children.Add(Text(route.Detail,14,"MutedBrush"));
            if(!string.IsNullOrWhiteSpace(route.Condition))stack.Children.Add(Text(route.Condition,14,"MutedBrush"));
            if(route.Chance is {} chance)stack.Children.Add(Text($"해당 보상 표의 1회 확률 {chance:0.##}%",13,"MutedBrush"));
            if(Uri.TryCreate(route.Source,UriKind.Absolute,out var uri)&&uri.Scheme=="https")
            {
                var link=ActionButton("출처 열기 ↗",()=>{try{Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});}catch(Exception e)when(e is System.ComponentModel.Win32Exception or InvalidOperationException){Status.Text="출처를 열지 못했습니다. 기본 웹 브라우저 설정을 확인하세요.";}});link.FontSize=12;link.Padding=new(8,3,8,3);link.MinHeight=26;link.HorizontalAlignment=HorizontalAlignment.Left;stack.Children.Add(link);
            }
            return stack;
        }
        var routes=entry.Routes.DistinctBy(r=>(r.Kind,r.Location,r.Detail,string.Join("|",r.Costs.Select(c=>$"{c.Currency}:{c.Amount}")))).ToArray();
        foreach(var route in routes.Take(4))panel.Children.Add(Route(route));
        if(routes.Length>4)
        {
            var rest=new StackPanel();foreach(var route in routes.Skip(4))rest.Children.Add(Route(route));
            panel.Children.Add(new Expander{Header=$"추가 획득처 {routes.Length-4}개",Content=rest});
        }
        var evidence=new StackPanel();evidence.Children.Add(Text($"WF Wiki 개정 {entry.Revision:N0} · 자료 수집 {InventoryAcquisition.Document.RetrievedUtc.LocalDateTime:yyyy-MM-dd}",12,"MutedBrush"));
        if(!string.IsNullOrWhiteSpace(entry.Original))evidence.Children.Add(Text(entry.Original,13,"MutedBrush"));
        panel.Children.Add(new Expander{Header="획득처 원문·검증 자료",Content=evidence,Margin=new(0,0,0,12)});return panel;
    }

    private RelicItem[] QuoteTargets(MasteryPlan plan)
    {
        var catalog=InventoryMarket.Items.ToDictionary(x=>x.Id);
        return plan.Requirements.Where(r=>r.Missing>0&&!r.CreditBlueprint).Select(r=>items.GetValueOrDefault(r.Id)?.MarketId)
            .Where(id=>id is not null&&catalog.ContainsKey(id)).Select(id=>catalog[id!]).Where(i=>i.Tradable).DistinctBy(i=>i.Id).ToArray();
    }
    internal async Task RefreshSelectedPricesAsync()
    {
        if(disposed||quoteCancellation is not null||MasteryList.SelectedItem is not InventoryRow{Model:MasteryPlan selected})return;
        var targets=QuoteTargets(selected);if(targets.Length==0)return;
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);quoteCancellation=cancellation;quoteItemId=selected.Item.Id;QuoteStatus.Visibility=Visibility.Visible;RefreshQuoteButton.Content="시세 조회 취소";
        int completed=0,available=0,failed=0;var preferences=RelicPreferences.Read();var presence=preferences.Presence==1?MarketPresence.Online:MarketPresence.InGame;
        try
        {
            foreach(var target in targets)
            {
                QuoteStatus.Text=$"{selected.Item.Name} · 부품 판매 호가 조회 {completed+1}/{targets.Length}";
                var quote=QuoteOverride is {} lookup?await lookup(target,cancellation.Token):await market.QuoteAsync(target,presence,preferences.Crossplay,true,cancellation.Token);
                if(disposed)return;
                foreach(var item in items.Values.Where(i=>i.MarketId==target.Id))
                {
                    if(quote.SellMin is >0 and var p){prices[item.Id]=new((double)p,quote.FetchedUtc,quote.Stale);available++;}
                    else if(quote.Error is null)prices.Remove(item.Id); // no seller is not a free item
                }
                if(quote.Error is not null)failed++;completed++;
            }
            QuoteStatus.Text=$"{selected.Item.Name} · {available}개 호가 확인 · {DateTime.Now:HH:mm:ss}"+(failed>0?$" · {failed}개 조회 실패, 저장 시세 유지":" · 등록 판매자 없으면 합계에서 확정하지 않습니다.");
        }
        catch(OperationCanceledException){if(!disposed)QuoteStatus.Text="시세 조회를 취소했습니다. 확인된 호가는 유지합니다.";}
        catch(Exception e)when(e is System.IO.IOException or InvalidOperationException or System.Net.Http.HttpRequestException){if(!disposed)QuoteStatus.Text="시세 조회 실패 · "+e.Message;}
        finally
        {
            quoteCancellation=null;if(!disposed){RefreshQuoteButton.Content="이 장비의 부품 시세 갱신";plansDirty=true;Refresh();}
        }
    }
}
