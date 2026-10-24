"""Ranged Riven magnitudes from the pinned Wiki base/spliced value tables."""
import json
from ranged_source_lib import ROOT,SOURCES
# Rifle, Shotgun, Pistol. Values are displayed percent magnitudes, except punch-through metres.
rows=[
 ('Damage','피해',[165,164.7,219.6],True),('CriticalChance','치명타 확률',[149.99,90,149.99],False),
 ('CriticalDamage','치명타 피해',[119.997,90,90],False),('Multishot','멀티샷',[90,119.7,119.7],False),
 ('FireRate','연사력',[60.03,90,74.7],False),('StatusChance','상태 확률',[90,90,90],False),
 ('StatusDuration','상태 지속 시간',[99.99]*3,False),('Magazine','탄창 용량',[49.995]*3,False),
 ('ReloadSpeed','재장전 속도',[49.995]*3,False),('AmmoMaximum','최대 탄약',[49.95,90,90],False),
 ('ProjectileSpeed','투사체 속도',[90]*3,False),('PunchThrough','관통 거리',[2.7]*3,True),
 ('Recoil','반동 감소',[90]*3,False),('Zoom','줌',[59.99,41.994,80.1],False),
 ('Impact','충격',[119.97]*3,False),('Puncture','관통 피해',[119.997]*3,False),('Slash','베기',[119.97]*3,False),
]
rows += [(name,label,[90]*3,True) for name,label in [('Heat','화염'),('Cold','냉기'),('Electricity','전기'),('Toxin','독성')]]
rows += [('Faction'+name,label+' 대상 피해',[45]*3,False) for name,label in [('Corpus','코퍼스'),('Grineer','그리니어'),('Infested','인페스티드')]]
spliced=[('AmmoEfficiency','탄약 효율',45),('HolsterReload','수납 중 재장전',90),('WeakpointDamage','약점 피해',225),('WeakpointCriticalChance','약점 치명타 확률',247.5),('StatusDamage','상태 피해',90)]
spliced += [(name,label,90) for name,label in [('Blast','폭발'),('Corrosive','부식'),('Gas','가스'),('Magnetic','자성'),('Radiation','방사능'),('Viral','바이러스')]]
spliced += [('Faction'+name,label+' 대상 피해',45) for name,label in [('Orokin','오로킨'),('Scaldra','스칼드라'),('Techrot','테크롯')]]
attributes=[dict(key=k,name=n,bases=dict(zip(['Rifle','Shotgun','Pistol'],v)),positiveOnly=pos,spliced=False,unit='m' if k=='PunchThrough' else '%') for k,n,v,pos in rows]
attributes += [dict(key=k,name=n,bases=dict(Rifle=v,Shotgun=v,Pistol=v),positiveOnly=True,spliced=True,unit='%') for k,n,v in spliced]
recipes=[('Damage','Zoom','WeakpointDamage'),('Damage','Multishot','WeakpointDamage'),('CriticalChance','Zoom','WeakpointCriticalChance'),('CriticalChance','Multishot','WeakpointCriticalChance'),('AmmoMaximum','ReloadSpeed','HolsterReload'),('AmmoMaximum','Magazine','HolsterReload'),('Magazine','ReloadSpeed','AmmoEfficiency'),('Recoil','AmmoMaximum','AmmoEfficiency'),('Damage','StatusChance','StatusDamage'),('Toxin','Heat','Gas'),('Toxin','Electricity','Corrosive'),('Toxin','Cold','Viral'),('Heat','Electricity','Radiation'),('Heat','Cold','Blast'),('Electricity','Cold','Magnetic'),('FactionCorpus','FactionGrineer','FactionOrokin'),('FactionCorpus','FactionInfested','FactionTechrot'),('FactionInfested','FactionGrineer','FactionScaldra')]
source=SOURCES['Riven Mods']
(ROOT/'data/ranged-rivens.json').write_text(json.dumps(dict(version='ranged-riven-'+str(source['revision']),source=source,attributes=attributes,recipes=[dict(first=a,second=b,result=c) for a,b,c in recipes]),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(len(attributes),'ranged attributes',len(recipes),'splicing recipes')
