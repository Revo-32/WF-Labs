"""Pinned Wiki manual-detonation limits. One throw/detonation batch is modelled."""
import json
from ranged_source_lib import ROOT, WEAPONS, SOURCES

rows=[]
for name,limit,direct,modes,overflow in [
    ('Penta',5,0,[0],True),('Carmine Penta',5,0,[0],True),('Secura Penta',5,0,[0],True),
    ('Aegrit',4,0,[0],False),('Castanas',6,-1,[0,1],False),
    ('Sancti Castanas',6,-1,[0,1],False),('Talons',8,-1,[0,1],False),
]:
    rows.append(dict(weaponId=WEAPONS[name]['id'],limit=limit,directAttack=direct,fireModes=modes,allowBatchOverflow=overflow,source=SOURCES[name]))
(ROOT/'data/ranged-deployments.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(len(rows),'manual deployment rules')
