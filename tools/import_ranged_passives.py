"""Weapon mechanics keyed by original internal IDs, with pinned source revisions."""
import json,re
from ranged_source_lib import ROOT,WEAPONS,SOURCES

kinds={
 'Onos':'Onos','Alternox':'Alternox','Arca Scisco':'Scisco','Magnus Prime':'Magnus','Akmagnus Prime':'Magnus','Athodai':'Athodai',
 'Tenet Plinx':'MagazineDump','Grimoire':'Grimoire','Ocucor':'Ocucor',
 'Cedo':'Cedo','Cedo Prime':'Cedo','AX-52':'AX52',
 'Perigale':'Perigale','Perigale Prime':'Perigale','Athodai Prime':'Athodai','Knell':'Knell','Knell Prime':'Knell','Dual Toxocyst':'Toxocyst',
 'Velox':'Velox','Velox Prime':'Velox','Tenet Spirex':'Spirex',
 'Acceltra':'SprintReload','Acceltra Prime':'SprintReload','Akarius':'SprintReload','Akarius Prime':'SprintReload',
 'Nukor':'Microwave','Kuva Nukor':'Microwave',
}
holster={name:.33 for name in ['Afentis','Afentis Prime','Ferrox','Tenet Ferrox','Javlok','Scourge','Scourge Prime']}
holster.update({'Tenet Envoy':.1,'Tenet Diplos':.1})
rows=[]
for name,w in WEAPONS.items():
 raw=w['raw'];zooms=[]
 for z in raw.get('Zoom',[]):
  m=re.search(r'\(\+(\d+(?:\.\d+)?)% (Headshot Damage|Critical Multiplier|Critical Chance)\)',z)
  if m:zooms.append(dict(name=z,kind={'Headshot Damage':'HeadshotMultiplier','Critical Multiplier':'CriticalDamage','Critical Chance':'FlatCriticalChance' if name=='Lanka' else 'CriticalChance'}[m[2]],value=float(m[1])/100))
 no_head=[i for i,a in enumerate(raw.get('Attacks',[])) if a.get('ExtraHeadshotDmg',0)<=-2]
 if name=='Alternox':no_head=[0,1,2,3]
 if name in ['Zarr','Kuva Zarr','Kuva Bramma','Kulstar']:no_head=[1,2,3]
 if name=='Nataruk':no_head=[1,2] # Charged and perfect shots, despite missing field on charged row.
 if name in holster or name in kinds or zooms or no_head or raw.get('SniperComboMin') or name=='Sonicor':
  rows.append(dict(weaponId=w['id'],kind=kinds.get(name,''),holsterReload=holster.get(name,0),falloffSpeedScaling=name!='Sonicor',zooms=zooms,noHeadshotBonus=no_head,comboMinimum=raw.get('SniperComboMin',0),comboDuration=raw.get('SniperComboReset',0),source=SOURCES.get(name),sprintReload=.25 if name.startswith('Akarius') else .125))
(ROOT/'data/ranged-passives.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Passive/zoom records:',len(rows))
