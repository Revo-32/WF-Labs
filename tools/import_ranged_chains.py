"""Pinned wiki chain rules; IDs resolved only during import."""
import json
from ranged_source_lib import ROOT, WEAPONS, SOURCES, CATALOG
WEAPONS.update({w["englishName"]:w for w in CATALOG["weapons"] if w["category"]=="modular"})
rows=[]
def add(name,attack,count,radius,decay,fan=False,source=None,chance=1,void=False,ricochet=False,impactRadius=0,autoBeams=1,excludeRagdoll=False):
 rows.append(dict(weaponId=WEAPONS[name]['id'],attack=attack,count=count,radius=radius,decay=decay,fan=fan,chance=chance,voidPrevents=void,ricochet=ricochet,impactRadius=impactRadius,autoBeams=autoBeams,excludeRagdoll=excludeRagdoll,source=SOURCES[source or name]))
add('Gaze (Secondary)',0,2,5,.2,source='Gaze')
add('Vermisplicer (Primary)',0,3,6,.7,fan=True,source='Vermisplicer')
add('Grimoire',2,2,6,1,ricochet=True)
add('Amprex',0,3,10,.5)
add('Atomos',0,3,7,.75)
add('Kuva Nukor',0,2,9,.5,True)
add('Tenet Cycron',0,2,7,.6,True)
add('Torid',2,5,7,.75,source='Torid Incarnon Genesis',impactRadius=2.3)
for name in ['Boar','Boar Prime']:add(name,1,2,10,.8,source='Boar Incarnon Genesis',autoBeams=3)
add('Dual Toxocyst',1,1,5,1,source='Dual Toxocyst Incarnon Genesis',ricochet=True,excludeRagdoll=True)
for name in ['Sicarus','Sicarus Prime']:add(name,1,1,6,1,source='Sicarus Incarnon Genesis',ricochet=True)
for name in ['Angstrum','Prisma Angstrum']:add(name,2,1,20,1,source='Angstrum Incarnon Genesis',ricochet=True)
for name,radius in [('Lato',10),('Lato Vandal',10),('Lato Prime',12)]:add(name,1,1,radius,1,source='Lato Incarnon Genesis',void=True,ricochet=True)
for name,radius,chance in [('Bronco',6,.5),('Bronco Prime',8,1)]:add(name,1,1,radius,1,source='Bronco Incarnon Genesis',chance=chance,void=True,ricochet=True)
(ROOT/'data/ranged-chains.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Verified chain rules:',len(rows))
