"""Pin research originals for combat, Zaws and Riven attributes; never execute Wiki Lua."""
import concurrent.futures, datetime, hashlib, json, pathlib, urllib.parse, urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]
DEST = ROOT / 'data' / 'combat-expansion-sources'
DEST.mkdir(exist_ok=True)
TITLES = ['Damage/Slash Damage', 'Damage/Heat Damage', 'Damage/Electricity Damage',
          'Damage/Toxin Damage', 'Damage/Gas Damage', 'Damage/Blast Damage',
          'Damage/Cold Damage', 'Damage/Magnetic Damage', 'Damage/Tau Damage',
          'Riven Mods', 'Zaw', 'Module:Zaws/data', 'Lesion', 'Dual Toxocyst',
          'Melee Animosity', 'Melee Exposure', 'Melee Afflictions', 'Melee Duplicate',
          'Exodia Force', 'Exodia Contagion', 'Harmony', 'Shattering Impact', 'Berserker Fury']

def fetch(title):
    query = urllib.parse.urlencode(dict(action='query', prop='revisions', titles=title,
        rvprop='ids|timestamp|content', rvslots='main', format='json', formatversion=2, redirects=1))
    req = urllib.request.Request('https://wiki.warframe.com/api.php?' + query,
        headers={'User-Agent':'WFLabs/0.7 source verification'})
    data = json.load(urllib.request.urlopen(req, timeout=50))
    page = data['query']['pages'][0]
    if 'revisions' not in page: return {'title':title, 'missing':True}
    rev = page['revisions'][0]
    content = rev['slots']['main']['content']
    name = str(rev['revid']) + '.txt'
    (DEST / name).write_text(content, encoding='utf-8', newline='\n')
    return dict(title=page['title'], revision=rev['revid'], timestamp=rev['timestamp'],retrievedAt=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        url='https://wiki.warframe.com/w/' + urllib.parse.quote(page['title'].replace(' ', '_')) + '?oldid=' + str(rev['revid']),
        sha256=hashlib.sha256(content.encode()).hexdigest(), file=name)

if __name__ == '__main__':
    with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:
        records = list(pool.map(fetch, TITLES))
    manifest=DEST/'manifest.json'
    previous=json.loads(manifest.read_text(encoding='utf-8')) if manifest.exists() else []
    merged={record['title']:record for record in previous+records}
    manifest.write_text(json.dumps(list(merged.values()), ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(records, ensure_ascii=False, indent=2))
