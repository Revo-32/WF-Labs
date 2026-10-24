"""Normalize explicit event descriptions; do not infer missing numbers from the perk name."""
import re

FIELDS={'Accuracy':'Accuracy','Weapon Recoil':'Recoil','Fire Rate':'FireRate','Multishot':'Multishot','Ammo Efficiency':'AmmoEfficiency','Toxin':'PassiveToxin','Punch Through':'PunchThrough','Critical Chance':'CriticalChance','Headshot Damage':'HeadshotMultiplier','Damage':'Damage'}
EVENTS={'Hit':'Hit','Headshot':'Headshot','Puncture Status Effect':'Puncture','Status Effect':'Status','Fire':'Fire','Kill':'Kill','Equip':'Equip','Punch Through Hit':'PunchThrough','Ability Cast':'AbilityCast','Shield Break':'PlayerShieldBreak','Shield/Overguard break':'PlayerDefenseBreak','Equip From Primary':'EquipFromPrimary','50 Energy Spent':'Energy50','ElectricTargetHit':'ElectricTargetHit'}

def parse(line,raw,name):
 if line=='On Slash Status Kill: +100 Overshield':
  return dict(event='SlashStatusKill',effects={'RestoreShields':100})
 if line=='With {{Weapon|Hate}} and {{Weapon|Despair}} Equipped: Hits increase Base Damage by +10 and Fire Rate by +10%, stacks up to 5x. Resets on missed shot':
  return dict(event='Hit',effects={'BaseDamage':10,'FireRate':.1},cap=5,requiresStalkerSet=True,resetOnMiss=True)
 if line=='On Full Burst Hit: +20% Damage, resets on Reload':
  return dict(event='FullBurstHit',effects={'Damage':.2},cap=4,resetOnReload=True)
 m=re.fullmatch(r'On Consecutive Weakpoint Hits: Increase Base Damage by \+(\d+)\. Stacks up to (\d+)x',line)
 if m:return dict(event='Headshot',effects={'BaseDamage':float(m[1])},cap=int(m[2]),resetOnReload=True,resetOnBodyshot=True,resetOnBlast=True)
 m=re.fullmatch(r'On Burst Headshots: Increase Base Damage by \+(\d+)\. Stacks up to (\d+)x',line)
 if m:return dict(event='FullBurstHeadshot',effects={'BaseDamage':float(m[1])},cap=int(m[2]),resetOnReload=True,normalOnly=True)
 m=re.fullmatch(r'On ('+'|'.join(EVENTS)+r'): (.+)',line)
 if not m:return None
 event=EVENTS[m[1]];effect=m[2];chance=1;duration=0;cap=1
 probability=re.match(r'(\d+)% chance for (.+)',effect)
 if probability:chance=float(probability[1])/100;effect=probability[2]
 stacks=re.search(r'\. Stacks up to (\d+)x?$',effect)
 if stacks:cap=int(stacks[1]);effect=effect[:stacks.start()]
 timer=re.search(r' for (\d+(?:\.\d+)?)(?:s| seconds)$',effect)
 if timer:duration=float(timer[1]);effect=effect[:timer.start()]
 values={}
 for part in effect.split(' and '):
  base=re.fullmatch(r'Increase Base (Damage|Critical Chance|Critical Damage Multiplier) by \+(\d+(?:\.\d+)?)([%x]?)',part)
  if base:
   values[{'Damage':'BaseDamage','Critical Chance':'BaseCriticalChance','Critical Damage Multiplier':'BaseCriticalDamage'}[base[1]]]=float(base[2])/(100 if base[3]=='%' else 1);continue
  number=re.fullmatch(r'([+-]\d+(?:\.\d+)?)(%?) (.+)',part)
  if number and number[3] in FIELDS:
   values[FIELDS[number[3]]]=float(number[1])/(100 if number[2] else 1);continue
  multiplier=re.fullmatch(r'x(\d+(?:\.\d+)?) Critical Damage',part)
  if multiplier:values['CriticalDamageMultiplier']=float(multiplier[1]);continue
  if part=='Instant Reload':values['InstantReload']=1;continue
  return None
 normal='does not affect the incarnon form' in raw.lower() or 'does not apply to incarnon form' in raw.lower()
 if name=='Headcracker':chance=.5
 return dict(event=event,effects=values,duration=duration,cap=cap,chance=chance,normalOnly=normal,decayOne='one stack is lost' in raw,resetOnReload='lost on reload' in raw or 'Resets after reloading' in raw)
