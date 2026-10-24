"""Extract pinned Genesis tables and resolve variant columns before applying recognized formulas.

Unrecognized lines remain audit entries, never silently dropped from a selectable perk.
"""
import re,json,html
from ranged_source_lib import PAGES,WEAPONS,SOURCES,ROOT
from ranged_evolution_triggers import parse as parse_trigger

def plain(t):
 t=re.sub(r'\[\[([^\]]+)\]\]',lambda m:m[1].split('|')[-1],t)
 t=re.sub(r'\{\{(?:D|Stat)\|([^}|]+)[^}]*\}\}',r'\1',t)
 return html.unescape(t.replace("'''",'').replace("''",'')).strip()

fields={'Damage':'BaseDamage','Critical Chance':'BaseCriticalChance','Critical Damage Multiplier':'BaseCriticalDamage','Status Chance':'BaseStatusChance','Magazine Capacity':'BaseMagazine','Ammo Capacity':'BaseAmmoCapacity'}
bonuses={'Accuracy':'Accuracy','Weapon Recoil':'Recoil','Recoil':'Recoil','Reload Speed':'ReloadSpeed','Projectile Speed':'ProjectileSpeed','Fire Rate':'FireRate','Punch Through':'PunchThrough','Multishot':'Multishot'}
all_rows=[]
for title,t in PAGES.items():
 if not title.endswith(' Incarnon Genesis'):continue
 heading=re.search(r'={2,4}\s*Evolutions\s*={2,4}',t)
 if not heading:continue
 table=t[heading.end():];header=table.split('EVO1')[0]
 variants=re.findall(r'(?m)^!.*?\{\{Weapon\|([^}|]+)',header)
 if not variants:variants=[title.removesuffix(' Incarnon Genesis')]
 variants=[n for n in variants if n in WEAPONS]
 if not variants:continue
 stage=0
 for row in re.split(r'(?m)^\|-\s*$',table):
  stage_match=re.search(r'\bEVO([1-5])\b',row)
  if stage_match:stage=int(stage_match[1])
  label=re.search(r'(?m)^\|\s*style=[^\n]+?\|\s*(.*?)\s*\[\[File:',row)
  if not label or stage<2:continue
  name=plain(label[1]);remaining=row[label.end():].split('\n',1)[1]
  cells=re.split(r'(?m)^\|[^\n]*$',remaining)
  desc=next((x.strip() for x in cells if x.strip().startswith('*')),'')
  values=re.findall(r'(?m)^\|(?:[^\n]*\|)?\s*([XYZ]\s*=[^\n]+(?:\n[XYZ]\s*=[^\n]+)*)$',row)
  if values and len(values)!=len(variants):
   all_rows.append(dict(source=title,name=name,error='variant column count mismatch',raw=row));continue
  for vi,variant in enumerate(variants):
   variables=dict(re.findall(r'([XYZ])\s*=\s*([+-]?\d+(?:\.\d+)?[%xm]?)',values[vi])) if values else {}
   description=plain(desc)
   for k,v in variables.items():description=re.sub(r'\b'+k+r'([%x]?)',lambda m:v+(m[1] if v[-1].isdigit() else ''),description)
   # Bind nested condition paragraphs before extracting each independent numeric clause.
   description=re.sub(r'On [Ee]quip [Ff]rom Primary:\s*\n\*\*\s*(.+?)\n\*\*\s*(.+?)(?=\n|$)',lambda m:'On Equip From Primary: '+m[1]+'\n*On Equip From Primary: '+m[2],description)
   effects={};unknown=[];triggers=[]
   lone=re.search(r'With No Primary Equipped:\s*\n\*\*\s*Increase Base Damage by \+(\d+)\.?\s*\n\*\*\s*Increase Base Magazine Capacity by \+(\d+)\.?',description)
   if lone:
    effects.update(NoPrimaryBaseDamage=float(lone[1]),NoPrimaryBaseMagazine=float(lone[2]));description=description[:lone.start()]+description[lone.end():]
   if variant=='Despair' and name=="Stalker's Vendetta":
    description=re.sub(r'With \{\{Weapon\|Dread\}\} and \{\{Weapon\|Hate\}\} equipped:\s*\n\*\*\s*Multishot consumes ammo directly from Capacity and increases Damage by \+100%\.\s*\n\*\*\s*\+30% Multishot\.', '',description)
    effects.update(StalkerExtraMultishotDamage=1,StalkerMultishot=.3)

   for line in description.splitlines():
    line=re.sub(r'\s+',' ',line.lstrip('* ').rstrip('. '))
    line=line.replace('Base Status by','Base Status Chance by')
    # PC 37.0.4 confirms additive +12/+9 percentage points; Wiki table's 'to' is a typo.
    if variant in ['Sicarus','Sicarus Prime'] and name=="Survivor's Edge":line=line.replace('Base Status Chance to','Base Status Chance by')
    if variant in ['Despair','Sybaris','Dex Sybaris','Sybaris Prime','Kunai','Mk1-Kunai'] and 'if enemy' not in line:
     line=re.sub(r'^Increase (Damage|Critical Chance|Status Chance) by',r'Increase Base \1 by',line)
    m=re.fullmatch(r'Increase Base (.+?) (by|to) \+?(\d+(?:\.\d+)?)([%xm]?)',line,re.I)
    if m and m[1] in fields:
     key='BaseAmmoAdd' if m[1]=='Ammo Capacity' and m[2]=='by' else fields[m[1]]
     effects[key]=float(m[3])/(100 if m[4]=='%' else 1);continue
    m=re.fullmatch(r'([+-]\d+(?:\.\d+)?)([%m]?) (.+?)',line)
    if m and m[3] in bonuses:
     effects[bonuses[m[3]]]=float(m[1])/(100 if m[2]=='%' else 1);continue
    m=re.fullmatch(r'Increase Accuracy by \+(\d+)%',line)
    if m:effects['Accuracy']=float(m[1])/100;continue
    m=re.fullmatch(r'\+(\d+)% Direct Damage per Status Type affecting the target',line)
    if m:effects['InnateCO']=float(m[1])/100;continue
    m=re.fullmatch(r'-(\d+)% Zoom',line)
    if m:effects['Zoom']=-float(m[1])/100;continue
    m=re.fullmatch(r'Increase Base Status Chance by \+(\d+(?:\.\d+)?)% for Incarnon Form',line)
    if m:effects['FormBaseStatusChance']=float(m[1])/100;continue
    m=re.fullmatch(r'Increase Base Status Chance by \+(\d+(?:\.\d+)?)% per projectile',line)
    if m:effects['BaseStatusChance']=float(m[1])/100;continue
    m=re.fullmatch(r'With Critical Chance [Bb]elow (\d+)%: Increase Base Critical Damage Multiplier by \+(\d+(?:\.\d+)?)x',line)
    if m:effects.update(LowCriticalThreshold=float(m[1])/100,LowCriticalBaseDamage=float(m[2]));continue
    m=re.fullmatch(r'With Overshields: Increase Base Damage by \+(\d+)',line)
    if m:effects['OvershieldsBaseDamage']=float(m[1]);continue
    m=re.fullmatch(r'With Energy Max (?:over|Over|above) 700: Increase Base Critical Damage Multiplier by \+(\d+(?:\.\d+)?)x',line)
    if m:effects['EnergyBaseCriticalDamage']=float(m[1]);continue
    if line=='With Energy Max above 700: +50% Blast Radius':effects['EnergyBlastRadius']=.5;continue
    m=re.fullmatch(r'With Armor Over 450: \+(\d+)(%?) (Punch Through|Multishot)',line)
    if m:effects['Armor'+bonuses[m[3]]]=float(m[1])/(100 if m[2] else 1);continue
    m=re.fullmatch(r'With Channeled Ability [aA]ctive: \+(\d+)(%?) (Punch Through|Multishot|Fire Rate|Ammo Efficiency)',line)
    if m:effects['Channel'+{'Ammo Efficiency':'AmmoEfficiency',**bonuses}[m[3]]]=float(m[1])/(100 if m[2] else 1);continue
    m=re.fullmatch(r'With Channeled Ability [aA]ctive: (?:Punch Through \+(\d+)|\+(\d+) Punch Through)(?: and|,) \+(\d+)% Accuracy',line)
    if m:effects.update(ChannelPunchThrough=float(m[1] or m[2]),ChannelAccuracy=float(m[3])/100);continue
    m=re.fullmatch(r'\+(\d+)% Headshot Damage',line)
    if m:effects['HeadshotMultiplier']=float(m[1])/100;continue
    m=re.fullmatch(r'\+(\d+) Range',line)
    if m:effects['BeamRange']=float(m[1]);continue
    m=re.fullmatch(r'\+(\d+)% Magazine Reloaded(?:/s| per second) (?:when|while) [hH]olstered',line)
    if m:effects['HolsterReload']=float(m[1])/100;continue
    if line=='Reduce the chance an enemy will hear gunfire by 100%':effects['Silence']=1;continue
    m=re.fullmatch(r'Increase Base Status Chance by \+(\d+(?:\.\d+)?)%\.? Effect doubles in Incarnon Form',line)
    if m:
     effects.update(BaseStatusChance=float(m[1])/100,FormBaseStatusChance=.23 if variant=='Sybaris Prime' else float(m[1])/50);continue
    m=re.fullmatch(r'\+(\d+(?:\.\d+)?) Base Multishot',line)
    if m:effects['BaseMultishot']=float(m[1]);continue
    if line=='Remove 20% of enemy Armor per Puncture Status':effects['PunctureArmorStrip']=.2;continue
    if variant=='Torid' and line=='On Reload from Empty: Lingering damage field duration doubles on first shot':
     effects['EmptyReloadDoubleField']=1;continue
    if variant=='Miter' and line=='Multishot consumes ammo directly from Capacity and increases Damage by +20':
     effects.update(ExtraMultishotFlatDamage=20,ExtraMultishotAmmo=1);continue
    if variant=='Torid' and line=='Multishot consumes ammo directly from Capacity and increases Damage by +60%':
     effects.update(ExtraMultishotDamage=.6,FormMultishotBonus=.6,ExtraMultishotAmmo=1);continue
    m=re.fullmatch(r'With Sprint Speed 1.2 or Higher: \+(\d+)% (Projectile Speed|Fire Rate|Direct Damage per Status Type affecting the target)',line)
    if m:effects['Sprint'+{'Projectile Speed':'ProjectileSpeed','Fire Rate':'FireRate','Direct Damage per Status Type affecting the target':'InnateCO'}[m[2]]]=float(m[1])/100;continue
    m=re.fullmatch(r'Increase Base Critical (Chance|Damage Multiplier) by \+(\d+(?:\.\d+)?)([%x]) on undamaged enemies',line)
    if m:effects['Undamaged'+('BaseCriticalChance' if m[1]=='Chance' else 'BaseCriticalDamage')]=float(m[2])/(100 if m[3]=='%' else 1);continue
    m=re.fullmatch(r'On Firing: \+(\d+(?:\.\d+)?)(%?) (Base Multishot|Multishot)\. Stacks up to (\d+)x',line)
    if m:effects.update({('FiringBaseMultishot' if m[3]=='Base Multishot' else 'FiringMultishot'):float(m[1])/(100 if m[2] else 1),'FiringStackLimit':float(m[4])});continue
    m=re.fullmatch(r'On Reload From Empty: Increase Base (Magazine Capacity|Critical Damage Multiplier|Damage) by \+(\d+(?:\.\d+)?)(?:x)?\. Stacks up to (\d+)x',line)
    if m:effects.update({'EmptyReload'+fields[m[1]]:float(m[2]),'EmptyReloadStackLimit':float(m[3])});continue
    m=re.fullmatch(r'Increase Base (Status Chance|Critical Chance) by (\d+)% of current (Critical Chance|Status Chance), up to (\d+)%',line)
    if m:
     key='StatusFromCritical' if m[1]=='Status Chance' else 'CriticalFromStatus'
     effects.update({key:float(m[2])/100,key+'Cap':float(m[4])/100});continue
    m=re.fullmatch(r'On Reload [Ff]rom Empty(?: Magazine)?: Increase Base Damage by \+(\d+)',line)
    if m:effects.update(EmptyReloadBaseDamage=float(m[1]),EmptyReloadStackLimit=1);continue
    m=re.fullmatch(r'On Reload [Ff]rom Empty: \+(\d+)% Reload Speed',line)
    if m:
     # Wiki explicitly documents Dual Toxocyst's nonfunctional evolution.
     effects['DocumentedInactive' if 'Currently does not work' in row else 'EmptyReloadSpeed']=float(m[1])/100;continue
    m=re.fullmatch(r'\+(\d+) Base Multishot on final magazine burst',line)
    if m:effects['FinalBurstBaseMultishot']=float(m[1]);continue
    m=re.fullmatch(r'\+(\d+) Multishot on last shot in magazine',line)
    if m:effects['FinalShotBaseMultishot']=float(m[1]);continue
    m=re.fullmatch(r'\+(\d+)% Multishot if no enemies are within (\d+)m',line)
    if m:effects.update(IsolatedMultishot=float(m[1])/100,IsolatedMultishotRadius=float(m[2]));continue
    m=re.fullmatch(r'\+(\d+)% Ammo efficiency when (\d+) enemies are within (\d+)m',line)
    if m:effects.update(CrowdAmmoEfficiency=float(m[1])/100,CrowdMinimum=float(m[2]),CrowdRadius=float(m[3]));continue
    m=re.fullmatch(r'Increase Base Damage by \+(\d+) \(Sicarus\) / \+\d+ \(Prime\)',line)
    if m:effects['BaseDamage']=float(m[1]);continue
    m=re.fullmatch(r'x0 Critical Chance on Bodyshots, \+(\d+)% Critical Chance on Weakpoint Hits',line)
    if m:effects.update(NoBodyCritical=1,WeakpointCriticalChance=float(m[1])/100);continue
    m=re.fullmatch(r'\+(\d+)% Damage to enemies below half Health',line)
    if m and variant in ['Sicarus','Sicarus Prime','Dread']:
     effects['LowHealthNativeDamage']=float(m[1])/100
     if variant=='Dread':effects.update(LowHealthChargeExclusion=1,LowHealthIgnoresCO=1)
     continue
    m=re.fullmatch(r'Increase Damage by \+(\d+)% if enemy has less than half health',line)
    if m and variant in ['Kunai','Mk1-Kunai']:effects['LowHealthCOBracket']=float(m[1])/100;continue
    if line.startswith('On hitting enemies affected by Electricity Status:'):line=line.replace('On hitting enemies affected by Electricity Status:','On ElectricTargetHit:')
    if line=='On hitting a target affected by an Electricity status, 40% chance to restore 1 round in the magazine from ammo pool':
     triggers.append(dict(event='ElectricTargetHit',chance=.4,effects={'RestoreMagazine':1},normalOnly=True));continue
    trigger=parse_trigger(line,row,name)
    if trigger:triggers.append(trigger);continue
    if line:unknown.append(line)
   if variant in ['Kunai','Mk1-Kunai'] and effects.get('BaseStatusChance') and 'divided among the 2 projectiles' in row:effects['FormBaseStatusChance']=effects['BaseStatusChance']/2
   all_rows.append(dict(weaponId=WEAPONS[variant]['id'],weapon=variant,stage=stage,name=name,description=description,effects=effects,triggers=triggers,unhandled=unknown,source=SOURCES[title],raw=row))
(ROOT/'data/ranged-evolution-audit.json').write_text(json.dumps(all_rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Normalized variant perks',len(all_rows),'fully numeric',sum(not r.get('unhandled',[]) and not r.get('error') for r in all_rows))
