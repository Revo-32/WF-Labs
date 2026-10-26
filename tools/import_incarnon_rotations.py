"""Normalize the wiki rotation and bind it to official weekly choices and item IDs."""
import datetime, hashlib, html, json, pathlib, re, urllib.parse, urllib.request

root = pathlib.Path(__file__).resolve().parents[1]
cache = root / 'artifacts/weapon-rotation-research'
cache.mkdir(exist_ok=True)

def fetch(name, url):
    path = cache / name
    if not path.exists():
        request = urllib.request.Request(url, headers={'User-Agent': 'WF-Labs/0.10.0'})
        path.write_bytes(urllib.request.urlopen(request, timeout=20).read())
    return path

page = fetch('incarnon.html', 'https://wiki.warframe.com/w/Incarnon')
revision = fetch('incarnon-revision.json', 'https://wiki.warframe.com/api.php?action=query&prop=revisions&titles=Incarnon&rvprop=ids%7Ctimestamp%7Ccontent&rvslots=main&format=json&formatversion=2&redirects=1')
official = fetch('official-worldstate.json', 'https://api.warframe.com/cdn/worldState.php')
api = fetch('api-worldstate.json', 'https://api.warframestat.us/pc?language=ko')
groups = {}
for row in re.findall(r'<tr[^>]*>(.*?)</tr>', page.read_text(encoding='utf-8'), re.S):
    text = html.unescape(re.sub('<[^>]+>', ' ', row))
    match = re.search(r'Week\s*([1-9])\s*\(', text)
    names = list(dict.fromkeys(urllib.parse.unquote(n).replace('_', ' ') for n in re.findall(r'href="/w/([^"]+)_Incarnon_Genesis"', row)))
    if match and len(names) == 5:
        week = int(match[1])
        if week in groups:
            assert groups[week] == names
        groups[week] = names
assert sorted(groups) == list(range(1, 10))
inventory = json.loads((root / 'data/inventory-catalog.json').read_text(encoding='utf-8'))['items']
rows = []
for week, names in sorted(groups.items()):
    weapons = []
    for name in names:
        matches = [i for i in inventory if i['englishName'] == name and i['masterable']]
        assert len(matches) == 1, (name, len(matches))
        item = matches[0]
        weapons.append(dict(id=item['id'], name=item['name'], englishName=name, category=item['category']))
    rows.append(dict(week=week, weapons=weapons))
raw = json.loads(official.read_text(encoding='utf-8'))
schedule = raw['EndlessXpSchedule'][0]
choices = next(c['Choices'] for c in schedule['CategoryChoices'] if c['Category'] == 'EXC_HARD')
anchor_week = next(w for w, names in groups.items() if set(names) == set(choices))
def date(value):
    return datetime.datetime.fromtimestamp(int(value['$date']['$numberLong']) / 1000, datetime.timezone.utc)
start, end = date(schedule['Activation']), date(schedule['Expiry'])
assert end - start == datetime.timedelta(days=7) and start.weekday() == 0 and start.hour == 0
rev = json.loads(revision.read_text(encoding='utf-8'))['query']['pages'][0]['revisions'][0]
doc = dict(version=1, retrievedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), anchorUtc=start.isoformat(), anchorWeek=anchor_week,
           wikiUrl=f"https://wiki.warframe.com/w/Incarnon?oldid={rev['revid']}", revision=rev['revid'], weeks=rows,
           sources=[dict(url=url, sha256=hashlib.sha256(path.read_bytes()).hexdigest()) for path, url in [(page,'https://wiki.warframe.com/w/Incarnon'), (revision,'https://wiki.warframe.com/api.php'), (official,'https://api.warframe.com/cdn/worldState.php')]])
(root / 'data/incarnon-rotations.json').write_text(json.dumps(doc, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(dict(weeks=len(rows), weapons=sum(len(r['weapons']) for r in rows), anchorUtc=doc['anchorUtc'], anchorWeek=anchor_week,
                      apiDuviri=json.loads(api.read_text(encoding='utf-8')).get('duviriCycle')), ensure_ascii=False)[:1800])
