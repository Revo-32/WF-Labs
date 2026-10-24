"""Pin modular firearm tables without executing Wiki Lua code."""
import json
from ranged_source_lib import ROOT,PAGES,SOURCES,CATALOG
from wiki_tables import parse
module=parse(PAGES['Module:Modular/data'])
rows=[]
for category,key in [('primary','KitgunPrimary'),('secondary','KitgunSecondary')]:
 parts=module[key]
 assert len(parts['Chamber'])==6 and len(parts['Grip'])==5 and len(parts['Loader'])==20
 for name,chamber in parts['Chamber'].items():
  display=name+(' (Primary)' if category=='primary' else ' (Secondary)')
  template=next(w for w in CATALOG['weapons'] if w['englishName']==display)
  rows.append(dict(weaponId=template['id'],chamber=name,category=category,template=template,stats=chamber,grips=parts['Grip'],loaders=parts['Loader'],source=SOURCES[name]))
result=dict(version='wiki-kitgun-'+str(SOURCES['Module:Modular/data']['revision']),sources=[SOURCES[n] for n in ['Module:Modular/data','Module:Modular','Pax Charge','Pax Soar','Pax Bolt','Pax Seeker']],weapons=rows)
(ROOT/'data/kitgun-parts.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Kitgun chambers:',len(rows),'assemblies:',sum(len(r['grips'])*len(r['loaders']) for r in rows))
