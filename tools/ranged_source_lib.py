"""Read pinned wiki revisions. No network access or fuzzy runtime name matching."""
import hashlib,json,pathlib,re
ROOT=pathlib.Path(__file__).resolve().parents[1]
CATALOG=json.loads((ROOT/'data/reference-catalog.json').read_text(encoding='utf-8'))
SOURCES={s['title']:s for s in json.loads((ROOT/'data/ranged-sources/manifest.json').read_bytes())['sources'] if not s.get('missing')}
PAGES={}
batches={}
for title,source in SOURCES.items():
 batch=source['batch']
 if batch not in batches:
  batches[batch]=json.loads((ROOT/'data/ranged-sources'/batch).read_bytes())['query']['pages']
 page=next(p for p in batches[batch] if p['title']==title)
 revision=next(r for r in page['revisions'] if r['revid']==source['revision'])
 content=revision['slots']['main']['content']
 assert revision['timestamp']==source['timestamp'],title
 assert hashlib.sha256(content.encode()).hexdigest()==source['sha256'],title
 PAGES[title]=re.split(r'\n==\s*Patch History\s*==',content,maxsplit=1)[0]
WEAPONS={w['englishName']:w for w in CATALOG['weapons'] if w['category'] in ['primary','secondary']}
