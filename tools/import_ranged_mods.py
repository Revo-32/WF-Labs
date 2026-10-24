"""Only normalize complete, unconditional numeric descriptions. Keep rejected rows visible."""
import json,re,pathlib,hashlib
from ranged_source_lib import PAGES
root=pathlib.Path(__file__).resolve().parents[1]
source=root/'data/reference-catalog.json'
catalog=json.loads(source.read_text(encoding='utf-8'))
labels={'Damage':'Damage','Multishot':'Multishot','Critical Chance':'CriticalChance','Critical Damage':'CriticalDamage','Status Chance':'StatusChance','Fire Rate':'FireRate','Reload Speed':'ReloadSpeed','Magazine Capacity':'Magazine','Impact':'Impact','Puncture':'Puncture','Slash':'Slash','Heat':'Heat','Cold':'Cold','Electricity':'Electricity','Toxin':'Toxin','Radiation':'Radiation','Magnetic':'Magnetic','Status Duration':'StatusDuration','Status Damage':'StatusDamage','Projectile Speed':'ProjectileSpeed','Accuracy':'Accuracy','Weapon Recoil':'Recoil','Ammo Maximum':'AmmoMaximum','Zoom':'Zoom','Sprint Speed':'SprintSpeed','Dodge Speed':'DodgeSpeed','Revive Speed':'ReviveSpeed','Punch Through':'PunchThrough'}
special={
 'Arrow Mutation':{'AmmoMutation':.5},'Rifle Ammo Mutation':{'AmmoMutation':.5},'Primed Rifle Ammo Mutation':{'AmmoMutation':.92},'Sniper Ammo Mutation':{'AmmoMutation':.5},'Primed Sniper Ammo Mutation':{'AmmoMutation':.92},
 'Primed Chamber':{'FirstMagazineDamage':1},'Charged Chamber':{'FirstMagazineDamage':.4},'Target Acquired':{'HeadshotMultiplier':.6},'Harkonar Scope':{'SniperComboDuration':12},
 'Aero Agility':{'GlideReloadSpeed':1},'Soaring Strike':{'AirborneFireRate':.5},
 'Galvanized Aptitude':{'StatusChance':.8,'ConditionOverload':.4,'KillStackLimit':2,'KillDuration':20},
 'Galvanized Savvy':{'StatusChance':.8,'ConditionOverload':.4,'KillStackLimit':2,'KillDuration':20},
 'Galvanized Shot':{'StatusChance':.8,'ConditionOverload':.4,'KillStackLimit':3,'KillDuration':14},
 'Galvanized Chamber':{'Multishot':.8,'KillMultishot':.3,'KillStackLimit':5,'KillDuration':20},
 'Galvanized Diffusion':{'Multishot':1.1,'KillMultishot':.3,'KillStackLimit':4,'KillDuration':20},
 'Galvanized Hell':{'Multishot':1.1,'KillMultishot':.3,'KillStackLimit':4,'KillDuration':20},
 'Hunter Munitions':{'CriticalSlashChance':.3},
 'Internal Bleeding':{'ImpactSlashChance':.35},'Hemorrhage':{'ImpactSlashChance':.35},
 'Magnetic Welt':{'ImpactMagneticChance':.35},
 'Argon Scope':{'HeadshotCriticalChance':1.35,'HeadshotDuration':9},
 'Hydraulic Crosshairs':{'HeadshotCriticalChance':1.35,'HeadshotDuration':9},
 'Laser Sight':{'HeadshotCriticalChance':1.2,'HeadshotDuration':9},
 'Galvanized Scope':{'HeadshotCriticalChance':1.2,'HeadshotKillCriticalChance':.4,'HeadshotKillStackLimit':5,'HeadshotDuration':12},
 'Galvanized Crosshairs':{'HeadshotCriticalChance':1.2,'HeadshotKillCriticalChance':.4,'HeadshotKillStackLimit':5,'HeadshotDuration':12},
 'Bladed Rounds':{'KillCriticalDamage':1.2,'KillDuration':9},
 'Sharpened Bullets':{'KillCriticalDamage':.75,'KillDuration':9},
 'Shrapnel Shot':{'KillCriticalDamage':.99,'KillDuration':9},
 'Kill Switch':{'KillReloadSpeed':.5,'KillDuration':3},'Secondary Wind':{'KillReloadSpeed':.5,'KillDuration':4},
 'Pressurized Magazine':{'ReloadFireRate':.9,'ReloadDuration':9},'Repeater Clip':{'ReloadFireRate':1.05,'ReloadDuration':9},
 'Spectral Serration':{'InvisibleDamage':3.3},
 'Semi-Rifle Cannonade':{'Damage':2.4,'PunchThrough':1.5,'LockFireRate':1},
 'Semi-Shotgun Cannonade':{'Damage':2.4,'PunchThrough':1.5,'LockFireRate':1},
 'Semi-Pistol Cannonade':{'Damage':3,'PunchThrough':1.5,'LockFireRate':1},
 'Primary Acuity':{'WeakpointDamage':3.5,'WeakpointCriticalChance':3.5,'LockMultishot':1},
 'Pistol Acuity':{'WeakpointDamage':3.5,'WeakpointCriticalChance':3.5,'LockMultishot':1},
 'Catalyzer Link':{'AbilityStatusChance':.6,'AbilityDuration':9},
 'Embedded Catalyzer':{'AbilityStatusChance':.9,'AbilityDuration':9},'Nano-Applicator':{'AbilityStatusChance':.9,'AbilityDuration':9},
 'Synth Charge':{'FinalShotDamage':2},
 'Firestorm':{'BlastRadius':.24},'Primed Firestorm':{'BlastRadius':.44},
 'Fulmination':{'BlastRadius':.24},'Primed Fulmination':{'BlastRadius':.44},
 'Heavy Warhead':{'BlastRadius':1,'ProjectileSpeed':-.5},
 'Sinister Reach':{'BeamRange':12},'Ruinous Extension':{'BeamRange':8},
 'Galvanized Acceleration':{'ProjectileSpeed':.3,'BeamRangePercent':.3,'KillProjectileSpeed':.3,'KillBeamRangePercent':.3,'KillStackLimit':2,'KillDuration':10},
 'Hush':{'Silence':1},'Suppress':{'Silence':1},'Silent Battery':{'Silence':1},
 'Cautious Shot':{'SelfStaggerReductionChance':1},
 'Pistol Ammo Mutation':{'AmmoMutation':.5},'Primed Pistol Ammo Mutation':{'AmmoMutation':.92},
 'Shotgun Ammo Mutation':{'AmmoMutation':.5},'Primed Shotgun Ammo Mutation':{'AmmoMutation':.92},
 'Vigilante Supplies':{'AmmoMutation':.3},
 'Aerial Ace':{'AirborneJumpResetLimit':6},
 'Tactical Reload':{'HolsterReload':.2},'Eject Magazine':{'HolsterReload':.2},'Lock and Load':{'HolsterReload':.2},
 'Agile Aim':{'AimingMovementSpeed':.2},'Snap Shot':{'AimingMovementSpeed':.2},'Spry Sights':{'AimingMovementSpeed':.2},
 'Apex Predator':{'MinimapRevealDuration':6},'Bounty Hunter':{'MinimapRevealDuration':6},'Night Stalker':{'MinimapRevealDuration':6},
 'Aero Periphery':{'GlideZoom':-.5},'Air Recon':{'GlideZoom':-.6},'Broad Eye':{'GlideZoom':-.6},
 'Motus Setup':{'JumpCriticalChance':1,'JumpStatusChance':1,'JumpDuration':4},
 'Proton Jet':{'LatchCriticalChance':1.2,'LatchStatusChance':1.2},
}
special.update({
 'Cannonade':{'CannonadeChance':.3},
 'Neutralizing Justice':{'NeutralizingChance':.9,'SyndicateJustice':1},
 'Spontaneous Singularity':{'SpontaneousDamage':.9,'SpontaneousChance':.18},
 'Adhesive Blast':{'AdhesiveChance':1},
 'Napalm Grenades':{'PentaNapalm':1,'FlatStatusChance':.3},
 'Voltage Sequence':{'VoltageTrapDuration':8,'SyndicateSequence':1},
 'Bursting Mass':{'BurstingMass':2},
 'Tether Grenades':{'TetherTargets':5,'TetherRadius':9},
 'Mending Shot':{'HealthOrbShotBonus':1.1},
 'Energizing Shot':{'EnergyOrbShotBonus':1.1},
 'Thunderbolt':{'ThunderboltChance':.3},
 'Concealed Explosives':{'ConcealedExplosionChance':.8},
 'Vile Discharge':{'DischargeCapacity':30000},
 'Disarming Purity':{'DisarmChance':.4,'SyndicatePurity':1},
 'Overpressured Rounds':{'BuckshotFalloff':1.2,'BuckshotMagazineRestore':.5},
 'Flux Overdrive':{'StatusChance':1.5,'OscillatingStatusChance':1.008},
 'Higasa Serration':{'Damage':4.5,'HigasaForcedStatus':1},
 'Fomorian Accelerant':{'AdditionalBounces':4,'ProjectileSpeed':.6},
 'Kinetic Ricochet':{'AdditionalBounces':6,'RicochetRange':.3},
 'Sentient Barrage':{'SentientBarrageCritical':3},
 'Amalgam Argonak Metal Auger':{'PunchThrough':3,'ExternalDaggerArmorStrip':6,'AimHighlightRange':100},
 'Amalgam Daikyu Target Acquired':{'HeadshotMultiplier':.75,'ExternalNikanaLifeSteal':.03,'ArrowRecoveryChance':.6},
 'Amalgam Javlok Magazine Warp':{'Magazine':.45,'ExternalShieldBlockCombo':1},
 'Deadly Sequence':{'CriticalChance':2,'SyndicateSequence':1},
 'Entropy Burst':{'FlatStatusChance':.2,'SyndicateEntropy':1},
 'Entropy Spike':{'EntropyExplosionChance':.2,'SyndicateEntropy':1},
 'Eroding Blight':{'Magazine':2,'SyndicateBlight':1},
 'Gilded Truth':{'FireRate':.8,'SyndicateTruth':1},
 'Lasting Purity':{'AimFinalDamage':.6,'SyndicatePurity':1},
 'Scattered Justice':{'Multishot':2,'SyndicateJustice':1},
 'Sequence Burn':{'BeamRange':20,'SyndicateSequence':1},
 'Shattering Justice':{'StatusChance':.9,'SyndicateJustice':1},
 'Stinging Truth':{'FlatMagazine':40,'SyndicateTruth':1},
 'Stockpiled Blight':{'Magazine':2,'SyndicateBlight':1},
 'Toxic Sequence':{'StatusDuration':2,'SyndicateSequence':1},
 'Winds of Purity':{'LifeSteal':.2,'SyndicatePurity':1},
 'Unseen Dread':{'InvisibleCriticalDamage':1.75,'ThreeTargetInvisibilityDuration':5},
 'Necrophagic Vigor':{'HealthReloadCriticalStep':.2},
 'Metamorphic Magazine':{'Magazine':.9,'AmmoMaximum':.9,'PetrifyHits':20,'PetrifyDuration':5},
 'Photon Overcharge':{'CriticalDamage':.9,'ColdKillEnergyOrbChance':.02},
 'Jahu Canticle':{'TomeDefenseStrip':.05},
 'Khra Canticle':{'TomeUniversalOrbChance':.12},
 'Velox Conclusion':{'VeloxStrengthPerHit':.006},
 'Dizzying Rounds':{'StatusChance':2,'FinisherStunRange':8},
 'Wild Frenzy':{'FrenzyRate':80,'FrenzyRefund':1},
 'Volatile Variant':{'VolatileVariant':1,'PunchThrough':3,'VolatileRadius':4,'VolatileStatus':.5},
 'Nightwatch Napalm':{'NapalmTicks':6,'NapalmArea':.9},
 'Critical Mutation':{'CriticalMutationStep':.3},
 'Deadly Maneuvers':{'DodgeHeadshotCriticalMultiplier':4},
 'Sentient Surge':{'TendrilCriticalChance':.6,'TendrilStatusChance':.6,'KillMagazineRefill':.2},
 'Double Tap':{'DoubleTapDamage':.2},
 'Acid Shells':{'KillExplosionCorrosive':450,'KillExplosionHealthBlast':.45,'KillExplosionRadius':10,'KillExplosionExcludeDifficulty':1},
 'Thermagnetic Shells':{'KillExplosionMagnetic':80,'KillExplosionHealthBlast':.4,'KillExplosionRadius':6},
 'Combustion Beam':{'KillExplosionBlast':600,'KillExplosionRadius':18,'KillExplosionDelayRequired':1,'KillExplosionRejectFirstHit':1},
 'Shivering Contagion':{'ColdSpreadChance':1},
 'Spring-Loaded Chamber':{'ReloadFireRate':.75,'ReloadDuration':9},
 'Gun Glide':{'SlidingRecoil':-.2,'SlidingAccuracy':.2},'Overview':{'GlideZoom':-.6},
 'Prototype Shock Coils':{'Electricity':.9,'ExtraElectricBaseChance':.2},
 'Clip Delegation':{'MagazineHitBonus':.15},
 'Double-Barrel Drift':{'SlidingRecoil':-.2,'SlidingAccuracy':.2},
 'Strafing Slide':{'SlidingRecoil':-.2,'SlidingAccuracy':.2},
 'Dreadful Killshot':{'HealthStepDamage':.2,'HealthStepStatusChance':.2},
 'Bhisaj-Bal':{'StatusChance':.9,'HealPerThreeStatusHits':300},
 'Zazvat-Kar':{'AirborneAmmoEfficiency':.75},'Damzav-Vati':{'Viral':2.4},
 'Directed Convergence':{'AimAccuracy':1},'Grinloked':{'AimAccuracy':.6},
 'Focused Acceleration':{'AimProjectileSpeed':.8},'Measured Burst':{'AimFireRate':-.6,'AimDamage':.3},
 'Meticulous Aim':{'HeadshotMultiplier':1.05,'BodyshotDamage':-.45},
 'Spring-Loaded Broadhead':{'DistantFinalDamage':.4,'DistantThreshold':15},
 'Range Advantage':{'IsolatedDamage':3,'IsolationRadius':10},
 'Efficient Beams':{'StatusChance':1.5,'AmmoOnlyOnDamage':1},
 'Static Alacrity':{'ProjectileSpeed':.5,'BlastRadius':-.5},
})
triggers={
 'Exposing Harpoon':[dict(event='HarpoonHit',effects={'CriticalChance':3},duration=15,rankEffects={'CriticalChance':[1,1.3,1.6,2,2.5,3]})],
 'Combat Reload':[dict(event='FiveHeadPellets',effects={'ReloadSpeed':1.2},duration=3)],
 'Xata Invocation':[dict(event='TomeAlternateHit',effects={'EnergyRegen':1},duration=20,cap=10)],
 'Vome Invocation':[dict(event='TomeAlternateHit',effects={'AbilityStrength':.04},duration=20,cap=15)],
 'Netra Invocation':[dict(event='TomeAlternateHit',effects={'AbilityEfficiency':.04},duration=20,cap=15)],
 'Ris Invocation':[dict(event='TomeAlternateHit',effects={'AbilityDuration':.04},duration=20,cap=15)],
 'Guided Ordnance':[dict(event='Damage',effects={'AimAccuracy':.3},duration=9,rankDurations=[1.5,3,4.5,6,7.5,9])],
 'Split Flights':[dict(event='Hit',effects={'Multishot':1,'Accuracy':-1.8},duration=2,cap=4)],
 'Sharpshooter':[dict(event='WeakpointKillWithDot',effects={'RestoreEnergy':15})],
 'Emergent Aftermath':[dict(event='Kill',effects={'ReloadSpeed':.5},duration=3)],
 'Fass Canticle':[dict(event='TomeKill',effects={'ShieldRecharge':.4,'ShieldRechargeDelay':-.28},duration=15,rankEffects={'ShieldRecharge':[.25,.3,.35,.4],'ShieldRechargeDelay':[-.07,-.14,-.21,-.28]})],
 'Lohk Canticle':[dict(event='TomeKill',effects={'FireRate':.3},duration=15)],

 'Critical Precision':[dict(event='BurstHeadshot',effects={'CriticalChance':.1},cap=200,effectLimits={'CriticalChance':5},rankEffects={'CriticalChance':[.025,.04,.055,.07,.085,.1]},burstOnly=True)],
 'Narrow Barrel':[dict(event='Hit',effects={'AimAccuracy':.3},duration=9,rankDurations=[1.5,3,4.5,6,7.5,9])],
 'Targeting Subsystem':[dict(event='Hit',effects={'AimAccuracy':.3},duration=9,rankDurations=[1.5,3,4.5,6,7.5,9])],
 'Reflex Draw':[dict(event='Equip',effects={'Recoil':-.4,'Accuracy':.4},duration=8)],
 'Twitch':[dict(event='Equip',effects={'Recoil':-.4,'Accuracy':.4},duration=8)],
 'Soft Hands':[dict(event='Equip',effects={'Recoil':-.4,'Accuracy':.4},duration=8)],
 'Biotic Rounds':[dict(event='HeadshotKill',effects={'Viral':1.5,'Magnetic':1.5,'StatusChance':1.5},duration=15)],
 'Leaded Gas':[dict(event='Headshot',effects={'Gas':3,'LiveGasBonus':3,'StatusChance':3},duration=6,rankDurations=[1,2,3,4,5,6])],
 'Eximus Advantage':[dict(event='EximusHeadshot',effects={'Damage':6},duration=10)],
 'Precision Strike':[dict(event='DirectHit',effects={'ReloadSpeed':1.5},duration=5,rankEffects={'ReloadSpeed':[.37,.75,1.10,1.5]})],
 'Pain Points':[dict(event='PrimaryHeadshot',effects={'WeakpointDamage':.6},duration=5,cap=10)],
 'Hata-Satya':[dict(event='Hit',effects={'CriticalChance':.012},cap=2500,resetOnReload=True,effectLimits={'CriticalChance':5})],
 'Brain Storm':[dict(event='Headshot',effects={'InfiniteMagazine':1},chance=.4,rankChances=[.1,.2,.3,.4],duration=2,rankDurations=[.5,1,1.5,2])],
 'Skull Shots':[dict(event='Headshot',effects={'InfiniteMagazine':1},chance=.4,rankChances=[.1,.2,.3,.4],duration=2,rankDurations=[.5,1,1.5,2],refreshable=False)],
 'Gorgon Frenzy':[dict(event='Kill',effects={'FireRate':.3},duration=3,rankDurations=[.75,2,2,3])],
}
special.update({name:{} for name in triggers})
weapons={w['englishName']:w for w in catalog['weapons'] if w['category'] in ['primary','secondary']}
def matches_type(w,m):
 kind=m['category'];cls=w['raw'].get('Class');name=m['englishName']
 if name=='Adhesive Blast':return w['englishName'] in ['Ogris','Penta','Secura Penta','Carmine Penta','Tenet Ferrox','Tenet Envoy','Tonkor','Torid','Zarr']
 if w['englishName']==kind or w['raw'].get('Family')==kind:return True
 if kind=='Assault Rifle':return cls in ['Rifle','Speargun']
 if kind=='Sniper':return cls in (['Sniper Rifle','Launcher','Bow'] if 'Ammo Mutation' in name else ['Sniper Rifle'])
 if kind=='Bow':
  if w['category']!='primary' or cls not in ['Bow','Crossbow']:return False
  if name=='Thunderbolt' and w['englishName'] in ['Lenz','Prisma Lenz']:return False
  if name=='Split Flights' and w['englishName'] in ['Artemis Bow','Artemis Bow Prime','Attica','Kuva Bramma','Lenz','Prisma Lenz','Nagantaka','Nagantaka Prime','Nataruk','Nunchasa','Proboscis Cernos','Zhuge','Zhuge Prime']:return False
  return True
 if kind in ['Tome','Thrown']:
  if name=='Concealed Explosives' and w['raw'].get('Family') in ['Castanas','Pox','Talons']:return False
  return cls==kind
 if kind=='{Pistol}':return w['category']=='secondary'
 if kind in ['Rifle (No AoE)','Pistol (No AoE)']:
  return w['category']==('secondary' if kind.startswith('Pistol') else 'primary') and cls!='Shotgun' and 'AOE' not in w['raw'].get('CompatibilityTags',[]) and 'Exalted' not in w['raw'].get('Traits',[])
 return False
