"""Freeze exact market set membership and quantityInSet using stable item/gameRef IDs."""
import datetime, hashlib, json, pathlib, time, urllib.error, urllib.request

root = pathlib.Path(__file__).resolve().parents[1]
cache = root/'artifacts/farming-market-sources'
cache.mkdir(parents=True, exist_ok=True)
sources = []
last_request = 0.0

def fetch(slug=None):
    global last_request
    url = 'https://api.warframe.market/v2/items' + ('/'+slug if slug else '')
    path = cache/((slug or 'items')+'.json')
    if path.exists():
        raw = path.read_bytes()
    else:
        for attempt in range(5):
            time.sleep(max(0, .40-(time.monotonic()-last_request)))
            last_request = time.monotonic()
            try:
                with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent':'WF-Labs-data-import/0.11'}), timeout=40) as response:
                    raw = response.read()
                json.loads(raw)
                path.write_bytes(raw)
                break
            except (urllib.error.URLError, TimeoutError):
                if attempt == 4: raise
                time.sleep(2*(attempt+1))
    sources.append({'url':url, 'sha256':hashlib.sha256(raw).hexdigest()})
    return json.loads(raw)['data']

catalog = json.loads((root/'data/inventory-catalog.json').read_text(encoding='utf-8-sig'))
items = {i['id']:i for i in catalog['items']}
manifest = fetch()
market = {i['id']:i for i in manifest}
normalize = lambda s:s.replace('/Lotus/StoreItems/','/Lotus/')
roots = [i for i in manifest if 'set' in i.get('tags',[]) and normalize(i.get('gameRef','')) in items]
details = {}
bundles = []
skipped = []
def descriptor(row, name):
    return dict(id=row['id'], slug=row['slug'], name=name, englishName=row['i18n'].get('en',{}).get('name',name), ducats=row.get('ducats',0), tradable=True)
for index, summary in enumerate(roots):
    data = fetch(summary['slug'])
    if not data.get('setRoot'):
        skipped.append({'id':summary['id'],'reason':'setRoot가 확인되지 않음'}); continue
    parts = []
    reason = None
    for part_id in dict.fromkeys(data.get('setParts',[])):
        if part_id == data['id']: continue
        row = market.get(part_id)
        game_id = normalize(row.get('gameRef','')) if row else ''
        if game_id not in items:
            reason = '게임 품목 ID가 연결되지 않은 구성품: '+part_id; break
        if part_id not in details: details[part_id] = fetch(row['slug'])
        detail = details[part_id]
        quantity = detail.get('quantityInSet')
        if not isinstance(quantity,int) or quantity < 1 or data['id'] not in detail.get('setParts',[]):
            reason = '구성 수량·세트 상호 참조 확인 필요: '+part_id; break
        parts.append(dict(gameId=game_id,count=quantity,item=descriptor(row,items[game_id]['name'])))
    if reason or not parts:
        skipped.append({'id':summary['id'],'reason':reason or '구성품 없음'})
    else:
        result_id = normalize(summary['gameRef'])
        bundles.append(dict(resultId=result_id,item=descriptor(summary,items[result_id]['name']+' 세트'),parts=parts))
    if index % 20 == 0: print(f'세트 {index+1}/{len(roots)} · 확인 {len(bundles)} · 제외 {len(skipped)}', flush=True)

document = dict(schemaVersion=1,retrievedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                source='https://api.warframe.market/v2/items', bundles=bundles, skipped=skipped, sources=sources)
destination = root/'data/inventory-market-bundles.json'
destination.write_text(json.dumps(document,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
print(json.dumps({'bundles':len(bundles),'parts':len(details),'skipped':skipped,'sources':len(sources)},ensure_ascii=False),flush=True)
