using TennoForge.Core;

internal static class RivenScanChecks
{
    internal static void Run(Action<string,Action> check)
    {
        void Yes(bool value){if(!value)throw new Exception("리벤 인식 분석 결과 불일치");}
        check("리벤 빠른 변경 감지 · 두 프레임 확인·순간 변화 무시·반복 OCR 방지",()=>
        {
            var watch=new RivenWatchState(0,600);var old=Enumerable.Repeat((byte)1,100).ToArray();var next=new byte[100];next[0]=1;
            watch.Prime(old,0);Yes(watch.Observe(true,next,400,true)==RivenWatchAction.None);Yes(watch.Observe(true,old,800,true)==RivenWatchAction.None);
            Yes(watch.Observe(true,next,1200,true)==RivenWatchAction.None);Yes(watch.Observe(true,next,1600,true)==RivenWatchAction.Read);
            watch.Prime(next,2000);for(int n=1;n<=100;n++)Yes(watch.Observe(true,next,2000+400*n,true)==RivenWatchAction.None);
            Yes(watch.Observe(false,[],42400,true)==RivenWatchAction.None);Yes(watch.Observe(false,[],42800,true)==RivenWatchAction.Clear);
        });
        check("리벤 전환 · 표시 창 유지·새 카드 재판독·목록 이탈",()=>
        {
            var watch=new RivenWatchState();var old=Enumerable.Repeat((byte)1,100).ToArray();var next=new byte[100];next[0]=1;
            watch.Prime(old,0);Yes(watch.Observe(true,next,750,true)==RivenWatchAction.None);
            var animated=next.ToArray();animated[1]=1;Yes(watch.Observe(true,animated,1500,true)==RivenWatchAction.None);Yes(watch.Observe(true,animated,2250,true)==RivenWatchAction.Read);
            watch.Prime(animated,4000);Yes(watch.Observe(false,[],4750,true)==RivenWatchAction.None);Yes(watch.Observe(false,[],5500,true)==RivenWatchAction.Clear);
            Yes(watch.Observe(true,old,6250,true)==RivenWatchAction.None&&watch.Observe(true,old,7000,true)==RivenWatchAction.Read);
        });
        check("리벤 카드 미세 이동 · ±3px 정렬·실제 글자 변경 구분",()=>
        {
            const int width=640;var a=new byte[width*100];
            for(int y=20;y<70;y++)for(int x=240;x<360;x++)if((x%9<4)&&(y%11<7))a[y*width+x]=1;
            var watch=new RivenWatchState(width);watch.Prime(a,0);
            for(int shift=-3;shift<=3;shift++)
            {
                var b=new byte[a.Length];for(int y=3;y<97;y++)for(int x=3;x<637;x++)b[(y+shift)*width+x+shift]=a[y*width+x];
                Yes(RivenWatchState.AlignedDifference(a,b,width)==0&&watch.Observe(true,b,4000+(shift+3)*750,true)==RivenWatchAction.None);
            }
            var changed=a.ToArray();for(int y=20;y<40;y++)for(int x=240;x<270;x++)changed[y*width+x]=0;
            Yes(RivenWatchState.AlignedDifference(a,changed,width)>.012);
            var action=watch.Observe(true,changed,12000,true);if(action!=RivenWatchAction.Read)action=watch.Observe(true,changed,12750,true);Yes(action==RivenWatchAction.Read);
        });
        check("리벤 태양과 달: 두 줄 게임 표기·제목 손상과 무관한 근접 자동 판별",()=>
        {
            var r=RivenKindDetection.Resolve("+56% 추가 연속기 카운트\n확률\n+1.8 사거리\n+84.1% 독성",8,"도즈74726토토새");
            Yes(r.Valid&&r.Kind=="Melee"&&r.Options.Select(o=>o.Key).SequenceEqual(new[]{"ComboChance","Range","Toxin"}));
            Yes(RivenKindDetection.WeaponKind("태양과\n달 Laci-toxitor")=="Melee");
        });
        check("리벤 제목: 한 글자 콤·접미사 OCR 기호·연속기 오인 금지",()=>
        {
            Yes(RivenKindDetection.WeaponKind("콤 Parci-decitio")=="Shotgun");
            Yes(RivenKindDetection.WeaponKind("콤보 지속 시간") is null);
            var r=RivenKindDetection.Resolve("+145.7% 독성\n+179.1% 멀티샷\n-64.6% 충격",8,"스트런 ㅣ0%×|031");
            Yes(r.Valid&&r.Kind=="Shotgun"&&r.Options[2].Negative);
            var k=RivenKindDetection.Resolve("+103.9% 상태 이상 지속\n시간\n+92.7% 전기\n+51.8% 탄약 효율\n-43.4% 탄창 용량",8,"콤 Parci-decitio");
            Yes(k.Valid&&k.Kind=="Shotgun"&&k.Options.Length==4);
        });
        check("리벤 타츠: 단위 없는 사거리와 양수 팩션 배율",()=>
        {
            var r=RivenKindDetection.Resolve("+1.8 사거리\n+81.9% 냉기\n인페스티드에 대한 피해\nx1.4",8,"타츠 Locti-geliada");
            Yes(r.Valid&&r.Kind=="Melee"&&r.Options[2].Value==40&&!r.Options[2].Negative&&r.Options[2].Decimals==0);
            Yes(!RivenKindDetection.Resolve("+1.8 사거리\n+81.9% 냉기\n인페스티드에 대한 피해\nxl.4",8).Valid);
        });
        check("리벤 행 장식: 정확한 후행 명칭만 분리하며 수치 수선 금지",()=>
        {
            Yes(RivenScan.NormalizeOcrLabels("1스+1.8사거리aae","Melee")=="+1.8 사거리");
            Yes(RivenScan.NormalizeOcrLabels("+92.79% 전기","Melee").StartsWith("+92.79%"));
            Yes(RivenScan.NormalizeOcrLabels("+81.9% 앵기","Melee")=="+81.9% 앵기");
            Yes(!RivenScan.Parse(RivenScan.NormalizeOcrLabels("+1.8 알수없는사거리","Melee"),"Melee",8).Options.Any());
        });
        check("리벤 자동 종류: 멀티샷·치확·양수 반동은 소총으로 판별",()=>
        {
            var result=RivenKindDetection.Resolve("+121% 멀티샷\n+208.4% 치명타 확률\n+53.3% 반동",8);
            Yes(result.Valid&&result.Kind=="Rifle"&&result.DetectedKind&&result.KindCandidates.SequenceEqual(new[]{"Rifle"}));
            Yes(result.Options[2].Key=="Recoil"&&result.Options[2].Negative);
        });
        check("리벤 자동 종류: 이름 없는 공통 원소 수치는 억지로 확정하지 않음",()=>
        {
            var result=RivenKindDetection.Resolve("+106.9% 화염\n+106.9% 냉기",8);
            Yes(!result.Valid&&result.KindCandidates.Length>1&&result.Values.All(v=>v.Grade is null)&&result.Recipes.Length==0);
            foreach(var (title,kind) in new[]{("울프 슬레지 Vexi-toxicron","Melee"),("브래튼 Visicron","Rifle"),("스트런 Critatis","Shotgun"),("라토 Lexiata","Pistol")})
            {
                var named=RivenKindDetection.Resolve("+106.9% 화염\n+106.9% 냉기",8,title);
                Yes(named.Kind==kind&&named.KindCandidates.SequenceEqual(new[]{kind})&&named.KindBasis=="무기 이름");
            }
        });
        check("리벤 자동 종류: 이름·수치 충돌과 잘못된 숫자·단위는 복구로 위장하지 않음",()=>
        {
            const string rifle="+121% 멀티샷\n+208.4% 치명타 확률\n+53.3% 반동";
            var conflict=RivenKindDetection.Resolve(rifle,8,"울프 슬레지 Vexi-toxicron");
            Yes(!conflict.Valid&&conflict.Kind=="Melee");
            Yes(!RivenKindDetection.Resolve(rifle.Replace("+121%","121%"),8).Valid);
            Yes(!RivenKindDetection.Resolve(rifle.Replace("+121%","+121m"),8).Valid);
            Yes(RivenKindDetection.WeaponKind("+106.9% 화염\n게임 플레이어 라토") is null);
        });
        check("리벤 자동 종류: 울프 슬레지의 강공 치확 주석·세 양옵",()=>
        {
            var result=RivenKindDetection.Resolve("+88.4% 전기\n+79.6% 독성\n+155.9% 치명타 확률 (강\n공격 시 2배)",8,"울프 슬레지 Vexi-toxicron");
            Yes(result.Valid&&result.Kind=="Melee"&&result.Options.Length==3&&result.Options[1].Key=="Toxin"&&result.Options[2].Value==155.9);
        });
        foreach(var kind in new[]{"Melee","Rifle","Shotgun","Pistol"})check($"리벤 {kind} 전체 옵션: 명칭·양음수·단위 입력 대응",()=>
        {
            foreach(var key in RivenScan.Keys(kind))foreach(var sign in new[]{"+","-"})
            {
                var label=RivenScan.Label(kind,key);var report=RivenScan.Parse($"{sign}1.2{RivenScan.Unit(kind,key)} {label}",kind,8);
                Yes(report.Options.Length==1&&report.Options[0].Key==key&&report.Options[0].Value==1.2);
                Yes(report.Options[0].Negative==(sign=="-"));
            }
        });
        check("리벤 한국어: 공식 모드 표기의 연속기·탄약·관통·확대율 대응",()=>
        {
            var rows=new[]{("Melee","+1% 마무리 일격 피해","FinisherDamage"),("Melee","연속기 지속 시간 +2초","ComboDuration"),("Melee","+1 기본 연속기","InitialCombo"),("Melee","+1% 연속기 카운트 확률","ComboChance"),("Rifle","+1% 탄약 최대량","AmmoMaximum"),("Rifle","+1% 발사체 속도","ProjectileSpeed"),("Rifle","+0.4 두께 꿰뚫기","PunchThrough"),("Rifle","+1% 확대율 증가","Zoom")};
            foreach(var (kind,text,key) in rows){var r=RivenScan.Parse(text,kind,8);Yes(r.Options.Length==1&&r.Options[0].Key==key);}
        });
        check("리벤 OCR 명칭 사전: 한 글자만·숫자 부호 보존·짧은 원소 추측 금지",()=>
        {
            var text="+141.8% 상대 이상 피해\n+140.7% 전기\n-76.1% 마무리 일격 피해 27";
            var r=RivenScan.Parse(RivenScan.NormalizeOcrLabels(text,"Melee"),"Melee",8);
            Yes(r.Valid&&r.Options.Select(o=>o.Value).SequenceEqual(new[]{141.8,140.7,76.1})&&r.Options[2].Negative);
            Yes(RivenScan.NormalizeOcrLabels("+100% 전가","Melee")=="+100% 전가");
            Yes(RivenScan.NormalizeOcrLabels("141.8% 상태 이상 피해","Melee")=="141.8% 상태 이상 피해");
            Yes(RivenScan.NormalizeOcrLabels("+141.89% 상대 이상 피해","Melee").StartsWith("+141.89%"));
            Yes(!RivenScan.Parse(RivenScan.NormalizeOcrLabels(text.Replace("+140.7%","+140.7"),"Melee"),"Melee",8).Valid);
        });
        check("리벤 히루도 · 단위 없는 정수 사거리·팩션 배율·표시 왕복",()=>
        {
            const string text="+101.1% 전기\n+63.9% 공격 속도\n+2 사거리\n코퍼스에 대한 피해 x0.59";
            var r=RivenScan.Parse(text,"Melee",8);
            Yes(r.Valid&&r.Options.Select(o=>o.Value).SequenceEqual(new[]{101.1,63.9,2,41d}));
            Yes(r.Options[2].Key=="Range"&&r.Options[2].Unit=="m"&&r.Options[2].Decimals==1&&r.Options[3].Negative);
            Yes(RivenScan.Parse(RivenScan.Editable(r),"Melee",8).Options.SequenceEqual(r.Options));
            Yes(!RivenScan.Parse(text.Replace("+2 사거리","+2% 사거리"),"Melee",8).Valid);
            Yes(!RivenScan.Parse(text.Replace("+63.9%","+63.9"),"Melee",8).Valid);
            Yes(RivenScan.Parse(text.Replace("+2 사거리","+2\n사거리"),"Melee",8).Valid);
        });
        check("리벤 인노뎀 · 두 잠금 행·3양1음·부분 판독은 수율 보류·수정 후 복구",()=>
        {
            const string text="🔒 +110.6% ⚡ 전기\n🔒 +110.1% 상태 이상 피해\n+76.9% 강 공격 효율\n-117.5% 베기";
            var complete=RivenScan.Parse(text,"Melee",8);
            Yes(complete.Valid&&complete.Options.Select(o=>o.Key).SequenceEqual(new[]{"Electricity","StatusDamage","HeavyEfficiency","SlashDamage"}));
            Yes(complete.Options.Select(o=>o.Value).SequenceEqual(new[]{110.6,110.1,76.9,117.5})&&complete.Options.Select(o=>o.Negative).SequenceEqual(new[]{false,false,false,true}));
            Yes(complete.Values.Length==4&&complete.Values.All(v=>v.Grade is not null)&&complete.Recipes.Length==0);
            var traits=RivenScan.Melee(complete).Traits;
            Yes(!traits[0].Spliced&&traits[1].Spliced); // Ordinary lock is not a second splice.
            foreach(var damaged in new[]{text.Replace("+110.6% ⚡ 전기","+110.070 전기"),text.Replace("⚡ 전기","미식별옵션")})
            {
                var partial=RivenScan.Parse(damaged,"Melee",8);
                Yes(!partial.Valid&&partial.Options.SequenceEqual(complete.Options.Skip(1))&&partial.Values.Length==0&&partial.Recipes.Length==0);
                Yes(partial.Issues.Any(i=>i.Contains("옵션 구성 확인 전"))&&!partial.Issues.Any(i=>i.Contains("범위 밖")));
                Yes(partial.Issues.Any(i=>i.Contains("+110.070 전기")||i.Contains("미식별옵션")));
                Yes(RivenScan.Opportunities(partial).Length==0);
                var detected=RivenKindDetection.Resolve(damaged,8,"인노뎀 Vexi-plagaus");
                Yes(detected.Kind=="Melee"&&!detected.Valid&&detected.Options.SequenceEqual(partial.Options)&&detected.Values.Length==0);
            }
            var restored=RivenScan.Parse(RivenScan.Editable(complete),"Melee",8);
            Yes(restored.Valid&&restored.Options.SequenceEqual(complete.Options)&&restored.Values.Length==4&&restored.Issues.Length==0);
            foreach(var title in new[]{"인노뎀 16%3-","인노뎀 +16%3-"})
            {
                var noisyTitle=RivenScan.Parse(title+"\n"+text,"Melee",8);
                Yes(noisyTitle.Valid&&noisyTitle.Options.SequenceEqual(complete.Options));
            }
            Yes(!RivenScan.Parse("+110.6% 인노뎀\n"+text,"Melee",8).Valid); // A signed stat is never discarded as a title.
            var wrappedNoise=RivenScan.NormalizeOcrLabels("+118.8% 상태 이상 지속 시간\n+108.1% 상태 이상 확률\n+106.4% 화염\n슬라이드 공격에 치명타\n흐확률 -100% 추가","Melee");
            var wrappedReport=RivenScan.Parse(wrappedNoise,"Melee",8);
            Yes(wrappedReport.Valid&&wrappedReport.Options[^1].Key=="SlideCriticalChance"&&wrappedReport.Options[^1].Value==100&&wrappedReport.Options[^1].Negative);
            // Callers also ask for a structurally known shape using placeholder
            // values. Those ranges remain available when parsing had no errors.
            var shape=RivenScan.Analyze(complete.Options.Select(o=>o with{Value=0}).ToArray(),"Melee",8);
            Yes(!shape.Valid&&shape.Values.Length==4);
        });
        check("리벤 시암 · 잠긴 접합·원소 아이콘·줄바꿈 팩션 배율·반올림 정밀도",()=>
        {
            var r=RivenScan.Parse("+110.3% 치명타 피해\n8+108.6% 상태 이상 피해\n+95.2% ⚡ 전기\n인페스티드에 대한 피해\nx0.58","Melee",8);
            Yes(r.Valid&&r.Options.Select(o=>o.Value).SequenceEqual(new[]{110.3,108.6,95.2,42d}));
            Yes(r.Options[1].Key=="StatusDamage"&&r.Options[3].Negative&&r.Options[3].Decimals==0&&r.Recipes.Length==0);
            var grade=r.Values[3].Grade!;var range=r.Values[3].Range;
            Yes(Math.Abs(grade.PositionLow-(41.5-range.Minimum)/(range.Maximum-range.Minimum))<1e-9);
            Yes(RivenScan.Parse(RivenScan.Editable(r),"Melee",8).Options.SequenceEqual(r.Options));
            Yes(!RivenScan.Parse("×1.1 치명타 피해\n+108.6% 상태 이상 피해","Melee",8).Valid);
        });
        foreach(var kind in new[]{"Melee","Rifle","Shotgun","Pistol"})check($"{kind} 팩션 접합 · 배율 두 자리의 반올림 구간 계승",()=>
        {
            var a=kind=="Melee"?"VsCorpus":"FactionCorpus";var b=kind=="Melee"?"VsGrineer":"FactionGrineer";
            var shape=RivenScan.Analyze([new(a,RivenScan.Label(kind,a),0,false,"%"),new(b,RivenScan.Label(kind,b),0,false,"%")],kind,8);
            var options=shape.Values.Select(v=>v.Option with{Value=Math.Round((v.Range.Minimum+v.Range.Maximum)/2),Decimals=0}).ToArray();var result=RivenScan.Analyze(options,kind,8);
            Yes(result.Valid&&result.Recipes.Length==1);var recipe=result.Recipes[0];
            Yes(recipe.Maximum-recipe.Minimum>.9&&RivenScan.Parse(RivenScan.Editable(result),kind,8).Options.SequenceEqual(options));
        });
        check("리벤 수치 등급 · 일곱 경계·음옵 반전·유효 범위",()=>
        {
            Yes(new[]{0d,.2,.4,.6,.8,.9,.97,1}.Select(RivenRating.Tier).SequenceEqual(new[]{"F","C","B","A","S","SS","SSS","SSS"}));
            Yes(RivenRating.Tier(.199999)=="F"&&RivenRating.Tier(.969999)=="SS");
            Yes(RivenRating.Tier(RivenRating.Quality(.13,true))=="S"&&RivenRating.Tier(RivenRating.Quality(1,true))=="F");
            try{RivenRating.Tier(double.NaN);throw new Exception("NaN 허용");}catch(ArgumentOutOfRangeException){}
        });
        check("리벤 접합 가능성 · 실제 인노뎀의 부족한 네 재료·조건부 등급",()=>
        {
            var report=RivenScan.Parse("+118.8% 상태 이상 지속 시간\n+108.1% 상태 이상 확률\n+106.4% 화염\n슬라이드 공격에 치명타 확률 -100% 추가","Melee",8);
            var future=RivenScan.Opportunities(report);
            Yes(report.Valid&&report.Recipes.Length==0&&future.Length==4);
            var damage=future.Single(o=>o.ResultKey=="StatusDamage");Yes(damage.HeldKey=="StatusChance"&&damage.MissingKey=="Damage"&&RivenRating.Tier(damage.MinimumQuality)=="S"&&damage.MaximumQuality==1);
            Yes(future.Where(o=>o.HeldKey=="Heat").Select(o=>o.ResultKey).Order().SequenceEqual(new[]{"Blast","Gas","Radiation"}));
            Yes(RivenScan.Opportunities(report with{Issues=["오인식"]}).Length==0);
        });
        check("리벤 즉시 접합 등급 · 기존 계승 범위와 동일·부족 재료와 구분",()=>
        {
            var report=RivenScan.Parse("+195.7% 근접 피해\n+106.9% 상태 이상 확률","Melee",8);var recipe=report.Recipes.Single();var prediction=RivenValues.Predict(RivenScan.Melee(report)).Single();
            Yes(Math.Abs(recipe.MinimumQuality-(prediction.InheritedRoll.Low-.9)/.2)<1e-9&&Math.Abs(recipe.MaximumQuality-(prediction.InheritedRoll.High-.9)/.2)<1e-9);
            Yes(!RivenScan.Opportunities(report).Any(o=>o.ResultKey=="StatusDamage"));
        });
        foreach(var kind in new[]{"Rifle","Shotgun","Pistol"})check($"{kind} 접합 가능성 · 종류별 레시피·부족한 원소",()=>
        {
            var seed=RivenScan.Parse("+1% 화염\n+1% 치명타 피해",kind,8);
            var report=RivenScan.Analyze(seed.Options.Select((o,i)=>o with{Value=Math.Round((seed.Values[i].Range.Minimum+seed.Values[i].Range.Maximum)/2,1)}).ToArray(),kind,8);
            Yes(report.Valid);var routes=RivenScan.Opportunities(report);Yes(routes.Length==3&&routes.All(o=>o.HeldKey=="Heat"&&o.MaximumQuality==1));
            Yes(routes.Select(o=>o.MissingKey).Order().SequenceEqual(new[]{"Cold","Electricity","Toxin"}));
        });
        check("리벤 실제 화면 · 줄바꿈·문장 뒤 음수·슬라이드 옵션",()=>
        {
            var r=RivenScan.Parse("+118.8% 상태 이상 지속\n시간\n+108.1% 상태 이상 확률\n+106.4% 화염\n슬라이드 공격에 치명타\n확률 -100% 추가","Melee",8);
            Yes(r.Valid&&r.Options.Select(o=>o.Value).SequenceEqual(new[]{118.8,108.1,106.4,100d})&&r.Options[3].Key=="SlideCriticalChance"&&r.Options[3].Negative);
        });
        check("리벤 자동 감지 · 진입·정지 화면·변경·이탈",()=>
        {
            var state=new RivenWatchState();var a=Enumerable.Repeat((byte)1,100).ToArray();var b=a.ToArray();b[0]=b[1]=b[2]=0;
            Yes(state.Observe(true,a,0)==RivenWatchAction.None);Yes(state.Observe(true,a,750)==RivenWatchAction.Read);
            for(int i=0;i<120;i++)Yes(state.Observe(true,a,1500+i*750)==RivenWatchAction.None);
            Yes(state.Observe(true,b,100000)==RivenWatchAction.Read);Yes(state.Observe(false,[],100750)==RivenWatchAction.None);Yes(state.Observe(false,[],101500)==RivenWatchAction.Clear);
            Yes(state.Observe(true,b,102250)==RivenWatchAction.None);Yes(state.Observe(true,b,103000)==RivenWatchAction.Read);
        });
        check("리벤 자동 감지 · OCR 재시도 제한·불안정 전환 억제",()=>
        {
            var state=new RivenWatchState();var a=Enumerable.Repeat((byte)1,100).ToArray();var b=new byte[100];
            for(int i=0;i<20;i++)Yes(state.Observe(true,i%2==0?a:b,i*750)==RivenWatchAction.None);
            state.Reset();state.Observe(true,a,0);Yes(state.Observe(true,a,750)==RivenWatchAction.Read);
            for(int i=0;i<2;i++){state.Retry();Yes(state.Observe(true,a,3000+i*3000)==RivenWatchAction.Read);}
            state.Retry();Yes(state.Observe(true,a,12000)==RivenWatchAction.None);state.Reset();Yes(state.Observe(true,a,14000)==RivenWatchAction.None);
        });
        check("리벤 자동 감지 · 배경 애니메이션 첫 판독·옵션 영역 추적",()=>
        {
            var state=new RivenWatchState();var a=Enumerable.Repeat((byte)1,100).ToArray();var animation=new byte[100];
            Yes(state.Observe(true,a,0,allowInitialRead:true)==RivenWatchAction.None);
            Yes(state.Observe(true,animation,750,allowInitialRead:true)==RivenWatchAction.Read);
            state.Prime(a,4000);
            for(int i=0;i<20;i++)Yes(state.Observe(true,a,5000+i*750,true)==RivenWatchAction.None);
            var changed=a.ToArray();changed[0]=changed[1]=changed[2]=0;
            Yes(state.Observe(true,changed,25000,true)==RivenWatchAction.Read);
            state.Prime(changed,28000);Yes(state.Observe(true,changed,30000,true)==RivenWatchAction.None);
            Yes(state.Observe(false,[],31000,true)==RivenWatchAction.None);Yes(state.Observe(false,[],32000,true)==RivenWatchAction.Clear);
        });
        check("리벤 OCR · 근접 기존 범위·접합 엔진 일치",()=>
        {
            var r=RivenScan.Parse("+195.7% 근접 피해\n+106.9% 상태 이상 확률","Melee",8);
            Yes(r.Valid&&r.Options.Length==2&&r.Recipes.Length==1&&r.Recipes[0].Result=="상태이상 피해");
            Yes(Math.Abs(r.Values[0].Range.Minimum-176.09724)<1e-6&&r.Values[0].Grade!.Position is >.49 and <.51);
            var expected=RivenValues.Predict(RivenScan.Melee(r))[0];Yes(r.Recipes[0].Minimum==expected.Value.Low&&r.Recipes[0].Maximum==expected.Value.High);
        });
        foreach(var kind in new[]{"Melee","Rifle","Shotgun","Pistol"})foreach(var rank in new[]{0,4,8})
        check($"리벤 OCR · {kind} {rank}랭크 반올림·접합·교정 왕복",()=>
        {
            var keys=new[]{"Damage","Heat","Cold","CriticalDamage"};var stub=keys.Select((key,i)=>new RivenScanOption(key,RivenScan.Label(kind,key),0,i==3,RivenScan.Unit(kind,key))).ToArray();
            var shape=RivenScan.Analyze(stub,kind,rank);Yes(shape.Values.Length==4);
            var actual=stub.Select((o,i)=>o with{Value=Math.Round((shape.Values[i].Range.Minimum+shape.Values[i].Range.Maximum)/2,1,MidpointRounding.AwayFromZero)}).ToArray();
            var result=RivenScan.Analyze(actual,kind,rank);Yes(result.Valid&&result.Recipes.Length==1);
            var parsed=RivenScan.Parse(RivenScan.Editable(result),kind,rank);Yes(parsed.Valid&&parsed.Options.SequenceEqual(result.Options));
            int count=kind=="Melee"?RivenWorkshop.Pool(false).Count(s=>s is not(RivenStat.Damage or RivenStat.CriticalDamage)):RangedRivenWorkshop.Pool(RivenScan.Ranged(result),false).Count(s=>s!="Damage"&&s!="CriticalDamage");Yes(result.Recipes[0].ReplacementOptions==count);
        });
        check("리벤 OCR · 누락 부호·미확인 옵션·단위·범위는 확정하지 않음",()=>
        {
            foreach(var extra in new[]{"99% 치명타 피해","99 치명타 피해","치명타 피해","+99% 알수없는옵션","+99m 치명타 피해","+99999% 치명타 피해","+20% 근접 피해"})
            {var r=RivenScan.Parse("+195.7% 근접 피해\n+106.9% 상태이상 확률\n"+extra,"Melee",8);Yes(!r.Valid&&r.Recipes.Length==0);}
        });
        check("리벤 OCR · 동일 옵션 중복과 잘못된 랭크 거부",()=>
        {Yes(!RivenScan.Parse("+195.7% 근접 피해\n+195.7% 근접 피해","Melee",8).Valid);Yes(!RivenScan.Parse("+195.7% 근접 피해\n+106.9% 상태이상 확률","Melee",9).Valid);});
        check("리벤 OCR · 반동 감소의 화면 부호는 음옵과 반대",()=>
        {
            var shape=RivenScan.Parse("+1% 피해\n-1% 반동","Rifle",8);var a=shape.Values[0].Range;var b=shape.Values[1].Range;
            var report=RivenScan.Parse($"+{Math.Round((a.Minimum+a.Maximum)/2,1):0.0}% 피해\n-{Math.Round((b.Minimum+b.Maximum)/2,1):0.0}% 반동","Rifle",8);
            Yes(report.Valid&&report.Options.All(o=>!o.Negative));Yes(RivenScan.Parse(RivenScan.Editable(report),"Rifle",8).Valid);
        });
    }
}
