"""Re-fetch exactly the pinned Wiki revisions and Export manifests into a scratch cache."""
import argparse,json,pathlib,urllib.request,urllib.parse,hashlib

root=pathlib.Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--cache',type=pathlib.Path,required=True);args=parser.parse_args();args.cache.mkdir(parents=True,exist_ok=True)
manifest=json.loads((root/'data/full-source-records.json').read_text(encoding='utf-8'))
rules=json.loads((root/'data/combat-rule-sources-v05.json').read_text(encoding='utf-8'))
def fetch(url):
    return urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'WFLabs/0.5 pinned reference importer'}),timeout=90).read()
for filename,sources in [('modules-api.json',[s for s in manifest['sources'] if not s['title'].startswith('Module:Weapons/data/')]),('weapon-partitions-api.json',[s for s in manifest['sources'] if s['title'].startswith('Module:Weapons/data/')]),('rules-api.json',rules['sources'])]:
    query=urllib.parse.urlencode({'action':'query','prop':'revisions','revids':'|'.join(str(s['revision']) for s in sources),'rvprop':'ids|timestamp|content','rvslots':'main','format':'json','formatversion':'2'})
    data=fetch('https://wiki.warframe.com/api.php?'+query)
    bytitle={s['title']:s for s in sources}
    for page in json.loads(data)['query']['pages']:
        revision=page['revisions'][0];expected=bytitle.pop(page['title'])
        if revision['revid']!=expected['revision'] or hashlib.sha256(revision['slots']['main']['content'].encode()).hexdigest()!=expected['sha256']:raise ValueError('위키 개정/해시 불일치: '+page['title'])
    if bytitle:raise ValueError('위키 원본 누락: '+str(bytitle))
    (args.cache/filename).write_bytes(data)
for kind,source in manifest['exportSources'].items():
    data=fetch(source['url'])
    if hashlib.sha256(data).hexdigest()!=source['sha256']:raise ValueError('공식 Export 해시 불일치: '+kind)
    (args.cache/(kind+'_ko.json')).write_bytes(data)
(args.cache/'export-sources.json').write_text(json.dumps(manifest['exportSources'],indent=2),encoding='utf-8')
print('고정 위키 원본과 공식 Export의 개정·해시 검증 완료')
