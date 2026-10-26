"""Normalize pinned WFCD facts, Wiki acquisition revisions and vendor manifests.
Never execute wiki templates. Keep exact game IDs, revisions and source hashes.
"""
import pathlib,json,re,html,urllib.parse,hashlib,datetime,collections
root=pathlib.Path(__file__).resolve().parents[1];cache=root/'artifacts/acquisition-research'
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
catalog=load(root/'data/inventory-catalog.json');items={x['id']:x for x in catalog['items']};recipes={r['resultId']:r for r in catalog['recipes']};bpresult={r['blueprintId']:r['resultId'] for r in catalog['recipes']}
categories=['Primary','Secondary','Melee','Warframes','Archwing','Arch-Gun','Arch-Melee','Sentinels','SentinelWeapons','Pets','Components','Misc','Resources']
wf={x['uniqueName']:x for cat in categories for x in load(cache/(cat+'.json'))}
vendors=load(root/'artifacts/worldstate-research/ExportVendors.json');inventory_sources=load(root/'data/inventory-sources.json')['sources'];relic_rewards={s['id'] for r in catalog['relics'] for s in r['rewards']}
pages={};redirects={}
for p in [*cache.glob('wiki-batch-*.json'),*cache.glob('wiki-extra-*.json'),*cache.glob('wiki-wrap-*.json'),*cache.glob('wiki-more-*.json')]:
 d=load(p);pages.update({x['title']:x for x in d['query']['pages']});redirects.update({r['from']:r['to'] for r in d['query'].get('redirects',[])})
ko=load(root/'artifacts/worldstate-research/dict.ko.json');en=load(cache/'dict.en.json')
localized={}
for tag,value in en.items():
 if isinstance(value,str) and tag in ko and isinstance(ko[tag],str) and '<' not in value and len(value)<100:localized.setdefault(value,ko[tag])
localized.update({x['englishName']:x['name'] for x in items.values() if x['englishName']})
localized.update({'Market':'오비터 상점','Dojo':'클랜 도장','Bio Lab':'바이오 연구소','Tenno Lab':'텐노 연구소','Energy Lab':'에너지 연구소','Chem Lab':'화학 연구소','Orokin Lab':'오로킨 연구소','Ventkids’ Bash Lab':'벤트키드 바쉬 연구소','Nightwave':'나이트웨이브','Baro Ki\'Teer':'바로 키 티어','Cephalon Simaris':'세팔론 시마리스','The Circuit':'순환로','The Steel Path':'강철의 길','Jackal':'자칼','Demolisher Infested':'인페스티드 데몰리셔','Sanctuary Onslaught':'생츄어리 맹습','Elite Sanctuary Onslaught':'엘리트 생츄어리 맹습','Research':'클랜 연구','Founders':'파운더스','Conservation':'보존 활동','The New War':'새로운 전쟁','Junction':'교차점','Standing':'평판','Teshin\'s Cave':'테신의 동굴','Weapons':'무기','Railjack':'레일잭','The Duviri Paradox':'두비리의 역설'})
localized.update({'Orokin Void':'보이드','Zariman Ten Zero':'자리만 텐 제로','Archwing':'아크윙','Daily Tribute':'일일 접속 보상','Isolation Vaults':'격리 금고','Assassination':'암살','Bounties':'의뢰','Bounty':'의뢰','Disruption':'교란','Mirror Defense':'거울상 방어','Cephalon Melica':'세팔론 멜리카','Acrithis':'아크리티스','Koumei\'s Shrine':'코우메이 성소','Little Duck':'리틀 덕','Drifter\'s Camp':'드리프터 야영지'})
def loc(s):
 s=re.sub(r'\s+',' ',s).strip();s=s.split('#')[0];return localized.get(s,localized.get(s.replace('_',' '),s))
def template(text,name):
 m=re.search(r'\{\{'+re.escape(name)+r'\s*\|',text,re.I)
 if not m:return ''
 start=m.end();i=start;depth=1
 while i<len(text)-1:
  if text[i:i+2]=='{{':depth+=1;i+=2
  elif text[i:i+2]=='}}':
   depth-=1
   if not depth:return text[start:i]
   i+=2
  else:i+=1
 return ''
