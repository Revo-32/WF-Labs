"""Pin syndicate identity and enemy affinity; never infer IDs at runtime."""
import json,hashlib
from ranged_source_lib import ROOT,PAGES,SOURCES,WEAPONS
from wiki_tables import parse

groups={
 'Justice':('Blast',['Vaykor Hek','Vaykor Marelok']),
 'Truth':('Gas',['Telos Boltor','Telos Akbolto']),
 'Entropy':('Magnetic',['Synoid Simulor','Synoid Gammacor']),
 'Sequence':('Radiation',['Secura Penta','Secura Dual Cestra']),
 'Blight':('Viral',['Rakta Cernos','Rakta Ballistica']),
 'Purity':('Corrosive',['Sancti Tigris','Sancti Castanas'])}
rules=[]
for code,(damage,names) in groups.items():
 for name in names:
  assert name in PAGES[code] or code=='Entropy'
  rules.append(dict(weaponId=WEAPONS[name]['id'],code=code,damageType=damage,source=SOURCES[code]))
records=json.loads((ROOT/'data/enemy-source-records.json').read_bytes())
for r in records: assert hashlib.sha256(r['content'].encode()).hexdigest()==r['sha256']
modules={r['title']:parse(r['content']) for r in records if r['title'].startswith('Module:Enemies/data/')}
enemies=[]
for e in json.loads((ROOT/'data/enemy-catalog.json').read_bytes())['enemies']:
 raw=next(r for r in modules[e['source']['title']].values() if r.get('General',{}).get('InternalName')==e['id'] and r['General']['Name']==e['englishName'])
 assert 'Affinity' in raw['Stats'],e['englishName']
 enemies.append(dict(id=e['id'],baseAffinity=raw['Stats']['Affinity'],source=e['source']))
result=dict(rules=rules,enemies=enemies,sources=[SOURCES[x] for x in ['Syndicate Radial Effects','Affinity','Enemy Level Scaling']],notes=['Justice 개별 문서의 Blast 상태와 총괄 문서의 상태 없음이 충돌하므로 선택 조건을 요구한다.','숙련도 레벨 공식은 현재 레벨을 사용하며 기본 레벨을 빼지 않는다.'])
(ROOT/'data/ranged-syndicates.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'{len(rules)} syndicate weapons, {len(enemies)} affinity records verified')
