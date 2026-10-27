using System.Net;
using System.Text;
using System.Text.Json;
using TennoForge.Core;

internal static class RelicChecks
{
    internal static void Run(Action<string,Action> check)
    {
        RelicMatchingEquivalenceChecks.Run(check);
        var catalog=RelicCatalog.Bundled();var nikana=catalog.Items.Single(x=>x.Slug=="nikana_prime_blade");
        void Yes(bool b){if(!b)throw new Exception("성유물 조건 불일치");}
        check("공유 시세: 저장본 즉시 조회·한 응답으로 두 거래자 필터·재시작",()=>Snapshot().GetAwaiter().GetResult());
        check("공유 시세: 프라임 세트도 저장 대상, 보상 OCR 목록과 분리",()=>PriceCatalog().GetAwaiter().GetResult());
        async Task PriceCatalog()
        {
            var dir=Path.Combine("artifacts","price-catalog-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            var root=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText("data/relic-market-items.json"))!;
            root["retrievedUtc"]=DateTimeOffset.UtcNow;var all=root["data"]!.AsArray();
            var set=all[0]!.DeepClone();set["id"]="test-prime-set";set["slug"]="test_prime_set";set["tags"]=new System.Text.Json.Nodes.JsonArray("prime","set");all.Add(set);
            await File.WriteAllTextAsync(Path.Combine(dir,"items.json"),root.ToJsonString());
            var handler=new Handler{Fail=true};using var client=new RelicMarketClient(dir,handler,snapshotMode:true);
            Yes((await client.PriceItemsAsync(default)).Any(i=>i.Id=="test-prime-set"));
            Yes(!(await client.CatalogAsync(false,default)).Catalog.Items.Any(i=>i.Id=="test-prime-set"));
            Yes(handler.Calls==0&&RelicMarketClient.SnapshotInterval==TimeSpan.FromHours(12));
            var firstRun=Path.Combine(dir,"first-run");var online=new Handler{Body=root.ToJsonString()};
            using var firstClient=new RelicMarketClient(firstRun,online,snapshotMode:true);
            Yes((await firstClient.PriceItemsAsync(default)).Any(i=>i.Id=="test-prime-set")&&online.Calls==1);
        }
        async Task Snapshot()
        {
            var dir=Path.Combine("artifacts","price-snapshot-check-"+Guid.NewGuid().ToString("N"));
            using var data=Orders(nikana.Id,[new("a","sell",20,"ingame"),new("b","sell",15,"online")]);
            var handler=new Handler{Body=data.RootElement.GetRawText()};
            using(var client=new RelicMarketClient(dir,handler,snapshotMode:true))
            {
                Yes((await client.QuoteAsync(nikana,MarketPresence.InGame,true,false,default)) is {Snapshot:true,SellMin:null,Error:not null}&&handler.Calls==0);
                await client.RefreshSnapshotItemAsync(nikana,true,default);
                Yes((await client.QuoteAsync(nikana,MarketPresence.InGame,true,false,default)) is {Snapshot:true,Cached:true,SellMin:20,Stale:false});
                Yes((await client.QuoteAsync(nikana,MarketPresence.Online,true,false,default)).SellMin==15&&handler.Calls==1);
                Yes(!await client.SnapshotDueAsync(nikana,true,default));
                Yes((await client.QuoteAsync(nikana,MarketPresence.InGame,false,false,default)).SellMin is null&&handler.Calls==1);
            }
            foreach(var file in Directory.GetFiles(dir,"*.json"))
            {
                var q=JsonSerializer.Deserialize<MarketQuote>(File.ReadAllText(file))!;
                File.WriteAllText(file,JsonSerializer.Serialize(q with{FetchedUtc=DateTimeOffset.UtcNow.AddHours(-13)}));
            }
            var offline=new Handler{Fail=true};using var restarted=new RelicMarketClient(dir,offline,snapshotMode:true);
            var stale=await restarted.QuoteAsync(nikana,MarketPresence.InGame,true,false,default);
            Yes(stale is {Snapshot:true,Cached:true,SellMin:20,Stale:true}&&offline.Calls==0);
            Yes(await restarted.SnapshotDueAsync(nikana,true,default));
            try{await restarted.RefreshSnapshotItemAsync(nikana,true,default);throw new Exception("실패 기대");}catch(HttpRequestException){}
            Yes((await restarted.QuoteAsync(nikana,MarketPresence.InGame,true,false,default)).FetchedUtc==stale.FetchedUtc);
        }
        check("성유물 자동 감지: 실제 2026-10-25 Lua 접두사·타이머 전환·선택 종료",()=>
        {
            var s=new RelicRewardSignals();
            string[] real=[
                "2151.453 Script [Info]: ProjectionRewardChoice.lua: Relic rewards initialized",
                "2152.002 Script [Info]: ProjectionsCountdown.lua: Relic timer closed",
                "2152.002 Script [Info]: ProjectionRewardChoice.lua: Got rewards",
                "2152.002 Script [Info]: ProjectionRewardChoice.lua: Missing icon data!",
                "2167.005 Script [Info]: ProjectionsCountdown.lua: Relic timer closed",
                "2167.005 Script [Info]: ProjectionRewardChoice.lua: Selection countdown done",
                "2167.005 Script [Info]: ProjectionRewardChoice.lua: Relic reward screen shut down"];
            Yes(real.Select((line,i)=>s.Accept(line,i*1000)).Where(x=>x!=RelicRewardSignal.None).SequenceEqual(new[]{RelicRewardSignal.Open,RelicRewardSignal.Close}));
            Yes(s.Accept(real[2],20_000)==RelicRewardSignal.None);
            Yes(RelicRewardSignals.Classify("1 Script [Info]: ProjectionsCountdown.lua: Relic timer closed")==RelicRewardSignal.None);
            Yes(RelicRewardSignals.Classify("1 Script [Info]: Chat.lua: Got rewards")==RelicRewardSignal.None);
        });
        check("성유물 자동 감지: 보상 진입·종료 신호와 채팅 구분",()=>
        {
            Yes(RelicRewardSignals.Classify("12.0 Script [Info]: Got rewards")==RelicRewardSignal.Open);
            Yes(RelicRewardSignals.Classify("12.0 Script [Info]: Pause countdown done")==RelicRewardSignal.Open);
            Yes(RelicRewardSignals.Classify("20.0 Script [Info]: ProjectionRewardChoice.lua: Relic reward screen shut down")==RelicRewardSignal.Close);
            Yes(RelicRewardSignals.Classify("20.0 Net [Info]: MatchingService::EndSession()")==RelicRewardSignal.Close);
            Yes(RelicRewardSignals.Classify("Chat [Info]: Got rewards")==RelicRewardSignal.None);
            Yes(RelicRewardSignals.Classify("Script [Info]: Player says Got rewards")==RelicRewardSignal.None);
        });
        check("성유물 자동 감지: 중복 로그·한 보상 화면·다음 라운드",()=>
        {
            var s=new RelicRewardSignals();
            Yes(s.Accept("12 Script [Info]: Got rewards",100)==RelicRewardSignal.Open);
            Yes(s.Accept("13 Script [Info]: Pause countdown done",200)==RelicRewardSignal.None);
            Yes(s.Accept("12 Script [Info]: Got rewards",400)==RelicRewardSignal.None);
            Yes(s.Accept("20 Script [Info]: ProjectionRewardChoice.lua: Relic reward screen shut down",800)==RelicRewardSignal.Close);
            Yes(s.Accept("12 Script [Info]: Got rewards",900)==RelicRewardSignal.None);
            Yes(s.Accept("40 Script [Info]: Got rewards",1000)==RelicRewardSignal.Open);
            Yes(s.Accept("80 Script [Info]: Got rewards",32000)==RelicRewardSignal.Open);
        });
        check("성유물: 한국어 품목·고유 ID·세트 제외",()=>{Yes(catalog.Items.Length>=590);Yes(catalog.Items.Select(x=>x.Id).Distinct().Count()==catalog.Items.Length);Yes(catalog.Items.All(x=>!x.Slug.EndsWith("_set")));Yes(catalog.Items.All(x=>x.Name.Any(c=>c>127)));});
        check("성유물: 띄어쓰기·영문 정확 검색",()=>{Yes(catalog.Search(" 니카나프라임 블레이드 ")[0] is {Similarity:1,Item.Id:var id}&&id==nikana.Id);Yes(catalog.Search("Nikana Prime Blade")[0].Item.Id==nikana.Id);});
        check("성유물: 오탈자 후보·자동 확정 금지",()=>{var p=catalog.Recognize([new("니카나 프라임 블래이드",.2,.4,.2,.03,98)]);Yes(p.Length==1&&p[0].Candidates[0].Item.Id==nikana.Id&&!p[0].Confident);});
        check("성유물: 인식 세션 캐시가 후보·점수·확정·순서를 바꾸지 않음",()=>
        {
            var session=new RelicCatalog.MatchSession();
            foreach(var item in catalog.Items.Where((_,i)=>i%7==0))
            {
                RewardText[] lines=[new(item.Name,.1,.4,.2,.03,90),new(item.Name.Replace("프라임","프 라 임"),.65,.4,.2,.03,45)];
                var expected=JsonSerializer.Serialize(catalog.Recognize(lines));
                Yes(JsonSerializer.Serialize(catalog.Recognize(lines,session))==expected);
                Yes(JsonSerializer.Serialize(catalog.Recognize(lines,session))==expected);
            }
            var other=catalog with{Items=[nikana]};RewardText[] checkLines=[new(nikana.Name,.1,.4,.2,.03,90)];
            Yes(JsonSerializer.Serialize(other.Recognize(checkLines,session))==JsonSerializer.Serialize(other.Recognize(checkLines)));
        });
        check("성유물: OCR 낮은 신뢰도는 정확한 이름도 재확인",()=>Yes(!catalog.Recognize([new(nikana.Name,.2,.4,.2,.03,20)])[0].Confident));
        check("성유물: 4개 보상·중복 품목을 위치별 유지",()=>{var p=catalog.Recognize(Enumerable.Range(0,4).Select(i=>new RewardText(nikana.Name,.06+i*.24,.4,.18,.025,96)).ToArray());Yes(p.Length==4&&p.All(x=>x.Item?.Id==nikana.Id));});
        check("성유물: 줄바꿈 합치기·위쪽 HUD 제외",()=>{var p=catalog.Recognize([new("니카나 프라임",.3,.4,.13,.02,94),new("블레이드",.34,.43,.05,.02,94),new("123 크레딧",.8,.1,.1,.03,90)]);Yes(p.Length==1&&p[0].Item?.Id==nikana.Id);});
        check("성유물: 빈 문자열·노이즈는 품목을 만들지 않음",()=>{Yes(catalog.Search("").Length==0);Yes(catalog.Recognize([new("플레이어123 체력",.1,.1,.1,.03,90)]).Length==0);});
        check("성유물: 아이콘 Vo·iS를 레퀴엠으로 인식하지 않음",()=>Yes(catalog.Recognize([new("Vo",.1,.4,.03,.03,90),new("iS",.4,.4,.03,.03,90)]).Length==0));
        check("성유물: 완전한 레퀴엠 이름은 인식 유지",()=>Yes(catalog.Recognize([new("Vome",.1,.4,.1,.03,96)]).Single().Item?.Slug=="vome"));
        check("성유물: 한글 사이 영문 잡음·수량 접두사 정리, 영문 품목 유지",()=>
        {
            Yes(RelicCatalog.NormalizeOcr("2 X 포 르마 설계도")=="포르마설계도");
            Yes(RelicCatalog.NormalizeOcr("가라 프라임 hi 뉴로옵틱스 설계도 ㅣ")=="가라프라임뉴로옵틱스설계도");
            Yes(RelicCatalog.NormalizeOcr("Vome")=="VOME");
        });
        check("성유물: 두 처리 계열이 일치하는 경미한 오탈자는 자동 확정",()=>
        {
            var reward=catalog.Recognize([new("니카나 프라임 블래이드",.2,.4,.2,.03,55)]).Single();
            Yes(!reward.Confident);
            Yes(RelicCatalog.ConfirmCorroborated([reward],[new("original",reward),new("contrast",reward)]).Single().Item?.Id==nikana.Id);
            Yes(!RelicCatalog.ConfirmCorroborated([reward],[new("contrast",reward),new("contrast",reward)]).Single().Confident);
        });
        check("성유물: 다른 처리 결과에 강한 경쟁 후보가 있으면 자동 확정 보류",()=>
        {
            var reward=catalog.Recognize([new("니카나 프라임 블래이드",.2,.4,.2,.03,55)]).Single();
            var competing=reward with{Candidates=[new(catalog.Items.Single(x=>x.Slug=="tatsu_prime_blade"),.98)]};
            Yes(!RelicCatalog.ConfirmCorroborated([reward],[new("original",reward),new("contrast",reward),new("tint",competing),new("inverted",competing)]).Single().Confident);
        });
        check("성유물: 분리된 설계도 조각보다 독립 판독으로 확인한 섀시 전체 이름 우선",()=>
        {
            RewardText[] fragments=[new("스티아낙스 프라임",.4,.4,.17,.02,98),new("설계도",.45,.43,.07,.02,98),
                new("스티아낙스 프라임 섀시",.4,.4,.22,.02,84),new("설계도",.46,.43,.07,.02,59)];
            var full=catalog.Recognize([new("스티아낙스 프라임 섀시 설계도",.4,.4,.22,.05,59)]).Single();
            RewardEvidence[] evidence=[new("bright",full),new("gold",full)];
            bool Confirmed(RecognizedReward r)=>evidence.Where(e=>e.Reward.Candidates[0].Item.Id==r.Candidates[0].Item.Id&&
                Math.Abs(e.Reward.Bounds.X-r.Bounds.X)<.06).Select(e=>e.Family).Distinct().Count()>=2;
            var chosen=catalog.Recognize(fragments,corroborated:Confirmed);
            Yes(RelicCatalog.ConfirmCorroborated(chosen,evidence).Single().Item?.Slug=="styanax_prime_chassis_blueprint");
            var unverified=catalog.Recognize([new("스티아낙스 프라임 섀시 설계도",.4,.4,.22,.05,20)],corroborated:_=>true);
            Yes(unverified.All(r=>!r.Confident)); // Ranking evidence never bypasses confidence checks.
        });
        check("성유물: 공통 부품명만으로 프레임을 확정하지 않음",()=>Yes(catalog.Recognize([new("뉴로옵틱스 설계도",.1,.4,.18,.03,99)]).All(x=>!x.Confident&&x.Candidates[0].Similarity<.95)));
        check("성유물: 프라임 글자가 누락되어도 고유 이름·부품이 뚜렷하면 복구",()=>
        {
            var reward=catalog.Recognize([new("라보스 뉴로옵틱스 설개도",.2,.4,.2,.04,83)]).Single();
            Yes(RelicCatalog.ConfirmCorroborated([reward],[]).Single().Item?.Slug=="lavos_prime_neuroptics_blueprint");
            var unread=catalog.Recognize([new("라보스 WREUA 설계도",.2,.4,.2,.04,83)]);
            Yes(RelicCatalog.ConfirmCorroborated(unread,[]).All(x=>!x.Confident));
            Yes(RelicCatalog.ConfirmCorroborated([reward with{Bounds=reward.Bounds with{Confidence=20}}],[]).All(x=>!x.Confident));
        });
        check("성유물: 여러 OCR 패스의 중복·아이콘보다 실제 4개 이름 우선",()=>
        {
            var names=new[]{"daikyu_prime_lower_limb","paris_prime_upper_limb","gara_prime_neuroptics_blueprint"}.Select(slug=>catalog.Items.Single(x=>x.Slug==slug)).Append(catalog.Items.Single(x=>x.Id=="local:forma-blueprint")).ToArray();
            var lines=Enumerable.Range(0,4).Select(i=>new RewardText(names[i].Name,.06+i*.24,.4,.18,.025,96)).ToArray();
            var p=catalog.Recognize(lines.Concat(lines.Select(x=>x with{X=x.X+.004,Confidence=70})).Append(new("Vo",.54,.4,.03,.03,90)).ToArray());
            Yes(p.Select(x=>x.Item?.Id).SequenceEqual(names.Select(x=>x.Id)));
        });
        check("성유물: 읽지 못한 가운데 보상의 순서 보존",()=>{var p=catalog.Recognize([new(nikana.Name,.1,.4,.18,.025,96),new("읽지못한보상",.4,.4,.18,.025,10),new(nikana.Name,.7,.4,.18,.025,96)]);Yes(p.Length==3&&p[0].Item?.Id==nikana.Id&&p[1].Item is null&&p[1].RawText=="읽지못한보상"&&p[2].Item?.Id==nikana.Id);});
        check("성유물: 포르마는 거래·듀캇 없음",()=>Yes(catalog.Items.Single(x=>x.Id=="local:forma-blueprint") is {Tradable:false,Ducats:0}));
        check("성유물: 레퀴엠 신품 충전만 비교",()=>{using var j=Orders(nikana.Id,[new("a","sell",12,"ingame",Charges:1),new("b","sell",30,"ingame",Charges:3)]);var q=RelicMarketMath.Summarize(j.RootElement,nikana.Id,DateTimeOffset.UtcNow,MarketPresence.InGame,true,3);Yes(q.SellMin==30&&q.SellOrders==1);});
        check("성유물: 레퀴엠 API 랭크 3 필터",()=>{var lohk=catalog.Items.Single(x=>x.Slug=="lohk");Yes(lohk.MarketRank==3);using var j=Orders(lohk.Id,[new("a","sell",2,"ingame",Rank:0),new("b","sell",30,"ingame",Rank:3),new("c","sell",1,"ingame")]);var q=RelicMarketMath.Summarize(j.RootElement,lohk.Id,DateTimeOffset.UtcNow,MarketPresence.InGame,true,rank:lohk.MarketRank);Yes(q.SellMin==30&&q.SellOrders==1);});
        var rows=new Row[]{new("a","sell",20,"ingame"),new("a","sell",30,"ingame"),new("b","sell",25,"ingame"),new("c","sell",1,"offline"),new("d","sell",10,"online"),new("e","sell",2,"ingame",Visible:false),new("f","buy",17,"ingame"),new("g","buy",18,"online"),new("h","sell",3,"ingame",Platform:"ps4",Crossplay:true),new("i","sell",1,"ingame",Platform:"switch",Crossplay:false),new("j","sell",1,"ingame",Quantity:0)};
        check("마켓: 게임 중·PC·중복 판매자·비공개·재고 필터",()=>{using var j=Orders(nikana.Id,rows);var q=RelicMarketMath.Summarize(j.RootElement,nikana.Id,DateTimeOffset.UtcNow,MarketPresence.InGame,false);Yes(q.SellMin==20&&q.LowFiveMedian==22.5m&&q.BuyMax==17&&q.SellOrders==2);});
        check("마켓: 온라인·크로스플레이 조건 구분",()=>{using var j=Orders(nikana.Id,rows);var q=RelicMarketMath.Summarize(j.RootElement,nikana.Id,DateTimeOffset.UtcNow,MarketPresence.Online,true);Yes(q.SellMin==3&&q.LowFiveMedian==15&&q.BuyMax==18&&q.SellOrders==4);});
        check("마켓: 묶음 호가는 개당 환산",()=>{using var j=Orders(nikana.Id,[new("a","sell",12,"ingame",PerTrade:6)]);Yes(RelicMarketMath.Summarize(j.RootElement,nikana.Id,DateTimeOffset.UtcNow,MarketPresence.InGame,true).SellMin==2);});
        check("마켓: 주문 없음은 0 플래티넘이 아님",()=>{using var j=Orders(nikana.Id,[]);var q=RelicMarketMath.Summarize(j.RootElement,nikana.Id,DateTimeOffset.UtcNow,MarketPresence.InGame,true);Yes(q.SellMin is null&&q.BuyMax is null&&q.LowFiveMedian is null);});
        check("마켓: 다른 품목 ID 가격 혼합 차단",()=>{using var j=Orders("other",rows);Yes(RelicMarketMath.Summarize(j.RootElement,nikana.Id,DateTimeOffset.UtcNow,MarketPresence.Online,true).SellOrders==0);});
        check("마켓: 익명 헤더·캐시·필터 분리·오프라인·취소",()=>Network().GetAwaiter().GetResult());
        check("마켓: 동시 중복 조회를 한 요청으로 합침",()=>Concurrent().GetAwaiter().GetResult());
        check("마켓: 느린 응답은 병렬 대기·요청 시작은 400ms 간격",()=>ParallelRequests().GetAwaiter().GetResult());
        check("마켓: 5초 연결 기한·오래된 캐시 명시",()=>Deadline().GetAwaiter().GetResult());
        check("마켓: 잘못된 응답은 저장 가격과 내장 목록으로 복구",()=>InvalidResponse().GetAwaiter().GetResult());
        check("마켓: HTTP 429 제한·재시도 폭주 방지",()=>RateLimit().GetAwaiter().GetResult());
        async Task Concurrent()
        {
            using var data=Orders(nikana.Id,rows);var handler=new Handler{Body=data.RootElement.GetRawText(),Delay=100};using var client=new RelicMarketClient(Path.Combine("artifacts","relic-core-cache-"+Guid.NewGuid().ToString("N")),handler);
            var quotes=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>client.QuoteAsync(nikana,MarketPresence.InGame,false,true,default)));Yes(handler.Calls==1&&quotes.All(x=>x.SellMin==20));
        }
        async Task ParallelRequests()
        {
            var handler=new Handler{Body="{\"data\":[]}",Delay=1100};using var client=new RelicMarketClient(Path.Combine("artifacts","relic-core-cache-"+Guid.NewGuid().ToString("N")),handler);
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var quotes=await Task.WhenAll(catalog.Items.Where(x=>x.Tradable).Take(4).Select(x=>client.QuoteAsync(x,MarketPresence.InGame,true,true,default)));
            Yes(quotes.All(q=>q.Error is null)&&handler.Starts.Count==4&&timer.Elapsed.TotalSeconds<3.8);
            var starts=handler.Starts.ToArray();Yes(starts.Zip(starts.Skip(1)).All(p=>(p.Second-p.First).TotalMilliseconds>=370));
        }
        async Task Deadline()
        {
            using var data=Orders(nikana.Id,rows);var handler=new Handler{Body=data.RootElement.GetRawText()};using var client=new RelicMarketClient(Path.Combine("artifacts","relic-core-cache-"+Guid.NewGuid().ToString("N")),handler);
            var saved=await client.QuoteAsync(nikana,MarketPresence.InGame,true,true,default);handler.Delay=10000;
            var timer=System.Diagnostics.Stopwatch.StartNew();var q=await client.QuoteAsync(nikana,MarketPresence.InGame,true,true,default);
            Yes(q.Stale&&q.Error is not null&&q.SellMin==saved.SellMin&&timer.Elapsed.TotalSeconds<6.5);
        }
        async Task InvalidResponse()
        {
            using var data=Orders(nikana.Id,rows);var handler=new Handler{Body=data.RootElement.GetRawText()};using var client=new RelicMarketClient(Path.Combine("artifacts","relic-core-cache-"+Guid.NewGuid().ToString("N")),handler);
            var first=await client.QuoteAsync(nikana,MarketPresence.InGame,false,true,default);handler.Body="{\"data\":null}";
            var fallback=await client.QuoteAsync(nikana,MarketPresence.InGame,false,true,default);Yes(fallback.Stale&&fallback.SellMin==20&&fallback.FetchedUtc==first.FetchedUtc&&fallback.Error is not null);
            var items=await client.CatalogAsync(true,default);Yes(items.Catalog.Items.Length==catalog.Items.Length&&items.Notice is not null);
        }
        async Task RateLimit()
        {
            var handler=new Handler{Status=HttpStatusCode.TooManyRequests};using var client=new RelicMarketClient(Path.Combine("artifacts","relic-core-cache-"+Guid.NewGuid().ToString("N")),handler);
            var quote=await client.QuoteAsync(nikana,MarketPresence.InGame,false,true,default);Yes(quote.SellMin is null&&quote.Error is not null&&handler.Calls==1);
            using var cancel=new CancellationTokenSource(40);try{await client.QuoteAsync(nikana,MarketPresence.InGame,false,true,cancel.Token);throw new Exception("제한 중 취소 무시");}catch(OperationCanceledException){}Yes(handler.Calls==1);
            await Task.Delay(1050);using var data=Orders(nikana.Id,rows);handler.Status=HttpStatusCode.OK;handler.Body=data.RootElement.GetRawText();
            Yes((await client.QuoteAsync(nikana,MarketPresence.InGame,false,true,default)).SellMin==20&&handler.Calls==2);
        }
        async Task Network()
        {
            var folder=Path.Combine("artifacts","relic-core-cache-"+Guid.NewGuid().ToString("N"));var handler=new Handler();using var client=new RelicMarketClient(folder,handler);
            using var data=Orders(nikana.Id,rows);handler.Body=data.RootElement.GetRawText();
            var first=await client.QuoteAsync(nikana,MarketPresence.InGame,false,false,default);Yes(first.SellMin==20&&!first.Cached);
            var second=await client.QuoteAsync(nikana,MarketPresence.InGame,false,false,default);Yes(second.Cached&&handler.Calls==1);
            await client.QuoteAsync(nikana,MarketPresence.Online,false,false,default);Yes(handler.Calls==2);
            handler.Fail=true;var stale=await client.QuoteAsync(nikana,MarketPresence.InGame,false,true,default);Yes(stale.Stale&&stale.Error is not null&&stale.FetchedUtc==first.FetchedUtc);
            var empty=await client.QuoteAsync(nikana,MarketPresence.InGame,true,false,default);Yes(empty.SellMin is null&&!empty.Cached&&empty.Error is not null);
            using var cts=new CancellationTokenSource();cts.Cancel();try{await client.QuoteAsync(nikana,MarketPresence.InGame,true,true,cts.Token);throw new Exception("취소 무시");}catch(OperationCanceledException){}
            using var restart=new RelicMarketClient(folder,new Handler{Fail=true});var disk=await restart.QuoteAsync(nikana,MarketPresence.InGame,false,false,default);Yes(disk.Cached&&disk.FetchedUtc==first.FetchedUtc);
        }
    }
    internal sealed record Row(string User,string Type,decimal Price,string Status,string Platform="pc",bool Crossplay=true,bool Visible=true,int Quantity=1,int PerTrade=1,int? Charges=null,int? Rank=null);
    internal static JsonDocument Orders(string id,Row[] rows)=>JsonDocument.Parse(JsonSerializer.Serialize(new{data=rows.Select(x=>new{itemId=id,type=x.Type,platinum=x.Price,quantity=x.Quantity,perTrade=x.PerTrade,visible=x.Visible,charges=x.Charges,rank=x.Rank,user=new{id=x.User,status=x.Status,platform=x.Platform,crossplay=x.Crossplay}})}));
    private sealed class Handler:HttpMessageHandler
    {
        internal int Calls;internal int Delay;internal string Body="";internal bool Fail;internal HttpStatusCode Status=HttpStatusCode.OK;
        internal readonly System.Collections.Concurrent.ConcurrentQueue<DateTimeOffset> Starts=new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Interlocked.Increment(ref Calls);Starts.Enqueue(DateTimeOffset.UtcNow);if(!request.Headers.UserAgent.ToString().Contains("WF-Labs")||!request.Headers.Contains("Language")||request.Headers.Authorization is not null)throw new Exception("마켓 헤더 오류");
            if(Delay>0)await Task.Delay(Delay,token);if(Fail)throw new HttpRequestException("오프라인 검사");var response=new HttpResponseMessage(Status){Content=new StringContent(Body,Encoding.UTF8,"application/json")};if(Status==HttpStatusCode.TooManyRequests)response.Headers.RetryAfter=new(TimeSpan.FromSeconds(1));return response;
        }
    }
}
