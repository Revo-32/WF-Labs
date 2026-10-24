"""Pinned kill-meter rules. Weapon gauges are distinct from magazine ammunition."""
import json
from ranged_source_lib import ROOT,WEAPONS,SOURCES
rows=[]
def add(name,primary,alternate,capacity,required,scale=False,noAmmo=True,locks=1,block=0):
 rows.append(dict(weaponId=WEAPONS[name]['id'],primary=primary,alternate=alternate,capacity=capacity,required=required,damagePerCharge=scale,noAmmo=noAmmo,lockTargets=locks,blockSeconds=block,source=SOURCES[name]))
add('Battacor',[0],[1,2],3,1,True,False)
add('Higasa',[0],[1],10,10,block=2)
add('Trumna',[0,1],[2,3],5,5)
add('Trumna Prime',[0,1],[2,3],5,5)
add('Sepulcrum',[0,1],[2,3],5,5,locks=5)
(ROOT/'data/ranged-alternates.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Alternate gauges:',len(rows))
