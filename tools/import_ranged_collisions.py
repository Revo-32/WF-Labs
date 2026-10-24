"""Pinned collision limits. Timing/terrain path is supplied by the encounter, never inferred."""
import json
from ranged_source_lib import ROOT,WEAPONS,SOURCES,CATALOG
WEAPONS.update({w['englishName']:w for w in CATALOG['weapons'] if w['category']=='modular'})
rows=[]
for name,attacks,maximum in [('Ogris',[0],1),('Tenet Envoy',[0],1),('Torid',[0],1),('Tenet Ferrox',[2],1),('Tonkor',[0],1),('Zarr',[0],1),('Sporelacer (Primary)',[0],3),('Sporelacer (Secondary)',[0],1),('Penta',[0],0),('Secura Penta',[0],0),('Carmine Penta',[0],0),('Zymos',[0],1),('Cedo',[1],11),('Cedo Prime',[1],11),('Trumna',[2],7),('Trumna Prime',[2],7),('Latron',[1],6),('Latron Wraith',[1],6),('Latron Prime',[1],6),('Miter',[0,1],6)]:
 source='Latron Incarnon Genesis' if name.startswith('Latron') else name
 rows.append(dict(weaponId=WEAPONS[name]['id'],attacks=attacks,maximumContacts=maximum,source=SOURCES[source.split(" (")[0]]))
for name,attacks,maximum in [('Drakgoon',[0],2),('Drakgoon',[1],3),('Kuva Drakgoon',[0,1],2),('Tetra',[0],1),('Prisma Tetra',[0],1),('Tenet Tetra',[0],1)]:
 rows.append(dict(weaponId=WEAPONS[name]['id'],attacks=attacks,maximumContacts=maximum,source=SOURCES[name.split(" (")[0]]))
(ROOT/'data/ranged-collisions.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

clusters=[]
for name,count in [('Sporelacer (Secondary)',3),('Zarr',6),('Kuva Zarr',3),('Kuva Bramma',3),('Kulstar',3)]:
 clusters.append(dict(weaponId=WEAPONS[name]['id'],parent=0,contact=2,explosion=3,count=count,source=SOURCES[name.split(" (")[0]]))
(ROOT/'data/ranged-clusters.json').write_text(json.dumps(clusters,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
