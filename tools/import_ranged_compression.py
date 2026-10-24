"""Primary Compression exceptions from its pinned per-weapon table."""
import json
from ranged_source_lib import ROOT,WEAPONS,SOURCES
rows=[]
def add(names,indices,radius_index,mode='Multiply',shrink=True,fixed=None,range_mod=True):
 for name in names.split('|'):
  w=WEAPONS[name]
  radius=fixed if fixed is not None else w['raw']['Attacks'][radius_index]['Range']
  for i in indices:rows.append(dict(weaponId=w['id'],attackIndex=i,radius=radius,mode=mode,shrink=shrink,rangeMod=range_mod,source=SOURCES['Primary Compression']))
add('Acceltra|Acceltra Prime',[0,1],1)
add('Aeolak',[1,2],2)
add('Afentis|Afentis Prime',[0,1],1)
add('Alternox|Alternox Prime',[1,2,3],3)
add('Ambassador',[1,2],2,'Add',False)
add('Astilla|Astilla Prime',[0,1],1)
add('Basmu',[0,1],1)
add('Braton|Mk1-Braton|Braton Prime|Braton Vandal',[1,2],2,'Add')
add('Burston|Burston Prime',[1,2],2,'Add')
add('Gorgon|Gorgon Wraith|Prisma Gorgon',[1,2],2)
add('Latron|Latron Prime|Latron Wraith',[1,2],2)
add('Miter',[1],0,fixed=.2)
add('Miter',[2,3],3)
add('Strun|Strun Prime|Strun Wraith|Mk1-Strun',[1,2],2)
add('Bubonico|Coda Bubonico',[1,2],2)
add('Cedo|Cedo Prime',[1,2],2)
add('Kuva Chakkhurr',[0,1],1)
add('Corinth|Corinth Prime',[1,2],2)
add('Tenet Envoy',[0,1],1)
add('Ferrox|Tenet Ferrox',[0,1],1,'Add')
add('Javlok',[0,1],1);add('Javlok',[2,3],3)
add('Evensong',[1,2],2)
add('Lenz|Prisma Lenz',[0,1,2],2)
add('Kuva Bramma',[0,1],1)
add('Ogris|Kuva Ogris',[0,1],1)
add('Opticor|Opticor Vandal',[0,1],1,'Add');add('Opticor|Opticor Vandal',[2,3],3,'Add')
add('Panthera Prime',[0,1],1)
add('Penta|Secura Penta|Carmine Penta',[0,1],1)
add('Phantasma|Phantasma Prime',[1,2],2)
add('Quanta|Quanta Vandal|Tenet Quanta',[1,2,3],2,fixed=.5)
add('Mutalist Quanta',[1,2],2)
add('Scourge|Scourge Prime',[0,1],1);add('Scourge|Scourge Prime',[2,3],3)
add('Shedu',[0,1],1,range_mod=False)
add('Simulor|Synoid Simulor',[0,1,2],2,fixed=5)
add('Sporothrix|Coda Sporothrix',[0,1],1)
add('Tenet Tetra',[1,2],2)
add('Tonkor|Kuva Tonkor',[0,1],1)
add('Torid',[0,1],1,shrink=False)
add('Trumna|Trumna Prime',[0,1],1,'Add',range_mod=False)
add('Zarr|Kuva Zarr',[0,1],1)
add('Zhuge Prime',[0,1],1)
# Explicitly ineffective components, not an inferred default for unmapped explosive attacks.
for names,indices in [('Afentis|Afentis Prime',[2]),('Mutalist Cernos',[0,1,2]),('Kuva Bramma',[2,3]),('Enkaus',[1,2]),('Ignis|Ignis Wraith',[0]),('Komorex',[0,1,2]),('Neutralizer',[0,1]),('Stahlta',[1,2]),('Torid',[2]),('Zarr|Kuva Zarr',[2,3,4]),('Phantasma|Phantasma Prime',[0,3,4])]:
 for name in names.split('|'):
  w=WEAPONS[name]
  for i in indices:
   if i<len(w['raw']['Attacks']):add(name,[i],0,'None',False,0)
(ROOT/'data/ranged-compression.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Compression attack rules:',len(rows))
