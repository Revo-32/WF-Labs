"""Import reward-only Korean market metadata; never export orders or player records."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import urllib.request

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--snapshot', type=Path)
args = parser.parse_args()
url = 'https://api.warframe.market/v2/items'
if args.snapshot:
    raw = args.snapshot.read_bytes()
    retrieved = datetime.datetime.fromtimestamp(args.snapshot.stat().st_mtime, datetime.timezone.utc)
else:
    request = urllib.request.Request(url, headers={
        'User-Agent': 'WF-Labs/0.10.0 (+https://github.com/Revo-32/WF-Labs)',
        'Language': 'ko', 'Platform': 'pc', 'Crossplay': 'true'})
    with urllib.request.urlopen(request, timeout=40) as response:
        raw = response.read()
    retrieved = datetime.datetime.now(datetime.timezone.utc)
data = json.loads(raw)
extra = {'lohk', 'xata', 'jahu', 'vome', 'ris', 'fass', 'netra', 'khra', 'ayatan_amber_star'}
items = []
for item in data['data']:
    tags = set(item.get('tags', []))
    prime = item.get('ducats', 0) > 0 and 'prime' in tags and 'set' not in tags and bool(tags & {'component', 'blueprint'})
    if not prime and item['slug'] not in extra:
        continue
    assert item['i18n']['ko']['name'], item['slug']
    row = {key: item[key] for key in ('id', 'slug', 'gameRef', 'tags', 'ducats', 'maxRank', 'maxCharges') if key in item}
    row['i18n'] = {lang: {'name': item['i18n'][lang]['name']} for lang in ('ko', 'en')}
    items.append(row)
assert len(items) > 500 and len({x['id'] for x in items}) == len(items)
output = {'retrievedUtc': retrieved.isoformat(), 'source': url, 'apiVersion': data['apiVersion'],
          'sourceSha256': hashlib.sha256(raw).hexdigest(), 'data': items}
(root / 'data/relic-market-items.json').write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'보상 품목 {len(items)}개 · 주문/사용자/가격 저장 없음')
