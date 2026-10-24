"""Incarnon form identity and perk definitions; source references remain attached to each weapon."""
import json
from ranged_source_lib import ROOT,WEAPONS,SOURCES
rows=[]
native={
 'Onos':[
  (2,'marksman',"Marksman's Hand",{'Recoil':-.3},''),(2,'rapid-wrath','Rapid Wrath',{'FireRate':.25},''),(2,'swift-deliverance','Swift Deliverance',{'ProjectileSpeed':.5},''),
  (3,'hunters-rearmament',"Hunter's Rearmament",{},'Normal'),(3,'extended-volley','Extended Volley',{'BaseMagazine':10},'Normal'),(3,'rapid-reinforcement','Rapid Reinforcement',{'ReloadSpeed':.3},''),
  (4,'lethal-lance','Lethal Lance',{},''),(4,'efficiency','Incarnon Efficiency',{'IncarnonChargeBonus':.5},''),(4,'elemental-excess','Elemental Excess',{'FlatCriticalChance':-.1,'FlatStatusChance':.2},''),
  (5,'devastation-cascade','Devastation Cascade',{'ChargeBlastCritical':.05},''),(5,'sequential-skullbuster','Sequential Skullbuster',{},''),(5,'impaler-ferocity',"Impaler's Ferocity",{},''),
 ],
 'Phenmor':[
  (2,"void-guidance","Void's Guidance",{'Accuracy':.5,'Recoil':-.5},'Aiming'),(2,'rapid-wrath','Rapid Wrath',{'FireRate':.2},''),(2,'swift-deliverance','Swift Deliverance',{'ProjectileSpeed':.8},''),
  (3,'retribution-vessel',"Retribution's Vessel",{'Magazine':.5},'Normal'),(3,'ready-retaliation','Ready Retaliation',{'ReloadSpeed':1},'EmptyReload6'),(3,'executioner-fortune',"Executioner's Fortune",{'HeadshotKillReloadChance':.2},'Normal'),
  (4,'survivor-edge',"Survivor's Edge",{'FlatCriticalChance':.1,'FlatStatusChance':.1},''),(4,'efficiency','Incarnon Efficiency',{'IncarnonChargeBonus':.5},''),(4,'elemental-excess','Elemental Excess',{'FlatCriticalChance':-.1,'FlatStatusChance':.2},''),
  (5,'devouring','Devouring Attrition',{'NonCriticalAttritionChance':.5,'NonCriticalAttritionBonus':20},''),(5,'spiteful','Spiteful Defilement',{'FlatCriticalDamage':1},'FewerThan3Statuses'),(5,'lingering','Lingering Judgement',{'LingeringHeadshot':.5},''),
 ],
 'Laetum':[
  (2,'marksman',"Marksman's Hand",{'Recoil':-.4},''),(2,'rapid-wrath','Rapid Wrath',{'FireRate':.2},''),(2,'raptor',"Raptor's Chase",{'AimingMovementSpeed':.5},''),
  (3,'lethal-rearmament','Lethal Rearmament',{'HeadshotReloadStacks':.3},''),(3,'awakened','Awakened Readiness',{'HolsterReload':.3},'Normal'),(3,'feather','Feather of Justice',{'AmmoEfficiency':.6},'NormalGlideSlide'),
  (4,'caput','Caput Mortuum',{'HeadshotMultiplier':.5},''),(4,'efficiency','Incarnon Efficiency',{'IncarnonChargeBonus':.5},''),(4,'elemental-excess','Elemental Excess',{'FlatCriticalChance':-.1,'FlatStatusChance':.2},''),
  (5,'devouring','Devouring Attrition',{'NonCriticalAttritionChance':.5,'NonCriticalAttritionBonus':20},''),(5,'reaper',"Reaper's Plenty",{'AmmoEfficiency':.4},'NormalHeadshot6'),(5,'overwhelming','Overwhelming Attrition',{'OverwhelmingAttrition':4},''),
 ],
 'Felarx':[
  (2,'attuned','Attuned Accuracy',{'Accuracy':.4},'Aiming'),(2,'kinetic','Kinetic Baffle',{'Recoil':-.5},'Aiming'),(2,'frictionless','Frictionless Flight',{'ProjectileSpeed':.5},'Aiming'),
  (3,'dual-mode','Dual-Mode Chamber',{'DualModeChamber':1},''),(3,'autoloader','Evolved Autoloader',{'HolsterReload':.5},''),(3,'momentum','Mounting Momentum',{'MountingMomentum':.1},''),
  (4,'brutal','Brutal Edge',{'FlatCriticalChance':.1,'FlatStatusChance':.025},''),(4,'catalyst','Incarnon Catalyst',{'IncarnonChargeBonus':.5},''),(4,'racking','Racking Wrath',{'FlatCriticalChance':-.1,'FlatStatusChance':.05},''),
  (5,'devastating','Devastating Attrition',{'NonCriticalAttritionChance':.5,'NonCriticalAttritionBonus':20},''),(5,'ruptured','Ruptured Plentitude',{'PunchThroughAmmoBuff':.7},''),(5,'agile','Agile Executor',{'AmmoEfficiency':.5},'GlideSlide'),
 ],
}
for i,name in enumerate(['Braton','Mk1-Braton','Braton Vandal','Braton Prime']):
 native[name]=[
  (2,'daring-reverie','Daring Reverie',{'BaseDamage':[24,28,12,4][i],'ChannelBaseDamage':[30,22,34,38][i],'ChannelAmmoEfficiency':.5},''),
  (2,'munitions-grit','Munitions Grit',{'BaseDamage':[14,20,8,2][i],'ExtraMultishotDamage':[.6,.48,.58,.54][i],'Multishot':.2},''),
  (3,'mercenary-chamber','Mercenary Chamber',{'BaseAmmoCapacity':[675,600,750,1125][i]},'Normal'),
  (3,'void-guidance',"Void's Guidance",{'Accuracy':.6,'Recoil':-.6},''),
  (3,'gunsmoke-pickup','Gunsmoke Pick Up',{'PunchThroughAmmoRestoreChance':.2,'PunchThroughAmmoRestore':.1},'Normal'),
  (4,'critical-parallel','Critical Parallel',{'BaseCriticalChance':[.16,.18,.16,.18][i],'BaseCriticalDamage':[.4,.5,.4,.2][i]},''),
  (4,'prelude-of-might','Prelude of Might',{'LowCriticalBaseDamage':[3.4,3.3,3,3][i]},''),
  (4,'survivor-edge',"Survivor's Edge",{'BaseCriticalChance':[.1,.12,.1,.1][i],'BaseStatusChance':[.12,.12,.12,.14][i]},''),
 ]
