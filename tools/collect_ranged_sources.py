"""Pin public Wiki revisions for the ranged mechanics audit. Existing batches are preserved."""
import json,hashlib,pathlib,urllib.request,urllib.parse,concurrent.futures,time
ROOT=pathlib.Path(__file__).resolve().parents[1]
DEST=ROOT/'data/ranged-sources';DEST.mkdir(exist_ok=True)
catalog=json.loads((ROOT/'data/reference-catalog.json').read_text(encoding='utf-8'))
mechanics=['Fire Rate','Continuous Weapon','Reload','Damage/Calculation','Condition Overload (Mechanic)','Multishot','Damage/Slash Damage','Damage/Heat Damage','Damage/Electricity Damage','Damage/Toxin Damage','Damage/Gas Damage','Damage/Blast Damage','Status Effect','Critical Hit','Incarnon','Module:Incarnon/data','Module:Weapons','Module:Weapons/characteristics','Galvanized Aptitude','Galvanized Shot','Galvanized Savvy','Hunter Munitions','Internal Bleeding','Hemorrhage']
families={w['raw'].get('Family',w['englishName']) for w in catalog['weapons'] if w['category'] in ['primary','secondary']}
incarnons={w['raw'].get('Family',w['englishName'])+' Incarnon Genesis' for w in catalog['weapons'] if w['category'] in ['primary','secondary'] and w['raw'].get('IncarnonImage')}
mechanics+=['Trigger Type','Sniper Rifle','Kitgun','Damage/True Damage','Damage/Viral Damage','Damage/Corrosive Damage','Damage/Magnetic Damage','Damage/Puncture Damage','Damage/Cold Damage','Damage/Impact Damage','Damage/Void Damage','Damage/Tau Damage','Enemy Body Parts','Damage','Progenitor','Kuva (Variant)','Tenet','Coda']
titles=sorted(set(mechanics+list(incarnons)+[w['englishName'] for w in catalog['weapons'] if w['category'] in ['primary','secondary']]+[m['englishName'] for m in catalog['mods'] if m['category'] in ['Rifle','Shotgun','Pistol'] or m['category'] in families]+[a['englishName'] for a in catalog['arcanes'] if a['englishName'].startswith(('Primary ','Secondary ','Cascadia ','Conjunction ','Shotgun ','Pax '))]))
batches=[titles[i:i+12] for i in range(0,len(titles),12)]
def fetch(batch):
 key=hashlib.sha256('|'.join(batch).encode()).hexdigest()[:16];path=DEST/(key+'.json')
 if not path.exists():
  query=urllib.parse.urlencode(dict(action='query',prop='revisions',titles='|'.join(batch),rvprop='ids|timestamp|content',rvslots='main',format='json',formatversion=2,redirects=1))
  for attempt in range(4):
   try:
    req=urllib.request.Request('https://wiki.warframe.com/api.php?'+query,headers={'User-Agent':'WF-Labs/0.9 offline mechanics research'})
    body=urllib.request.urlopen(req,timeout=60).read();data=json.loads(body)
    if 'query' not in data:raise ValueError(str(data)[:150])
    path.write_bytes(body);break
   except Exception:
    if attempt==3:raise
    time.sleep(2*(attempt+1))
 data=json.loads(path.read_bytes());records=[]
 for page in data['query']['pages']:
  if 'revisions' not in page:records.append(dict(title=page['title'],missing=True,batch=path.name));continue
  rev=page['revisions'][0];text=rev['slots']['main']['content']
  records.append(dict(title=page['title'],revision=rev['revid'],timestamp=rev['timestamp'],sha256=hashlib.sha256(text.encode()).hexdigest(),url='https://wiki.warframe.com/w/'+urllib.parse.quote(page['title'].replace(' ','_'))+'?oldid='+str(rev['revid']),batch=path.name))
 return records
if __name__=='__main__':
 previous=json.loads((DEST/'manifest.json').read_bytes()) if (DEST/'manifest.json').exists() else {}
 records=[]
 with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
  for rows in pool.map(fetch,batches):records.extend(rows)
 merged={r['title']:r for r in previous.get('sources',[])}
 for record in records:merged[record['title']]=record
 records=sorted(merged.values(),key=lambda r:r['title'])
 titles=sorted(set(titles)|set(previous.get('requested',[])))
 (DEST/'manifest.json').write_text(json.dumps(dict(requested=titles,sources=records),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print(json.dumps(dict(requested=len(titles),pages=len(records),missing=[r['title'] for r in records if r.get('missing')]),ensure_ascii=False))