def acquisition(text):
 t=template(text,'Acquisition')
 if not t:
  m=re.search(r'^(={2,4})\s*(?:Acquisition|Obtaining)\s*\1\s*$',text,re.M|re.I)
  if m:t=re.split(r'^={2,'+str(len(m[1]))+r'}[^=]',text[m.end():],maxsplit=1,flags=re.M)[0]
 if not t and '{{Acquisition}}' in text:t=text.split('{{Acquisition}}',1)[1].split('\n==',1)[0]
 t=re.split(r'^={2,4}\s*(?:History|Historical|Vaultings|Crafting|Manufacturing|Release|Notes)|\{\{(?:BuildAutomatic|StanceTable|Navbox)',t,flags=re.M|re.I)[0]
 return re.sub(r'<!--.*?-->','',t,flags=re.S).strip()
def page_for(meta):
 if not meta.get('wikiaUrl') and meta.get('name') in ['Blueprint','Barrel','Receiver','Stock','Chassis','Systems','Neuroptics','String','Grip','Upper Limb','Lower Limb','Blade','Blades','Handle','Hilt','Hook','Gauntlet','Core','Engine','Link']:
  owner=next((wf.get(id) for id in meta.get('parentUniqueNames',[]) if wf.get(id,{}).get('wikiaUrl')),None)
  if owner:meta=owner
 title=urllib.parse.unquote(meta.get('wikiaUrl','').split('/w/')[-1]).replace('_',' ')
 if not title and meta.get('name') not in ['Blueprint','Barrel','Receiver','Stock','Chassis','Systems','Neuroptics','String','Grip','Upper Limb','Lower Limb']:title=meta.get('name','')
 title=redirects.get(title,title);p=pages.get(title,{})
 t=p.get('revisions',[{}])[0].get('slots',{}).get('main',{}).get('content','')
 if re.search(r'\{\{(?:Warframe|Archwing|Companion|Necramech)Page',t):title+='/Main';p=pages.get(title,p)
 rev=p.get('revisions',[{}])[0];text=rev.get('slots',{}).get('main',{}).get('content','');acq=acquisition(text)
 if not acq:
  acq='\n\n'.join(x for x in re.split(r'\n\s*\n',text) if len(x)<1400 and '{|' not in x and 'data-rowid' not in x and re.search(r'(?:reusable blueprint|blueprint can be|blueprints can be|blueprint is obtained)',x,re.I))
 return title,rev,acq,text
def clean(t):
 t=re.sub(r'<ref\b[^>]*>.*?</ref>|<ref\b[^>]*/>', '',t,flags=re.S)
 t=re.sub(r'\[\[([^]|]+)\|([^]]+)\]\]',lambda m:m[2],t);t=re.sub(r'\[\[([^]]+)\]\]',lambda m:m[1],t)
 for tag,label in [('cc','크레딧'),('pc','P'),('sc','평판'),('Nc','나이트웨이브 크레드')]:t=re.sub(r'\{\{'+tag+r'\|([^}]+)\}\}',lambda m:m[1]+' '+label,t,flags=re.I)
 t=re.sub(r'\{\{(?:Resource|Faction|Weapon|WF|Companion)\|([^}|]+)(?:[^}]*)\}\}',lambda m:loc(m[1]),t,flags=re.I)
 t=re.sub(r'\{\{[^{}]*\}\}','',t);t=re.sub(r'<[^>]+>','',t);t=t.replace("'''",'').replace("''",'');t=re.sub(r'^name=[^|]+\|','',t)
 return re.sub(r'\s+',' ',html.unescape(t)).strip()
def entities(raw):
 names=[]
 for a,b in re.findall(r'\[\[([^]|]+)(?:\|([^]]+))?\]\]',raw):
  label=b or a;label=label.split('#')[0]
  if a.startswith(('File:','Image:')) or re.fullmatch(r'[0-9]+x?[0-9]*px',label):continue
  if label.lower() in ['blueprint','blueprints','warframes','warframe','mastery rank','weapons','weapon','research','standing','rotation','type','market','dojo']:continue
  value=loc(label)
  if value and value not in names:names.append(value)
 return names
def cost(currency,amount,id=''):return {'id':id,'currency':currency,'amount':amount}
def route(kind,location,detail='',costs=None,condition='',source='',chance=None):return dict(kind=kind,location=location,detail=detail,costs=costs or [],condition=condition,source=source,chance=chance)
entries={};unresolved=[];source_records={}
used_by=collections.defaultdict(list)
for r in catalog['recipes']:
 for ingredient in r['ingredients']:used_by[ingredient['id']].append(r['resultId'])