# Consume only fully parsed entries; native/manual definitions keep precedence.
audit=json.loads((ROOT/'data/ranged-evolution-audit.json').read_bytes())
manual=set(native)
for p in audit:
 if p.get('error') or p['unhandled'] or p['weapon'] in manual:continue
 # Stug's speed bonus explicitly excludes bouncing blobs and needs component state.
 # Swift Deliverance's bounce exclusion is now handled per component in RangedIncarnons.Apply.
 import re
 key=re.sub(r'[^a-z0-9]+','-',p['name'].lower()).strip('-')
 effect=p['effects'];condition='Normal' if effect and set(effect)<=set(['BaseMagazine','BaseAmmoCapacity','BaseAmmoAdd']) else ''
 native.setdefault(p['weapon'],[]).append((p['stage'],key,p['name'],effect,condition))
for name,w in WEAPONS.items():
 overrides={'Miter':{2:20},'Stug':{2:120},'Onos':{1:350,2:350,3:350}}.get(name,{})
 forms=[i for i,a in enumerate(w['raw'].get('Attacks',[])) if a.get('IncarnonCharges') or i in overrides]
 if not forms:continue
 source=SOURCES.get(w['raw'].get('Family','')+' Incarnon Genesis',SOURCES.get(name))
 perks=[dict(id=w['id']+'#'+key,stage=stage,name=label,effects=effects,condition=condition) for stage,key,label,effects,condition in native.get(name,[])]
 for perk in perks:
  if perk['name']=="Executioner's Fortune":perk['effects']={};perk['triggers']=[dict(event='HeadshotKill',effects={'InstantReload':1},chance=.2,normalOnly=True)]
  if perk['name']=='Gunsmoke Pick Up':perk['effects']={};perk['triggers']=[dict(event='PunchThrough',effects={'ReserveFraction':.1},chance=.2,normalOnly=True)]
  if name=='Onos':
   manual_triggers={
    "Hunter's Rearmament":[dict(event='Kill',effects={'ReservePickup':10},chance=.1,normalOnly=True)],
    'Lethal Lance':[dict(event='Kill',effects={'PunchThrough':2.5},duration=20)],
    'Sequential Skullbuster':[dict(event='Headshot',effects={'HeadshotMultiplier':.3},cap=4,normalOnly=True,resetOnBodyshot=True)],
    "Impaler's Ferocity":[dict(event='PunchThrough',effects={'Damage':2},duration=10)],
   }
   if perk['name'] in manual_triggers:perk['triggers']=manual_triggers[perk['name']]
  record=next((p for p in audit if p.get('weaponId')==w['id'] and p.get('name')==perk['name']),None)
  if record and record.get('triggers') and not record.get('unhandled'):perk['triggers']=record['triggers']
 rows.append(dict(weaponId=w['id'],chargeOnBody=name in ['Torid','Angstrum','Prisma Angstrum'],forms=forms,chargeGain={'Miter':5,'Stug':4}.get(name,w['raw'].get('IncarnonChargeGain',0)),chargeCapacity=overrides,source=source,perks=perks,spoolStep=-.008 if name=="Phenmor" else 0,spoolFloor=.6 if name=="Phenmor" else 0))
(ROOT/'data/ranged-incarnons.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Incarnon weapons:',len(rows),'native perks:',sum(len(x['perks']) for x in rows))
