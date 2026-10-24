"""Persistent fields with explicit unknown first-pulse phase."""
import json
from ranged_source_lib import ROOT,WEAPONS,SOURCES
rows=[]
for name,parents,child,spawn,duration,interval,explosion,single,rate,stats in [('Mutalist Cernos',[0,1],2,.25,10,1,-1,False,False,False),('Alternox',[1],2,0,10,1,3,True,False,False),('Alternox Prime',[1],2,0,10,1,3,True,False,False),('Ferrox',[2],3,0,30,2,-1,True,True,True),('Tenet Ferrox',[2],3,0,30,2,-1,True,True,True)]:
 rows.append(dict(weaponId=WEAPONS[name]['id'],parents=parents,child=child,spawnDelay=spawn,duration=duration,interval=interval,explosion=explosion,single=single,fireRateScales=rate,unmoddedCriticalStatus=stats,source=SOURCES[name],repeatAfterReload=name=="Tenet Ferrox",blockedByDefenses=name=="Tenet Ferrox"))
for name in ['Pox','Coda Pox']:
 rows.append(dict(weaponId=WEAPONS[name]['id'],parents=[0],child=1,spawnDelay=0,duration=5,interval=1,explosion=-1,single=False,fireRateScales=False,unmoddedCriticalStatus=False,source=SOURCES[name],requireDuration=True))
(ROOT/'data/ranged-fields.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
