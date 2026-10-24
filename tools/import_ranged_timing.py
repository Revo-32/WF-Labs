"""Extract only explicit timing statements from pinned source pages; preserve missing fields."""
import json,pathlib,re
from ranged_beam_source import beam_rule
ROOT=pathlib.Path(__file__).resolve().parents[1];folder=ROOT/'data/ranged-sources'
manifest=json.loads((folder/'manifest.json').read_text(encoding='utf-8'))
sources={s['title']:s for s in manifest['sources'] if not s.get('missing')};pages={}
for path in folder.glob('*.json'):
 if path.name=='manifest.json':continue
 for p in json.loads(path.read_bytes()).get('query',{}).get('pages',[]):
  if 'revisions' in p:pages[p['title']]=p['revisions'][0]['slots']['main']['content'].split('==Patch History==')[0]
catalog=json.loads((ROOT/'data/reference-catalog.json').read_text(encoding='utf-8'));rows=[]
shells={'Strun':(.75,.5),'Mk1-Strun':(.75,.5),'Strun Wraith':(.5,.45),'Strun Prime':(.6,.4),'Corinth':(1.2,.22),'Felarx':(1.3,.4),'Zarr':(.75,.5),'Kuva Zarr':(1.05,.75)}
mode_overrides={
 'Phantasma':{3:'Semi-Auto',4:'Semi-Auto'},'Phantasma Prime':{3:'Semi-Auto',4:'Semi-Auto'},
 'Drakgoon':{0:'Semi-Auto'},'Kuva Drakgoon':{0:'Semi-Auto'},'Miter':{0:'Semi-Auto'},'Staticor':{0:'Semi-Auto'},'Ballistica Prime':{0:'Semi-Auto'},
 'Alternox':{0:'Auto'},'Alternox Prime':{0:'Auto'},'Argonak':{1:'Auto'},'Cedo':{1:'Semi-Auto'},'Cedo Prime':{1:'Semi-Auto'},
 'EFV-5 Jupiter':{0:'Auto'},'Fulmin':{1:'Semi-Auto'},'Fulmin Prime':{1:'Semi-Auto'},'Hind':{0:'Burst'},
 'Quellor':{0:'Auto'},'Stradavar':{0:'Auto'},'Stradavar Prime':{0:'Auto'},'Tenet Tetra':{0:'Auto'},
 'Tiberon Prime':{0:'Burst',1:'Semi-Auto',2:'Auto'},'Zenith':{0:'Auto'},'Ballistica':{0:'Burst',1:'Charge'},
 'Cyanex':{0:'Auto'},'Fusilai':{0:'Auto'},'Kuva Kraken':{0:'Burst'},'Pandero':{0:'Semi-Auto'},'Pandero Prime':{0:'Semi-Auto'},
 'Quatz':{0:'Auto'},'Tenet Plinx':{0:'Semi-Auto',1:'Charge'},
}
for w in catalog['weapons']:
 if w['category'] not in ['primary','secondary']:continue
 name=w['englishName'];text=pages.get(name,'');plain=re.sub(r"'''|\[\[|\]\]",'',text)
 spool=re.search(r'[Ff]ire rate starts at (\d+(?:\.\d+)?)%[^\n]*?increases? by (\d+(?:\.\d+)?)% per shot',plain)
 row=dict(id=w['id'],source=sources.get(name),spoolStart=float(spool[1])/100 if spool else None,spoolStep=float(spool[2])/100 if spool else None,shellDelay=shells.get(name,(None,None))[0],shellTime=shells.get(name,(None,None))[1],beamStart=.2,noReload=name in ['Epitaph','Epitaph Prime'],noShotDelay=name in ['Lanka','Vectis','Vectis Prime'],reloadOverride=.6 if name=='Nataruk' else None,triggerOverrides=mode_overrides.get(name,{}))
 row.update(beam_rule(text) or {})
 if name in ['Kohm','Kuva Kohm','Kohmak','Twin Kohmak']:
  row['pelletSpoolMaximum']=12 if 'Kohm' in name and 'Kohmak' not in name else 5
  row['pelletAmmoDivisor']=3
  if name=='Kohm':
   row['spoolStart']=.2;row['spoolStep']=.2;row['supplementarySource']=sources['Kuva Kohm']
 if name=='Convectrix':row['beamStartOverrides']={1:.8}
 if name in ['Opticor','Opticor Vandal']:row['chargeOverrides']={2:w['raw']['Attacks'][0]['ChargeTime']*.4}
 rows.append(row)
result=dict(sources=[sources[n] for n in ['Fire Rate','Reload','Continuous Weapon']],weapons=rows)
(ROOT/'data/ranged-timing.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Timing records:',len(rows),'explicit spool:',sum(r['spoolStart'] is not None for r in rows))
