"""Normalize public forecast/node/vendor facts without copying upstream algorithms.
Run after downloading sources into artifacts/worldstate-research.
"""
import pathlib,json,re,hashlib,datetime,urllib.request
root=pathlib.Path(__file__).resolve().parents[1];cache=root/'artifacts/worldstate-research'
def load(n):return json.loads((cache/n).read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
regions=load('ExportRegions.json');ko=load('dict.ko.json');inventory=json.loads((root/'data/inventory-catalog.json').read_text(encoding='utf-8'))
items={i['id']:i for i in inventory['items']};tiers=dict(re.findall(r'(\w+):\s*"([SABCD])"', (cache/'supplemental-data-arbyTiers.js').read_text()))
rows=[l.split(',') for l in (cache/'arby.txt').read_text().splitlines() if l.strip()];used={r[1] for r in rows};nodes=[]
factions=load('ExportFactions.json')
for id in sorted(used):
 r=regions.get(id)
 if r is None:raise ValueError('Missing node '+id)
 loc=lambda tag:ko.get(tag,tag)
 faction=ko.get(factions.get(r.get('faction'),{}).get('name',''),'진영 확인 필요')
 if id=='SolNode450':faction='그리니어·코퍼스'
 nodes.append(dict(id=id,name=loc(r.get('name','')),planet=loc(r.get('systemName','')),mission=loc(r.get('missionName','')),faction=faction,tier=tiers.get(id,'F')))
vendors=load('ExportVendors.json');offers=[]
for key,shop in [('PerrinSequenceWeaponVendorManifest','tenet'),('InfestedLichWeaponVendorManifest','coda')]:
 v=next(v for k,v in vendors.items() if k.endswith('/'+key))
 for row in v['items']:
  id=row['storeItem'].replace('/Lotus/StoreItems/','/Lotus/');item=items[id];assert row['durationHours']==96 and row['purchaseLimit']==1
  offers.append(dict(id=id,name=item['name'],englishName=item['englishName'],shop=shop,batch='A' if shop=='coda' and row['bin']==1 else 'B' if shop=='coda' else '',price=row['itemPrices'][0]['ItemCount'],currency=ko['/Lotus/Language/1999Coda/CodaWeaponBucksName'] if shop=='coda' else ko['/Lotus/Language/Currency/GranumBucksName'],masteryRequired=item['masteryRequired']))
assert len(offers)==19 and len([o for o in offers if o['batch']=='A'])==7 and len([o for o in offers if o['batch']=='B'])==7
source=[]
for filename,url in [('arby.txt','https://browse.wf/arbys.txt'),('supplemental-data-arbyTiers.js','https://raw.githubusercontent.com/calamity-inc/browse.wf/senpai/supplemental-data/arbyTiers.js'),('ExportRegions.json','https://raw.githubusercontent.com/calamity-inc/warframe-public-export-plus/senpai/ExportRegions.json'),('ExportVendors.json','https://raw.githubusercontent.com/calamity-inc/warframe-public-export-plus/senpai/ExportVendors.json'),('dict.ko.json','https://raw.githubusercontent.com/calamity-inc/warframe-public-export-plus/senpai/dict.ko.json')]:source.append(dict(url=url,sha256=sha(cache/filename)))
for page in ['coda-weapons','tenet-weapons']:
 r=load(page+'-revision.json')['query']['pages'][0];v=r['revisions'][0];source.append(dict(title=r['title'],revision=v['revid'],timestamp=v['timestamp'],url='https://wiki.warframe.com/w/'+r['title'].replace(' ','_')+'?oldid='+str(v['revid']),sha256=hashlib.sha256(v['slots']['main']['content'].encode()).hexdigest()))
doc=dict(version=1,retrievedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),nodes=nodes,offers=offers,tenetEpoch='2015-12-03T00:00:00Z',codaEpoch='2025-03-18T00:00:00Z',tierSource='Arbitration Goons / browse.wf',sources=source)
(root/'data/world-schedules.json').write_text(json.dumps(doc,ensure_ascii=False,indent=2),encoding='utf-8')
(root/'data/arbitration-schedule.csv').write_text('\n'.join(','.join(r) for r in rows)+'\n',encoding='utf-8')
(root/'data/world-schedule-sources.json').write_text(json.dumps(dict(retrievedUtc=doc['retrievedUtc'],sources=source,notes=['중재 등급과 예측 일정: Arbitration Goons / browse.wf. https://browse.wf/about 의 Raw Data / Arbitrations 이용 고지에 따라 출처를 표시한다.','지구 숲의 예전 4시간 낮밤 API 필드는 사용하지 않고 Update 38.5의 평원 동기화 규칙을 따른다.','코다 기준점은 현재 위키 주기식을 따른다. 초기 출시 공지의 첫 교체 날짜와 차이가 있어 위키 기준 예상으로 표시한다.','원소·보너스는 제보값이며 해당 회차에서 값이 변경된 근거가 있을 때만 현회차 제보로 표시한다.'],officialRules=['https://www.warframe.com/en/patch-notes/pc/38-5-0'],worldState='https://api.warframestat.us/pc?language=ko',officialWorldState='https://api.warframe.com/cdn/worldState.php'),ensure_ascii=False,indent=2),encoding='utf-8')
license_url='https://raw.githubusercontent.com/calamity-inc/warframe-public-export-plus/senpai/LICENSE'
(root/'data/PUBLIC-EXPORT-PLUS-LICENSE.txt').write_bytes(urllib.request.urlopen(license_url,timeout=30).read())
(root/'data/BROWSE-WF-NOTICE.txt').write_text('중재 예측 일정: browse.wf / Calamity, Inc.\n중재 등급: Arbitration Goons. 게임 공식 등급이 아닙니다.\nhttps://browse.wf/arbys\nhttps://browse.wf/about\nhttps://github.com/calamity-inc/browse.wf\n공개 데이터의 이용·출처 표시 안내: https://browse.wf/about\n게임 명칭·자료의 권리는 Digital Extremes에 있습니다.\n원본 URL·개정·SHA-256: Data/world-schedule-sources.json\n',encoding='utf-8')
print(dict(nodes=len(nodes),slots=len(rows),offers=len(offers),tierS=[n['name'] for n in nodes if n['tier']=='S']))
