"""The Wiki distinguishes infinite BODY penetration from geometry penetration."""
import json
from ranged_source_lib import ROOT,WEAPONS,SOURCES,PAGES
groups={
 (0,):['Alternox','Alternox Prime','Arca Plasmor','Tenet Arca Plasmor','Coda Bassocyst','Exergis','Ignis','Ignis Wraith','Phantasma','Phantasma Prime','Purgator 1','Steflos'],
 (0,1):['Felarx','Lanka'],
 (1,):['Fulmin','Fulmin Prime','Quellor','Nunchasa','Athodai','Athodai Prime','Lex','Lex Prime'],
 (1,2):['Nataruk'],
 (2,):['Cinta','Dread','Paris','Paris Prime','Mk1-Paris','Cyanex','Ballistica','Rakta Ballistica','Ballistica Prime']}
rows=[]
for attacks,names in groups.items():
 for name in names:
  if name not in WEAPONS:continue
  w=WEAPONS[name]
  assert all(i<len(w['raw']['Attacks']) for i in attacks),(name,attacks)
  rows.append(dict(weaponId=w['id'],attacks=list(attacks),source=SOURCES['Punch Through']))
(ROOT/'data/ranged-penetration.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(len(rows),'infinite-body weapon records')