def ancestor_meta(id):
 queue=[id];seen=set()
 while queue:
  current=queue.pop(0)
  if current in seen:continue
  seen.add(current);m=wf.get(current,{})
  if m.get('wikiaUrl') and items.get(current,{}).get('masterable'):return m
  queue.extend(used_by.get(current,[]))
 return {}
def get(id):
 if id in entries:return entries[id]
 meta=wf.get(id,{});parent=wf.get(bpresult.get(id,''),{});info=meta if meta.get('wikiaUrl') else parent or meta
 if not info or info.get('name') in ['Night Aspect','Day Aspect','Harness','Wings']:info=ancestor_meta(bpresult.get(id,id)) or info
 title,rev,raw,text=page_for(info);url='https://wiki.warframe.com/w/'+urllib.parse.quote(title.replace(' ','_'),safe='/')
 if rev.get('revid'):url+='?oldid='+str(rev['revid']);source_records[title]={'url':url,'revision':rev['revid'],'timestamp':rev['timestamp'],'sha256':hashlib.sha256(rev.get('slots',{}).get('main',{}).get('content','').encode()).hexdigest()}
 entry=dict(id=id,wikiUrl=url,revision=rev.get('revid',0),summary='',original=clean(raw)[:1100],routes=[],builtPlatinum=meta.get('marketCost'),blueprintCredits=items.get(id,{}).get('blueprintCredits'))
 entries[id]=entry
 target=meta.get('name','');isblueprint=id in bpresult;subject=parent if isblueprint else meta
 part=isblueprint and not items.get(bpresult.get(id,''),{}).get('masterable') and items.get(bpresult.get(id,''),{}).get('category')!='Resources'
 if title=='Model':
  token=subject.get('name','').split(' ')[0];matching=[s for s in re.split(r'\n\s*\n',raw) if re.search(r"'''"+re.escape(token)+r"'''",s,re.I)]
  if matching:raw='\n\n'.join(matching);sentences=re.split(r'(?<=[.!?])\s+',raw)
 sentences=re.split(r'(?<=[.!?])\s+',raw)
 if part:raw=' '.join(s for s in sentences if not ('main' in s.lower() and 'blueprint' in s.lower() and 'component' not in s.lower()))
 elif isblueprint and items.get(bpresult.get(id,''),{}).get('masterable'):raw=' '.join(s for s in sentences if not (('component blueprints' in s.lower() or 'its parts' in s.lower()) and 'main' not in s.lower()))
 sentences=re.split(r'(?<=[.!?])\s+|\n\s*\n',raw)
 rawlow=raw.lower();linked=entities(raw)
 r=entry['routes'];bpprice=items.get(id,{}).get('blueprintCredits');price=[cost('크레딧',bpprice)] if bpprice else []
 if title.removesuffix('/Main')==subject.get('name'):
  unit=re.search(r'^\|\s*blueprintunit\s*=\s*([^\n]+)',text,re.M|re.I);unit=unit[1].strip() if unit else 'Credits'
  m=re.search(r'^\|\s*blueprint\s*=\s*([0-9,]+)\s*$',text,re.M|re.I)
  if m and unit.lower() in ['credits','credit','cc'] and any(k in rawlow for k in ['market','research','dojo']):
   bpprice=int(m[1].replace(',',''));entry['blueprintCredits']=bpprice;price=[cost('크레딧',bpprice)]
  rendered=cache/('render-'+subject.get('name','').replace(' ','_')+'.json')
  if rendered.exists():
   renderedtext=clean(re.sub('<[^>]+>',' ',load(rendered)['parse']['text']['*']))
   if not bpprice:
    m=re.search(r'Blueprints? Price:\s*([0-9,]+)',renderedtext)
    if m:bpprice=int(m[1].replace(',',''));entry['blueprintCredits']=bpprice;price=[cost('크레딧',bpprice)]
   if not isblueprint and not entry['builtPlatinum']:
    m=re.search(r'Market Price:\s*([0-9,]+)',renderedtext)
    if m:entry['builtPlatinum']=int(m[1].replace(',',''))
 if not isblueprint and id in recipes:
  bp=get(recipes[id]['blueprintId']);r.extend(bp['routes']);entry['blueprintCredits']=bpprice or bp['blueprintCredits']
 else:
  if '{{PrimeTable' in raw or id in relic_rewards:
   r.append(route('relic','성유물 개봉 · 플레이어 거래','부품/설계도 단위로 거래 · 보유 성유물의 상세 개봉 계획에서 정련도 비교',source=url))
  if 'ZanukaPetPartHead' in id:r.append(route('companion','파보스의 자매 처치 · 플레이어 거래 → 포르투나 레그스','하운드 완성품 수령 또는 부품 설계도 획득/거래 후 조립 · 마스터리는 도금한 모델 기준',condition='모델·코어·브래킷·안정장치 필요',source='https://support.warframe.com/hc/en-us/articles/38823929417741-Companions'))
  if subject.get('name')=='Feverspine':r.append(route('race','데이모스 → 캠비온 퇴적지 → Dead Drop K-드라이브 경주','해당 경주 완료 시 피버스파인 설계도 · 경주는 매일 순환',source='https://wiki.warframe.com/w/Feverspine'))
  if ('research' in rawlow or 'dojo' in rawlow) and (re.search(r'blueprints?.{0,90}(?:research|replicat)|(?:research|replicat).{0,90}blueprint',raw,re.I|re.S) or 'can be [[research]]ed' in rawlow):
   lab=next((loc(x) for x in ['Bio Lab','Tenno Lab','Energy Lab','Chem Lab','Orokin Lab','Ventkids’ Bash Lab'] if x.lower() in rawlow),'클랜 연구실')
   r.append(route('dojo','클랜 도장 → '+lab,'연구 완료 후 설계도 복제',price,'클랜 가입·해당 연구 완료 필요',url))
  market_sentences=[s for s in sentences if re.search(r'\[\[Market(?:\||\]\])',s,re.I) and not re.search(r'not offered|cannot|not available|unavailable|not sold',s,re.I) and (not isblueprint or 'blueprint' in s.lower())]
  if market_sentences:
   role='설계도 구매' if isblueprint else '완성품 구매';amount=bpprice if isblueprint else meta.get('marketCost')
   r.append(route('market','오비터 → 상점',role,price if isblueprint else ([cost('플래티넘',amount)] if amount else []),source=url))
   if not isblueprint and not amount:
    m=re.search(r'(?:purchased|bought)[^.]{0,200}\{\{(pc|cc)\|([0-9,]+)\}\}',raw,re.I)
    if m:r[-1]['costs']=[cost('플래티넘' if m[1].lower()=='pc' else '크레딧',int(m[2].replace(',','')))]
   if isblueprint and not price:
    m=re.search(r'blueprint[^.]{0,250}(?:purchased|bought)[^.]{0,200}\{\{(pc|cc)\|([0-9,]+)\}\}',raw,re.I)
    if m:r[-1]['costs']=[cost('플래티넘' if m[1].lower()=='pc' else '크레딧',int(m[2].replace(',',''))) ]
  trans=template(raw,'Transclude')
  if 'Kuva Acquisition' in trans:r.append(route('adversary','쿠바 리치 → 최종 대결','해당 무기를 든 쿠바 리치 처치 후 주조소 수령 · 설계도 제작 방식 아님',condition='내면의 전쟁·쿠바 리치 해금',source=url))
  if 'Tenet Acquisition (Sister)' in trans:r.append(route('adversary','파보스의 자매 → 최종 대결','해당 무기를 든 자매 처치 후 주조소 수령 · 설계도 제작 방식 아님',source=url))
  if 'Tenet Acquisition (Ergo Glast)' in trans:r.append(route('vendor','릴레이 → 에르고 글래스트','4일마다 보너스 교체', [cost('커럽티드 홀로키',40)],'판매 회차당 1회',url))
  if 'Coda Acquisition' in trans:r.append(route('vendor','홀바니아 → 엘레노어','코다 A/B 판매 회차 · 보너스 교체는 일정 화면 확인',[cost('라이브 심장 세포',10)],'해당 무기 판매 회차',url))
  if 'founders' in rawlow:r.append(route('unavailable','파운더스 한정 보상','PC 글로벌 버전에서 신규 획득 불가 · 일반 거래·설계도 구매 대상 아님',source=url))
  if not isblueprint and ('/SentinelWeapons/' in id or 'automatically acquired' in rawlow) and ('automatically acquired' in rawlow or 'comes with' in rawlow):
   r.append(route('bundled','동반자 획득 시 함께 지급',' · '.join(linked[:6]),source=url))
  for paragraph in sentences:
   low=paragraph.lower();ls=entities(paragraph)
   if ('quest' in low or 'prologue' in low or 'the sacrifice' in low) and (any(s in low for s in ['awarded','rewarded','reward from','acquired during','obtained during','obtained from','is given','is granted']) or re.search(r'(?:acquired|obtained).*?(?:upon|after) completion',low)):r.append(route('quest','퀘스트 보상',' · '.join(ls[:8]),source=url))
   if any(s in low for s in ['obtained from defeating','reward for defeating','dropped by','obtainable from','obtained from rotation','can be obtained from','can be earned from','bounties']):
    nodes=re.findall(r'\bon ([A-Z][A-Za-z -]+),\s*\[\[',paragraph)
    r.append(route('drop','미션·보상 획득',' · '.join([*ls[:10],*nodes]),source=url))
   if 'invasion' in low and any(s in low for s in ['now','can be','are available','potential','reward']):r.append(route('drop','침공 → 설계도·부품 보상','현재 침공 보상 목록에서 확인 · 부품 단위 플레이어 거래 가능',source=url))
   if any(s in low for s in ['purchased from','purchased in','bought from','purchased for','buy from','sold by']):
    if '[[market]]' in low or '[[market|' in low:continue
    prices=[];m=re.search(r'(?:purchased|bought)[^.]{0,220}\{\{(sc|cc|pc|Nc)\|([0-9,]+)\}\}',paragraph,re.I)
    if m:prices=[cost({'sc':'평판','cc':'크레딧','pc':'플래티넘','nc':'나이트웨이브 크레드'}[m[1].lower()],int(m[2].replace(',','')))]
    # Multi-price main/component sentences must keep the price for this exact ID.
    if isblueprint and 'main blueprint' in low and 'component blueprint' in low:
     fragment=re.search((r'component blueprints?' if part else r'main blueprint')+r'[^;]{0,180}',paragraph,re.I)
     if fragment:
      m=re.search(r'\{\{(sc|cc|pc|Nc)\|([0-9,]+)\}\}',fragment[0],re.I)
      if m:prices=[cost({'sc':'평판','cc':'크레딧','pc':'플래티넘','nc':'나이트웨이브 크레드'}[m[1].lower()],int(m[2].replace(',','')))]
    # Resource-denominated offers such as Hunhow's Trinket are not Platinum.
    prices.extend(cost(loc(n),int(a.replace(',',''))) for a,n in re.findall(r'([0-9,]+)\s*\{\{Resource\|([^}|]+)',paragraph))
    if not prices:
     m=re.search(r'\{\{(sc|cc|pc|Nc)\|([0-9,]+)\}\}',paragraph,re.I)
     if m:prices=[cost({'sc':'평판','cc':'크레딧','pc':'플래티넘','nc':'나이트웨이브 크레드'}[m[1].lower()],int(m[2].replace(',','')))]
    vendorname=next((loc(b or a) for a,b in re.findall(r'(?:from|by|in)\s*\[\[([^]|]+)(?:\|([^]]+))?\]\]',paragraph,re.I) if not a.startswith('File:')),None)
    condition='판매 목록·회차 확인' if 'nightwave' in low or 'baro' in low else ''
    ranks=re.findall(r'Rank ([0-9]+)',paragraph,re.I)
    if ranks:condition=('평판 랭크 '+ranks[0 if part else -1]+' 필요')
    # A key/decoration that unlocks a boss is an access cost, not a BP sale.
    if 'oubliette' in low:continue
    if prices or vendorname:r.append(route('vendor',vendorname or '교환 상점',' · '.join(ls[:8]),prices,condition,source=url))
   if any(s in low for s in ['revivification','gild','conservation','incubat','breeding']):r.append(route('companion','동반자 육성·조립',' · '.join(ls[:9]),source=url))
   if "baro ki'teer" in low and 'sold' in low:
    costs=[cost('크레딧',int(a.replace(',',''))) for a in re.findall(r'\{\{cc\|([0-9,]+)\}\}',paragraph,re.I)]+[cost('두캇',int(a.replace(',',''))) for a in re.findall(r'\{\{dc\|([0-9,]+)\}\}',paragraph,re.I)]
    r.append(route('vendor','릴레이 → 바로 키 티어','미랭크·포르마/카탈리스트 미설치 완성품 플레이어 거래 가능',costs,'격주 방문 · 현재 판매 목록 확인',url))
   if 'anniversary' in low:r.append(route('event','워프레임 주년 기념 이벤트','이벤트 알림·일정 확인 · 상시 판매 아님',source=url))
   if 'epoch operation: taubound' in low and 'purchased' in low:r.append(route('event','프라그하사 → 타우바운드 작전 상점','행사 운영 기간에 교환',[cost(loc('Moonflesh'),150)],'한정 이벤트 · 현재 운영 여부 확인',url))
  for d in meta.get('drops',[]):
   if ' Relic' in d.get('location',''):continue
   location=d.get('location','');translated=' · '.join(loc(x.strip()) for x in location.split(','));r.append(route('drop',translated,'획득 표 기준',source=inventory_sources.get('WFCD:'+meta.get('category','Components'),{}).get('url',url),chance=d.get('chance')))
  # Exact storeItem IDs supply resource/standing prices. Seasonal/retired vendor
  # rows are included only if the current acquisition paragraph corroborates them.
  permanent={'EntratiLabsDisruptionVendorManifest':'생텀 아나토미카 → 로이드','OtakLastWishManifest':'네크랄리스크 → 오탁','AcrithisKullervoShopManifest':'두비리 → 아크리티스','PerrinSequenceWeaponVendorManifest':'릴레이 → 에르고 글래스트','InfestedLichWeaponVendorManifest':'홀바니아 → 엘레노어','ChipperVendorManifest':'칼 주둔지 → 치퍼','FiveFatesVendorManifest':'시터스 → 코우메이 성소','HexScaldranWeaponsVendorManifest':'홀바니아 → 아미르'}
  for vendor,offer in vendors.items():
   name=vendor.rsplit('/',1)[-1]
   for row in offer.get('items',[]):
    if row['storeItem'].replace('/Lotus/StoreItems/','/Lotus/')!=id:continue
    if name not in permanent and not any(k in rawlow for k in ['purchas','bought','offerings']):continue
    if 'Event' in name or 'Halloween' in name or 'Valentines' in name or 'RadioLegion' in name:continue
    costs=[cost(items.get(i['ItemType'],{}).get('name',loc(wf.get(i['ItemType'],{}).get('name',i['ItemType'].rsplit('/',1)[-1]))),i['ItemCount'],i['ItemType']) for i in row.get('itemPrices',[])]
    syn=row.get('syndicate',{})
    if syn.get('standingCost',0)>0:costs.append(cost('평판',syn['standingCost'],syn.get('tag','')))
    if not costs:continue
    buyer=re.search(r'(?:purchased from|bought from|purchased in)\s*\[\[([^]|]+)(?:\|([^]]+))?\]\]',raw,re.I)
    location=permanent.get(name,'교환 상점'+(' → '+loc(buyer[2] or buyer[1]) if buyer else ''))
    cond=('평판 랭크 '+str(syn['minRank'])+' 필요' if syn.get('minRank',0)>0 else '')
    if offer.get('isDynamic'):cond+=(' · ' if cond else '')+'판매 회차·운영 조건 원문 확인'
    r.append(route('vendor',location,'게임 상점 등록표 기준',costs,cond,url))
 # A literal acquisition paragraph is primary evidence even if it does not fit
 # a common route category. Keep its linked places/conditions, never invent a shop.
 if not r and raw:r.append(route('other','획득 조건',' · '.join(linked[:12]) or clean(raw)[:180],source=url))
 if not r:unresolved.append((id,items.get(id,{}).get('name'),subject.get('name'),title))
 seen=set();entry['routes']=[x for x in r if not ((key:=json.dumps(x,sort_keys=True,ensure_ascii=False)) in seen or seen.add(key))]
 # A built-credit weapon can have an internal dummy recipe. That does not make
 # its non-existent blueprint purchasable; keep the direct finished-item offer.
 if not isblueprint:
  for sentence in sentences:
   if 'blueprint' in sentence.lower():continue
   m=re.search(r'(?:purchased|bought)[^.]{0,220}\[\[Market\]\][^.]{0,100}\{\{cc\|([0-9,]+)\}\}',sentence,re.I)
   if m:entry['routes'].append(route('market','오비터 → 상점','완성품 크레딧 구매',[cost('크레딧',int(m[1].replace(',','')))],source=url))
 if not isblueprint and items.get(id,{}).get('masterable') and entry['builtPlatinum']:
  entry['routes'].append(route('market','오비터 → 상점','완성품 구매',[cost('플래티넘',entry['builtPlatinum'])],source=url))
 if not isblueprint and (meta.get('type')=='Kubrow' or meta.get('name','').endswith('Kubrow') or meta.get('name')=='Helminth Charger'):
  entry['routes']=[route('companion','오비터 인큐베이터 → 쿠브로 부화','종별 고정 구매가가 아닌 부화·유전자 템플릿 방식 · 관련 퀘스트/인큐베이터 해금 필요',source=url)]
  if meta.get('name')=='Helminth Charger':entry['routes'][0]['detail']='헬민스 시스트를 사용한 쿠브로 부화 · 감염 시스트와 인큐베이터 해금 필요'
 if not isblueprint and meta.get('name') in ['Adarza Kavat','Smeeta Kavat','Vasca Kavat']:
  entry['routes']=[route('companion','오비터 인큐베이터 → 카밧 부화','카밧 인큐베이터 업그레이드·유전자 코드·파워 코어 필요 · 종별 유전자 템플릿 사용 가능'+(' · 바스카 감염 템플릿 필요' if meta['name']=='Vasca Kavat' else ''),source=url)]
 # These literal multi-paragraph offers cannot safely be reduced to the first
 # number in a paragraph. The reviewed Wiki revision above is their provenance.
 offers={
  'Nunchasa':('세팔론 멜리카',loc('Entropic Kuva'),200,50,''),
  'Aksondol':('세팔론 멜리카',loc('Entropic Kuva'),200,60 if 'link' in subject.get('name','').lower() else 30,''),
  'Narin':('세팔론 멜리카',loc('Entropic Kuva'),400,130,'자리만의 천사들·벽 속의 속삭임 완료'),
  'Scyotid':('아크리티스 → 도미존',loc('Scuttler Husk'),48,12,'헥스 퀘스트 완료'),
  'Spinnerex':('아크리티스 → 도미존',loc('Scuttler Husk'),48,16,'헥스 퀘스트 완료'),
  'Oraxia':('아크리티스 → 도미존',loc('Scuttler Husk'),60,20,'헥스 퀘스트 완료'),
  'Baruuk':('리틀 덕 → 복스 솔라리스','평판',5000,5000,'평판 랭크 '+('3' if part else '2')+' 필요'),
  'Bonewidow':('로이드 → 네크랄로이드','평판',10000,3500,'평판 랭크 '+('2' if part else '3')+' 필요'),
  'Voidrig':('로이드 → 네크랄로이드','평판',5000,2000,'평판 랭크 '+('1' if part else '2')+' 필요 · 데이모스의 심장 완료 시 설계도 전체 무료 지급'),
  'Orion & Sirius':('헌하우 → 폰티스 탑',loc('Emerald Talent')+' 또는 '+loc('Crimson Talent'),275,90,'옥빛의 그림자: 성좌들 완료'),
  'Sirius & Orion':('헌하우 → 폰티스 탑',loc('Emerald Talent')+' 또는 '+loc('Crimson Talent'),275,90,'옥빛의 그림자: 성좌들 완료')}
 offerkey=title.removesuffix('/Main')
 if offerkey in offers and (isblueprint or id not in recipes):
  place,currency,main,component,cond=offers[offerkey];entry['routes']=[r for r in entry['routes'] if r['kind']!='vendor'];entry['routes'].append(route('vendor',place,'설계도 교환',[cost(currency,component if part else main)],cond,url))
 if id.endswith('/SentientCoreConversionABlueprint'):
  entry['routes']=[route('vendor','시터스 → 퀼','우수한 센티언트 코어 전환 · 재사용 설계도',[cost('평판',500)],source=url)]
 if offerkey=='Voidrig' and isblueprint:entry['routes'].insert(0,route('quest','데이모스의 심장 완료 보상','본체·부품 설계도 전체 지급',source=url))
 if not isblueprint and not items.get(id,{}).get('masterable') and items.get(id,{}).get('category') in ['Misc','Resources'] and rev.get('revid'):
  intro=re.split(r'^==',text,flags=re.M)[0]
  places=[p for p in entities(intro) if p.lower() not in ['resource','resources','decay','decaying mechanics'] and 'Guide' not in p]
  if places and re.search(r'resource|found|drop|obtained|mining|fishing',intro,re.I):
   notes='원문 기준 수집 지역 · 파밍 소요시간을 확정값으로 추정하지 않음'
   if subject.get('name')=='Argon Crystal':notes+=' · 아르곤 크리스탈은 시간 경과에 따라 감소하므로 제작 착수 시 수량 확인'
   if subject.get('name')=='Tellurium':notes+=' · 잠수정 아크윙(Submersible) 태그가 있는 임무의 적도 드롭'
   entry['routes']=[route('resource','자원 수집 → '+' · '.join(places[:9]),notes,source=url),*entry['routes']]
   entry['original']=clean(intro)[:1100]
 # Drop empty duplicate purchase summaries once an exact offer from the same
 # article/manifest is available. Keep literal original evidence separately.
 if any(x['kind']=='vendor' and x['costs'] for x in entry['routes']):entry['routes']=[x for x in entry['routes'] if not(x['kind']=='vendor' and not x['costs'])]
 if any(x['location']=='릴레이 → 바로 키 티어' for x in entry['routes']):entry['routes']=[x for x in entry['routes'] if x['location']!='바로 키 티어']
 if len(entry['routes'])>1:entry['routes']=[x for x in entry['routes'] if x['kind']!='other']
 entry['summary']=' / '.join(dict.fromkeys(x['location'] for x in entry['routes']))
 return entry
