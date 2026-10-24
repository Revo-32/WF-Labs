"""Audited attack-index joins to the pinned Condition Overload (Mechanic) table.

Values are exact damage ratios rather than the rounded percentages in that table.
An unlisted direct attack uses the table's explicitly documented additive default.
Radial attacks default to no CO; listed direct-target hitscan AoE exceptions override it.
"""
from ranged_source_lib import *
WEAPONS.update({w["englishName"]:w for w in CATALOG["weapons"] if w["category"]=="modular"})
rows={}
def put(name,indices,mode='Multiplicative',ratio=1,independent=False,extra=1):
 w=WEAPONS[name]
 for i in indices:
  assert i<len(w['raw']['Attacks']) or (name=='Tombfinger (Primary)' and i==3),(name,i)
  assert (w['id'],i) not in rows,('duplicate CO rule',name,i)
  rows[(w['id'],i)]=dict(weaponId=w['id'],attackIndex=i,mode=mode,ratio=ratio,multishotIndependent=independent,extraProjectileRatio=extra)
for name,ids in {
 'Catchmoon (Primary)':[0],'Catchmoon (Secondary)':[0],'Tombfinger (Primary)':[0,3],'Tombfinger (Secondary)':[0],'Sporelacer (Primary)':[0],
 'Aeolak':[0,1],'Alternox':[0],'Alternox Prime':[0],'Arca Plasmor':[0],'Tenet Arca Plasmor':[0],
 'Basmu':[0],'Battacor':[0],'Bubonico':[0,1],'Buzlok':[0,1],'Cedo':[1],'Cedo Prime':[1],
 'Coda Bassocyst':[0],'Coda Hema':[0],'Mutalist Cernos':[0],'Cinta':[0,1,2],'Dread':[2],
 'Exergis':[0],'Felarx':[0,1],'Fulmin':[1],'Harpak':[1],'Hema':[0],'Javlok':[0],
 'Latron':[1],'Latron Prime':[1],'Miter':[2],'Nataruk':[1,2],'Nunchasa':[1],'Paracyst':[1],
 'Phantasma':[3],'Phantasma Prime':[3],'Quellor':[1],'Scourge':[2],'Scourge Prime':[2],
 'Shedu':[0],'Stahlta':[0,1],'Steflos':[0],'Tenet Envoy':[0],'Torid':[0,1],'Trumna':[2],
 'Akarius Prime':[0],'Aegrit':[0],'Angstrum':[2],'Prisma Angstrum':[2],'Ballistica Prime':[2],
 'Catabolyst':[1,3],'Coda Catabolyst':[1,3],'Cyanex':[0,2],'Epitaph':[0,1],'Kunai':[1],'Mk1-Kunai':[1],
 'Tenet Plinx':[1],'Seer':[0],'Kuva Seer':[0],'Sepulcrum':[0,2],'Tenet Diplos':[1],
 'Tenet Spirex':[0],'Zakti':[0],'Zakti Prime':[0],'Zymos':[3]
}.items():put(name,ids)
for name,i,n,d in [
 ('Acceltra',0,26,35),('Cernos',1,190,380),('Cernos Prime',1,276,552),('Rakta Cernos',1,235,470),
 ('Mutalist Cernos',1,205,410),('Daikyu Prime',1,375,750),('Drakgoon',1,400,700),('Kuva Drakgoon',1,230,460),
 ('Dread',1,168,336),('Evensong',1,325,500),('Lanka',1,200,525),('Miter',1,200,500),
 ('Paris',1,160,320),('Paris Prime',1,180,360),('Mk1-Paris',1,115,230),
 ('Zarr',2,25,15),('Kuva Zarr',2,50,15),('Ballistica',1,25,100),('Ballistica Prime',1,38,76),
 ('Rakta Ballistica',1,75,300),('Kulstar',2,200,75),('Pox',1,50,20)
]:put(name,[i],'Additive',n/d)
for name,i,n,d in [('Ambassador',2,600,800),('Ferrox',1,350,100),('Tenet Ferrox',1,200,60),('Opticor',1,1000,400),('Opticor Vandal',1,400,200),('Trumna',1,82,50)]:put(name,[i],'Additive',n/d,True)
# The table lists Prime base damage as an example; each variant uses its own parent.
for names in [('Braton','Mk1-Braton','Braton Prime','Braton Vandal'),('Burston','Burston Prime'),('Zylok','Zylok Prime')]:
 for name in names:
  a=WEAPONS[name]['raw']['Attacks'];put(name,[2],'Additive',sum(a[1]['Damage'].values())/sum(a[2]['Damage'].values()),True)
for name,ids in {
 'Coda Bassocyst':[1],'Ferrox':[3],'Tenet Ferrox':[3],'Azima':[1],'Stug':[0,1,2,3,4],'Sonicor':[0,1],'Scyotid':[1],'Tysis':[1],'Coda Tysis':[1]}.items():put(name,ids,'None',0)
put('Grimoire',[2,3],'None',0)
put('Tenet Detron',[0],'Additive',1,False,.65)
out=dict(source=SOURCES['Condition Overload (Mechanic)'],rules=list(rows.values()))
(ROOT/'data/ranged-co.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Explicit attack CO rules:',len(rows))
