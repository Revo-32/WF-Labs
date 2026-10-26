"""Normalize pinned official Korean Public Export recipes/relics + WFCD mastery metadata.
Usage: python tools/import_inventory_catalog.py <download cache>
Source payloads stay outside the repo; hashes and exact source URLs are recorded.
"""
import sys,json,pathlib,re,hashlib,datetime
root=pathlib.Path(__file__).resolve().parents[1];cache=pathlib.Path(sys.argv[1])
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'),strict=False)
def clean(s):return re.sub(r'<[^>]*>','',s).strip()
sources=load(cache/'export-sources.json'); official={}; recipes=[]; relics=[]
for k in sources:
 doc=load(cache/(k+'_ko.json'))
 for rows in doc.values():
  if isinstance(rows,list):
   for x in rows:
    if isinstance(x,dict) and x.get('uniqueName') and x.get('name'):official[x['uniqueName']]=clean(x['name'])
 if k=='ExportRecipes':recipes=doc[k]
 if k=='ExportRelicArcane':relics=doc[k]
wf={}
wfcd_revision=load(cache/'wfcd-source.json')['commit']
for category in ['Primary','Secondary','Melee','Warframes','Archwing','Arch-Gun','Arch-Melee','Sentinels','SentinelWeapons','Pets','Misc','Components','Resources','Relics']:
 path=cache/(category+'.json')
 if not path.exists():continue
 sources['WFCD:'+category]={'url':'https://raw.githubusercontent.com/WFCD/warframe-items/'+wfcd_revision+'/data/json/'+category+'.json','sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'revision':wfcd_revision}
 for x in load(path):wf[x['uniqueName']]=x
items={}
for id,x in wf.items():
 if x.get('masterable') or id in official:
  cat=x.get('category','Resources');rate=200 if cat in ['Warframes','Pets','Sentinels','Archwing'] and x.get('type') not in ['Sentinel Weapon'] else 100
  # Modular companion heads are exported in a weapon product bin. They still
  # award companion mastery (200/rank), not the pistol bin's 100/rank.
  if x.get('type')=='K-Drive Component':rate=200
  modular=bool(re.search(r'Modular|OperatorAmp',id)) or (cat=='Pets' and (any(s in id for s in ['/MoaPets/','/ZanukaPets/']) or any(s in x.get('name','') for s in ['Predasite','Vulpaphyla'])))
  name=official.get(id)
  if not name:continue
  cap=40 if x.get('type')=='Necramech' else x.get('maxLevelCap',30)
  items[id]=dict(id=id,name=name,englishName=x.get('name',''),category=cat,masterable=bool(x.get('masterable')),pointsPerRank=rate,maxRank=cap,masteryRequired=x.get('masteryReq',0),gildRequired=modular,tradable=x.get('tradable',False),blueprintCredits=x.get('bpCost'),marketId=None)
for id,name in official.items():
 if id not in items:items[id]=dict(id=id,name=name,englishName='',category='Mods' if '/Upgrades/' in id else 'Resources',masterable=False,pointsPerRank=0,maxRank=0,masteryRequired=0,gildRequired=False,tradable=False,blueprintCredits=None,marketId=None)
outrecipes=[]
for r in recipes:
 result=r['resultType'];bp=r['uniqueName']
 if result not in items:continue
 if bp not in items:items[bp]=dict(id=bp,name=items[result]['name']+' 설계도',englishName='',category='Components',masterable=False,pointsPerRank=0,maxRank=0,masteryRequired=0,gildRequired=False,tradable=bool(r.get('primeSellingPrice')),blueprintCredits=items[result]['blueprintCredits'],marketId=None)
 ingredients=[dict(id=i['ItemType'],count=i['ItemCount']) for i in r.get('ingredients',[])]
 if items[result]['blueprintCredits'] is not None:items[bp]['blueprintCredits']=items[result]['blueprintCredits']
 outrecipes.append(dict(blueprintId=bp,resultId=result,credits=r['buildPrice'],seconds=r['buildTime'],quantity=r.get('num',1),consumeBlueprint=r.get('consumeOnUse',True),ingredients=ingredients))
# Stable gameRefs, never fuzzy item names. The market supplies localized names for
# Prime components absent from Public Export and shares the existing price cache.
market=load(root/'data/relic-market-items.json')['data']
for m in market:
 id=m.get('gameRef')
 if not id:continue
 id=id.replace('/Lotus/StoreItems/','/Lotus/')
 ko=m.get('i18n',{}).get('ko',{}).get('name')
 if not ko:continue
 if id not in items:items[id]=dict(id=id,name=ko,englishName=m.get('i18n',{}).get('en',{}).get('name',''),category='Components',masterable=False,pointsPerRank=0,maxRank=0,masteryRequired=0,gildRequired=False,tradable=True,blueprintCredits=None,marketId=m['id'])
 else:items[id]['marketId']=m['id'];items[id]['tradable']=True
outrelics=[]
for r in relics:
 rewards=r.get('relicRewards',[])
 # Only the standard six-slot relic distribution is used for refinement maths.
 if len(rewards)!=6 or sorted(x['rarity'] for x in rewards)!=['COMMON']*3+['RARE']+['UNCOMMON']*2:continue
 id=r['uniqueName']; suffix=next((s for s in ['Bronze','Silver','Gold','Platinum'] if id.endswith(s)),None)
 if not suffix:continue
 level=['Bronze','Silver','Gold','Platinum'].index(suffix)
 slots=[dict(id=x['rewardName'].replace('/Lotus/StoreItems/','/Lotus/'),rarity=x['rarity'],count=x.get('itemCount',1)) for x in rewards]
 outrelics.append(dict(id=id,family=id[:-len(suffix)],name=clean(r['name']),refinement=level,rewards=slots))
doc=dict(schemaVersion=1,retrievedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),items=list(items.values()),recipes=outrecipes,relics=outrelics)
(root/'data/inventory-catalog.json').write_text(json.dumps(doc,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
(root/'data/inventory-sources.json').write_text(json.dumps(dict(retrievedUtc=doc['retrievedUtc'],sources=sources,masteryRules=['https://support.warframe.com/hc/en-us/articles/218718097-Mastery-Basics','https://wiki.warframe.com/w/Mastery_Rank'],reviewedOverrides=[dict(field='maxRank',type='Necramech',value=40,source='https://wiki.warframe.com/w/Rank'),dict(field='pointsPerRank',category='Pets',value=200,note='모아·하운드 헤드의 productCategory=Pistols를 마스터리 분류로 사용하지 않음')],refinementRules='https://www.warframe.com/fr/patch-notes/pc/20-7-4'),ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(items=len(items),masterable=sum(x['masterable'] for x in items.values()),recipes=len(outrecipes),relics=len(outrelics),unresolvedRewards=len({x['id'] for r in outrelics for x in r['rewards'] if x['id'] not in items}))))