master=[i for i in items.values() if i['masterable']]
for item in master:get(item['id'])
# Also expose the blueprint/part requirements of these equipment recipes.
def dependencies(id,seen):
 if id in seen:return
 seen.add(id);r=recipes.get(id)
 if not r:return
 get(r['blueprintId'])
 for part in r['ingredients']:
  if part['id'] in recipes:dependencies(part['id'],seen)
  elif wf.get(part['id'],{}).get('drops'):get(part['id'])
for i in master:dependencies(i['id'],set())
materials=set()
def gather_materials(id,seen):
 if id in seen:return
 seen.add(id)
 for ingredient in recipes.get(id,{}).get('ingredients',[]):
  item=items.get(ingredient['id'],{})
  if not item.get('masterable') and item.get('category') in ['Misc','Resources']:materials.add(ingredient['id'])
  else:gather_materials(ingredient['id'],seen)
for i in master:gather_materials(i['id'],set())
for id in sorted(materials):
 if page_for(wf.get(id,{}))[1].get('revid'):get(id)
unresolved=[x for x in unresolved if not entries[x[0]]['routes']]
doc=dict(version=1,retrievedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),entries=list(entries.values()),sources=list(source_records.values()),additionalSources=[dict(url=v['url'],sha256=v.get('sha256','')) for v in inventory_sources.values() if v.get('url')]+[dict(url='https://github.com/calamity-inc/warframe-public-export-plus/blob/senpai/ExportVendors.json',sha256=hashlib.sha256((root/'artifacts/worldstate-research/ExportVendors.json').read_bytes()).hexdigest())])
(root/'data/inventory-acquisition.json').write_text(json.dumps(doc,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
renders=[]
for p in cache.glob('render-*.json'):
 parsed=load(p)['parse'];renders.append(dict(title=parsed['title'],revision=parsed['revid'],url='https://wiki.warframe.com/w/'+urllib.parse.quote(parsed['title'].replace(' ','_'),safe='/')+'?oldid='+str(parsed['revid']),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),kind='rendered-wiki-response'))
provenance=dict(retrievedUtc=doc['retrievedUtc'],dataSha256=hashlib.sha256((root/'data/inventory-acquisition.json').read_bytes()).hexdigest(),sources=[*doc['sources'],*doc['additionalSources'],*renders],unresolvedExportBlueprints=unresolved)
(root/'data/inventory-acquisition-provenance.json').write_text(json.dumps(provenance,ensure_ascii=False,indent=2),encoding='utf-8')
(cache/'unresolved.json').write_text(json.dumps(unresolved,ensure_ascii=False,indent=2),encoding='utf-8')
print({'equipment':len(master),'entries':len(entries),'sources':len(source_records),'equipmentWithRoute':sum(bool(entries[i['id']]['routes']) for i in master),'unresolved':len(unresolved)})
print(unresolved[:50])
