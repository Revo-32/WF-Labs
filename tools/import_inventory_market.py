"""Link public market metadata to exact inventory gameRef IDs, never item names."""
import argparse, hashlib, json, os, pathlib

root=pathlib.Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser()
parser.add_argument('--manifest',type=pathlib.Path,default=pathlib.Path(os.environ['LOCALAPPDATA'])/'WF Labs/relic-market-cache/items.json')
args=parser.parse_args()
raw=args.manifest.read_bytes();data=json.loads(raw.decode('utf-8-sig'))
catalog=json.loads((root/'data/inventory-catalog.json').read_text(encoding='utf-8'))
allowed={i['id'] for i in catalog['items'] if i['category']!='Mods'}
entries=[];seen=set()
for row in data['data']:
    game_id=row.get('gameRef','').replace('/Lotus/StoreItems/','/Lotus/')
    if game_id not in allowed or game_id in seen or any(t in row.get('tags',[]) for t in ['set','bundle']):continue
    names=row.get('i18n',{});ko=names.get('ko',{}).get('name');en=names.get('en',{}).get('name')
    if not ko or not en:continue
    seen.add(game_id)
    entries.append(dict(gameId=game_id,item=dict(id=row['id'],slug=row['slug'],name=ko,englishName=en,ducats=row.get('ducats',0),tradable=True,charges=row.get('maxCharges'),marketRank=row.get('maxRank'))))
document=dict(retrievedUtc=data['retrievedUtc'],source='https://api.warframe.market/v2/items',sourceSha256=hashlib.sha256(raw).hexdigest(),entries=entries)
(root/'data/inventory-market-items.json').write_text(json.dumps(document,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
print(dict(linked=len(entries),source=document['source']))
