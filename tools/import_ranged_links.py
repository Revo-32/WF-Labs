"""Normalize verified impact/area relationships; timing is never inferred from table adjacency."""
import json
from ranged_source_lib import ROOT, WEAPONS, SOURCES, CATALOG

# Indices refer to the pinned module, zero based. Entries below were checked against
# the weapon Notes/Characteristics, not inferred from similarly named attacks.
WEAPONS.update({w["englishName"]:w for w in CATALOG["weapons"] if w["category"]=="modular"})
WEAPONS["Gaze (Primary)"]["raw"]["Attacks"][1]["ShotType"]="AoE"
immediate={
 'Angstrum':[(0,1)],'Prisma Angstrum':[(0,1)],
 'Enkaus':[(1,2)],'Atomos':[(1,2)],'EFV-8 Mars':[(1,2)],
 'Strun':[(1,2)],'Strun Prime':[(1,2)],'Strun Wraith':[(1,2)],'Mk1-Strun':[(1,2)],
 'Catabolyst':[(1,2),(3,4)],'Coda Catabolyst':[(1,2),(3,4)],
 'Gaze (Primary)':[(0,1)],
 'Phantasma':[(1,2),(3,4)],'Phantasma Prime':[(1,2),(3,4)],
 'Sporelacer (Primary)':[(0,1)],'Sporelacer (Secondary)':[(0,1),(2,3)],
 'Tombfinger (Secondary)':[(0,1)],
 'Tombfinger (Primary)':[(0,1),(3,2)],
 'Onos':[(2,3)],
 'Zarr':[(0,1),(2,3)],'Kuva Zarr':[(0,1),(2,3)],'Kuva Bramma':[(0,1),(2,3)],'Kulstar':[(0,1),(2,3)],
 'Miter':[(2,3)],'Acceltra':[(0,1)],'Acceltra Prime':[(0,1)],'Aeolak':[(1,2)],
 'Astilla':[(0,1)],'Astilla Prime':[(0,1)],'Basmu':[(0,1)],
 'Bubonico':[(1,2)],'Coda Bubonico':[(1,2)],'Kuva Chakkhurr':[(0,1)],
 'Kuva Ogris':[(0,1)],'Ogris':[(0,1)],'Kuva Tonkor':[(0,1)],'Tonkor':[(0,1)],
 'Akarius':[(0,1)],'Akarius Prime':[(0,1)],'Kuva Seer':[(0,1)],
 'Epitaph':[(1,2)],'Epitaph Prime':[(1,2)],'Tenet Spirex':[(0,1)],
 'Tenet Envoy':[(0,1)],'Tenet Tetra':[(1,2)],'Shedu':[(0,1)],
 'Stahlta':[(1,2)],'Cyanex':[(0,1)],'Sonicor':[(0,1)],
 'Staticor':[(0,1),(2,3)],'Evensong':[(1,2)],
 'Braton':[(1,2)],'Mk1-Braton':[(1,2)],'Braton Prime':[(1,2)],'Braton Vandal':[(1,2)],
 'Burston':[(1,2)],'Burston Prime':[(1,2)],'Laetum':[(1,2)],
 'Zylok':[(1,2)],'Zylok Prime':[(1,2)],'Trumna':[(0,1),(2,3)],'Trumna Prime':[(0,1),(2,3)],
 'Opticor':[(0,1),(2,3)],'Opticor Vandal':[(0,1),(2,3)],
 'Ferrox':[(0,1)],'Tenet Ferrox':[(0,1)],'Ambassador':[(1,2)],
 'Battacor':[(1,2)], # Module parent=1 contradicts the secondary-fire description.
 'Panthera Prime':[(0,1)],'Javlok':[(0,1),(2,3)],
 'Scourge':[(0,1),(2,3)],'Scourge Prime':[(0,1),(2,3)],
 'Cedo':[(1,2)],'Cedo Prime':[(1,2)],'Latron':[(1,2)],'Latron Wraith':[(1,2)],'Latron Prime':[(1,2)],
 'Grimoire':[(0,1)],'Sepulcrum':[(0,1),(2,3)],
}
rows=[]
WEAPONS['Paracyst']['raw']['Attacks'][2]['ShotType']='DoT'
def add(name,parent,child,delay=0,minimum=0,manual=False,interval=0,ticks=1,pierce=False):
 w=WEAPONS[name];a=w['raw']['Attacks'];assert a[child]['ShotType'] in ['AoE','DoT']
 rows.append(dict(weaponId=w['id'],parent=parent,child=child,delay=delay,minimumDistance=minimum,manualDetonation=manual,tickInterval=interval,tickCount=ticks,singleTickOnPierce=pierce,source=SOURCES[name.split(" (")[0] if " (" in name else name]))
for name,pairs in immediate.items():
 for parent,child in pairs:add(name,parent,child,minimum=7 if name.startswith('Acceltra') else 7.5 if name=='Tonkor' else 0)
for name in ['Penta','Carmine Penta','Secura Penta','Aegrit']:add(name,0,1,manual=True)
for name in ['Gorgon','Gorgon Wraith','Prisma Gorgon']:add(name,1,2,WEAPONS[name]['raw']['Attacks'][2]['ExplosionDelay'])
for name in ['Lenz','Prisma Lenz']:
 add(name,0,1);add(name,0,2,1.3)
for name in ['Zakti','Zakti Prime']:add(name,0,1,1)
for name in ['Kompressa','Kompressa Prime']:
 add(name,0,1,.6);rows[-1]['maximumDelay']=.8
add('Tenet Plinx',1,2,.8)
add('Afentis',0,1,.8)
add('Afentis Prime',0,1,.4)
add('Zhuge Prime',0,1,.6)
add('Despair',1,2,.5)
for name in ['Gammacor','Synoid Gammacor']:add(name,1,2,1.2)
add('Torid',0,1,1,interval=1,ticks=10)
for name in ['Tysis','Coda Tysis']:add(name,0,1,interval=1,ticks=3,pierce=True)
for name in ['Sporothrix','Coda Sporothrix']:add(name,0,1,.9)
add('Paracyst',1,2,interval=1,ticks=5)
rows.append(dict(weaponId=WEAPONS['Komorex']['id'],parent=2,child=1,delay=0,minimumDistance=0,source=SOURCES['Komorex']))
(ROOT/'data/ranged-links.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Verified event links:',len(rows))
