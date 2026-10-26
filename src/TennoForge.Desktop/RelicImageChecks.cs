using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    internal sealed record RewardImageFixture(string Image,string[] ExpectedSlugs,string Note,int MinimumAutomatic=0,bool RequireExactOrder=false,bool UseSnapshot=false,bool Live=false,double MaximumSeconds=0);
    internal async Task<object> CheckRelicImages(string manifest,string dir,Action<FrameworkElement,string,int,int,double> render)
    {
        var fixtures=JsonSerializer.Deserialize<RewardImageFixture[]>(File.ReadAllText(manifest),Catalog.Json)!;
        var root=Path.GetDirectoryName(Path.GetFullPath(manifest))!;
        var checks=new List<string>();var reports=new List<object>();
        void Yes(bool value,string name){if(!value)throw new InvalidOperationException(name);checks.Add(name);}
        using var view=new RelicPage(new RelicMarketClient(Path.Combine(dir,"live-market-cache"),snapshotMode:fixtures.All(f=>f.UseSnapshot)));
        SelectPage(4);page.Content=view;view.Presence.SelectedIndex=0;view.Crossplay.IsChecked=true;
        foreach(var fixture in fixtures)
        {
            var key=Path.GetFileNameWithoutExtension(fixture.Image);
            var expected=fixture.ExpectedSlugs.Select(slug=>view.Catalog.Items.Single(x=>x.Slug==slug||x.Id==slug)).ToArray();
            if(fixture.UseSnapshot)
                foreach(var item in expected.Where(x=>x.Tradable).DistinctBy(x=>x.Id))await view.Market.RefreshSnapshotItemAsync(item,true,default);
            int requestsBefore=view.Market.RequestCount;
            view.ChooseImageFile=()=>Path.Combine(root,fixture.Image);
            var timer=Stopwatch.StartNew();
            if(fixture.Live)await view.ReadAsync(RelicOcr.Load(Path.Combine(root,fixture.Image)),live:true);
            else Invoke(view.LoadButton);
            await Idle();
            while(view.Busy){if(timer.Elapsed>TimeSpan.FromMinutes(3))throw new Exception(key+" 인식 제한 시간 초과");await Task.Delay(50);}
            await Idle();var elapsed=timer.Elapsed.TotalSeconds;
            var initial=view.LastRecognitionForCheck??throw new Exception("파일 불러오기 인식 결과 없음");
            var proposed=initial.Rewards.Select(x=>x.Candidates.FirstOrDefault()?.Item.Id).ToArray();
            if(fixture.RequireExactOrder)Yes(proposed.SequenceEqual(expected.Select(x=>x.Id))&&initial.Rewards.All(x=>x.Confident),key+" 모든 보상 개수·좌우 순서·자동 확정 일치");
            if(fixture.UseSnapshot)Yes(view.Market.RequestCount==requestsBefore,key+" OCR 시작부터 가격 표시까지 HTTP 요청 0회");
            if(fixture.MaximumSeconds>0)Yes(elapsed<fixture.MaximumSeconds,key+$" 인식·시세 {fixture.MaximumSeconds}초 이내");
            Yes(initial.Rewards.Where(x=>x.Confident).All(x=>expected.Any(item=>item.Id==x.Item?.Id)),key+" 잘못된 품목 자동 확정 없음");
            Yes(initial.Rewards.Count(x=>x.Confident)>=fixture.MinimumAutomatic,key+" 자동 확정 최소 기준");
            foreach(var card in view.Cards.Where(x=>x.Item is {Tradable:true}))
                Yes(card.Quote is {Error:null,Stale:false}&&card.Quote.ItemId==card.Item!.Id,key+" 사용자 선택 없이 실제 호가 자동 조회");
            render(this,key+"-recognized",1760,920,1);
            var before=new{elapsed,firstPricedSeconds=view.FirstPricedSeconds,requestsDuringRecognition=view.Market.RequestCount-requestsBefore,initial,proposed,automatic=view.Cards.Where(x=>x.Item is not null).Select(x=>new{item=x.Item,quote=x.Quote}).ToArray(),correctAtPosition=Enumerable.Range(0,expected.Length).Count(i=>i<proposed.Length&&proposed[i]==expected[i].Id)};
            if(expected.Length==0)
            {
                Yes(initial.Rewards.Length==0,key+" 비보상 화면 후보 생성 없음");
                Yes(view.Cards.All(x=>x.Item is null&&x.Quote is null),key+" 비보상 화면 가격 없음");
                reports.Add(new{fixture,before,negative=true});Save();continue;
            }
            while(view.Cards.Count>expected.Length){Invoke(view.Cards[^1].Remove);await Idle();}
            while(view.Cards.Count<expected.Length){Invoke(view.AddButton);await Idle();}
            var corrections=new List<object>();
            for(int i=0;i<expected.Length;i++)
            {
                var card=view.Cards[i];var needed=card.Item?.Id!=expected[i].Id;Invoke(card.Correct);await Idle();
                var search=card.Search;var old=card.Item;search.QueryInput.Text=expected[i].Name;await Idle();
                Yes(search.IsDropDownOpen&&card.Item?.Id==old?.Id,key+$" 보상 {i+1} 입력만으로 확정 안 함");
                RelicKey(search.QueryInput,Key.Down);
                Yes(search.Suggestions.SelectedItem is RelicItem selected&&selected.Id==expected[i].Id,key+$" 보상 {i+1} 방향키 정답 후보");
                if(i==0)render((FrameworkElement)search.SearchPopup.Child,key+"-correction",390,360,1);
                RelicKey(search.QueryInput,Key.Enter);await card.RefreshAsync(false);
                Yes(card.Item?.Id==expected[i].Id&&!search.IsDropDownOpen,key+$" 보상 {i+1} Enter 확정");
                Yes(card.Ducats.Text==expected[i].Ducats.ToString("N0"),key+$" 보상 {i+1} 두캇 일치");
                if(expected[i].Tradable)
                    Yes(card.Quote is {Error:null,Stale:false}&&card.Quote.ItemId==expected[i].Id,key+$" 보상 {i+1} 실제 API 품목·호가");
                else Yes(card.Price.Text=="거래 불가"&&!card.MarketLink.IsEnabled,key+" 거래 불가 보상");
                corrections.Add(new{slot=i+1,needed,item=card.Item,quote=card.Quote,price=card.Price.Text,ducats=card.Ducats.Text});
            }
            view.UpdateHighlights();
            foreach(var expander in LayoutDiagnostics.Tree(view).OfType<Expander>())expander.IsExpanded=false;
            render(this,key+"-compared",1760,920,1);
            Yes(LayoutDiagnostics.Inspect(view).Length==0,key+" 실제 이름·시세 카드 겹침·잘림 없음");
            foreach(var scale in new[]{1.25,1.5})
            {
                render(this,key+$"-scale-{scale}",1760,920,scale);
                Yes(LayoutDiagnostics.Inspect(view).Length==0,key+$" 배율 {scale} 배치");
            }
            reports.Add(new{fixture,before,corrections,highlight=view.Cards.Select(x=>x.Best.Text).ToArray()});Save();
        }
        return new{passed=true,checks=checks.Count,details=checks,fixtures=reports,liveMarket=true,imagesUploaded=false,gameReturn="사용자 요청에 따라 실제 게임 복귀 검수는 보류. 이 검수에 포함하지 않음."};
        void Save()=>File.WriteAllText(Path.Combine(dir,"fixtures.json"),JsonSerializer.Serialize(reports,Catalog.Json));
    }
}