rows=[]
for m in catalog['mods']:
 weapon_ids=[w['id'] for w in weapons.values() if matches_type(w,m) and not (set(w['raw'].get('CompatibilityTags',[])) & set(m['raw'].get('IncompatibilityTags',[])))]
 if m['category'] not in ['Rifle','Shotgun','Pistol','Primary','Secondary'] and not weapon_ids:continue
 raw=m['raw'];effects={};reason=[]
 if m['englishName'] in special:effects=special[m['englishName']]
 else:
  for line in m['description'].replace('\r','').replace(' (x2 for Bows)','').split('\n'):
   faction=re.fullmatch(r'x(\d+(?:\.\d+)?) Damage to (Corpus|Grineer|Infested|Orokin|Murmur)',line.strip())
   if faction:effects['Faction'+faction[2]]=float(faction[1])-1;continue
   conversion=re.fullmatch(r'(\d+)% of Damage converted into (Impact|Puncture|Slash)',line.strip())
   if conversion:effects['Convert'+conversion[2]]=float(conversion[1])/100;continue
   match=re.fullmatch(r'([+-]\d+(?:\.\d+)?)(%?) (.+)',line.strip())
   if not match or match[3] not in labels:reason.append('조건부·특수 효과 규칙 연결 필요');break
   effects[labels[match[3]]]=float(match[1])/(100 if match[2] else 1)
 if raw.get('ConclaveOnly') or (re.search(r'Conclave\]\]-exclusive',PAGES.get(m['englishName'],'')) and 'PvE' not in PAGES.get(m['englishName'],'').split('==Stats==')[0]):reason.append('콘클레이브 전용')
 if raw.get('Set')=='Vigilante Set':effects['VigilanteSetChance']=.05
 if not isinstance(raw.get('MaxRank'),int) or not isinstance(raw.get('BaseDrain'),int):reason.append('랭크·코스트 수치 없음')
 rows.append(dict(id=m['id'],name=m['name'],englishName=m['englishName'],category=m['category'],maxRank=raw.get('MaxRank',0),baseDrain=raw.get('BaseDrain',0),polarity=raw.get('Polarity','None'),incompatible=raw.get('Incompatible',[]),effects=effects if not reason else {},limitations=sorted(set(reason)),description=m['description'],source=m['source'],exilus=raw.get('IsExilus',False),rankEffects={'AdhesiveChance':[.17,.34,.51,.68,.85,1]} if m['englishName']=='Adhesive Blast' else {'VoltageTrapDuration':[2,4,6,8]} if m['englishName']=='Voltage Sequence' else {'BurstingMass':[.5,.8,1.1,1.4,1.7,2]} if m['englishName']=='Bursting Mass' else {'TetherTargets':[2,3,3,4,4,5],'TetherRadius':[4,5,6,7,8,9]} if m['englishName']=='Tether Grenades' else {'BuckshotMagazineRestore':[.25,.3,.35,.4,.45,.5]} if m['englishName']=='Overpressured Rounds' else {'InvisibleCriticalDamage':[.5,.75,1,1.25,1.5,1.75]} if m['englishName']=='Unseen Dread' else {'AimHighlightRange':[100]*6} if m['englishName']=='Amalgam Argonak Metal Auger' else {'HealthReloadCriticalStep':[.1,.12,.14,.16,.18,.2]} if m['englishName']=='Necrophagic Vigor' else {'PetrifyHits':[20]*6,'PetrifyDuration':[5]*6} if m['englishName']=='Metamorphic Magazine' else {'TomeDefenseStrip':[.02,.03,.04,.05]} if m['englishName']=='Jahu Canticle' else {'ColdKillEnergyOrbChance':[.02]*6} if m['englishName']=='Photon Overcharge' else {'FrenzyRate':[40,44.44,50,57.14,66.67,80],'FrenzyRefund':[.25,.4,.55,.7,.85,1]} if m['englishName']=='Wild Frenzy' else {'VolatileVariant':[1]*6,'PunchThrough':[3]*6,'VolatileRadius':[4]*6,'VolatileStatus':[.25,.3,.35,.4,.45,.5]} if m['englishName']=='Volatile Variant' else {'AmmoMutation':([.08,.16,.25,.33,.42,.5,.58,.67,.75,.84,.92][:raw.get('MaxRank',0)+1])} if 'Ammo Mutation' in m['englishName'] or m['englishName']=='Arrow Mutation' else None,compatibilityTags=raw.get('CompatibilityTags',[]),incompatibleWeaponIds=[weapons[n]['id'] for n in ['Vectis','Vectis Prime']] if m['englishName']=='Terminal Velocity' else [weapons['Vaykor Hek']['id']] if m['englishName']=='Scattered Justice' else [],weaponIds=weapon_ids,triggers=triggers.get(m['englishName'],[])))
out=dict(sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),rules='ranged-events-2',mods=rows)
(root/'data/ranged-mods.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print({'supported':sum(not m['limitations'] for m in rows),'listed':len(rows)})
