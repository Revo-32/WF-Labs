"""Audit normalized enemy statistics against pinned Wiki Lua without running Lua."""
import argparse,hashlib,json,pathlib
from wiki_tables import parse

root=pathlib.Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--report',type=pathlib.Path);args=parser.parse_args()
records=json.loads((root/'data/enemy-source-records.json').read_text(encoding='utf-8'))
catalog=json.loads((root/'data/enemy-catalog.json').read_text(encoding='utf-8'))
sources={r['title']:r for r in records}
for r in records:
 assert hashlib.sha256(r['content'].encode()).hexdigest()==r['sha256'],r['title']
modules={r['title']:parse(r['content']) for r in records if r['title'].startswith('Module:Enemies/data/')}
checks=[]
gadget=sources['MediaWiki:Gadget-enemyinfoboxslider.js']['content']
assert gadget.index('base_armor = base_armor * icebind_multi') < gadget.index('var armor = Math.floor(base_armor * armor_multi)') < gadget.index('armor = Math.min(armor, 2700)')
assert 'let icebind_multi = 3.0 + (player_count * 1.0)' in gadget
for e in catalog['enemies']:
 s=e['source'];assert all(s[k]==sources[s['title']][k] for k in ['revision','sha256','url'])
 matches=[r for r in modules[s['title']].values() if r.get('General',{}).get('InternalName')==e['id']]
 raw=next(r for r in matches if r['General']['Name']==e['englishName'])
 assert raw['General']['Faction'].replace(' ','')==e['faction']
 for dest,source in [('health','Health'),('armor','Armor'),('shields','Shield'),('baseLevel','BaseLevel')]:assert e[dest]==raw['Stats'].get(source,0),(e['englishName'],source)
 for dest,source in [('eximusHealth','EximusHealth'),('eximusShields','EximusShield'),('eximusArmor','EximusArmor'),('eximusOverguard','EximusOverguard')]:assert e[dest]==raw['Stats'].get(source),(e['englishName'],source)
 assert not raw['Stats'].get('ProcResists'),e['englishName']
 checks.append(dict(id=e['id'],enemy=e['englishName'],revision=s['revision'],passed=True))
result=dict(passed=True,enemies=len(checks),sourceHashes=len(records),factions=len(set(e['faction'] for e in catalog['enemies'])),checks=checks,note='수치·내부 ID·원본 해시 검사. 한국어 표시명과 적 고유 전투 이벤트 전체 검증은 아님.')
if args.report:args.report.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(f"Pinned sources {len(records)}, enemy records {len(checks)}: verified")
