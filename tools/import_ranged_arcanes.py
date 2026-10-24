from ranged_source_lib import *
rows=[]
codes={
 'Akimbo Slip Shot':('AkimboSlip',1,0),
 'Primary Merciless':('Merciless',12,4),'Secondary Merciless':('Merciless',12,4),
 'Primary Deadhead':('Deadhead',3,24),'Secondary Deadhead':('Deadhead',3,24),
 'Primary Dexterity':('Dexterity',6,20),'Secondary Dexterity':('Dexterity',6,20),
 'Primary Frostbite':('Frostbite',40,12),'Primary Blight':('Blight',40,12),
 'Cascadia Flare':('Flare',40,10),'Conjunction Voltage':('Voltage',40,12),
 'Cascadia Accuracy':('Accuracy',1,4),'Cascadia Overcharge':('Overcharge',1,0),
 'Cascadia Empowered':('Empowered',1,0),'Secondary Shiver':('Shiver',10,0),
 'Secondary Fortifier':('Fortifier',1,0),'Secondary Encumber':('Encumber',1,0),
 'Secondary Enervate':('Enervate',1000,0),'Secondary Kinship':('Kinship',1000,0),
 'Secondary Outburst':('Outburst',13,30),'Secondary Surge':('Surge',1,0),
 'Secondary Cryogenic':('Cryogenic',1,0),'Secondary Irradiate':('Irradiate',1,0),
 'Shotgun Vendetta':('Vendetta',1,15),'Primary Bulwark':('Bulwark',1,0),
 'Primary Compression':('Compression',1,0),'Primary Crux':('Crux',10,10),
 'Primary Debilitate':('Debilitate',1,0),'Primary Exhilarate':('Exhilarate',3,10),
 'Primary Obstruct':('Obstruct',1,0),'Primary Overcharge':('EnergyMultishot',1,0),
 'Primary Plated Round':('PlatedRound',1,10),'Longbow Sharpshot':('Sharpshot',1,0),
}
for a in CATALOG['arcanes']:
 if a['englishName'] not in codes:continue
 code,cap,duration=codes[a['englishName']]
 rows.append(dict(id=a['id'],name=a['name'],code=code,category='primary' if a['englishName'].startswith(('Primary ','Longbow ','Shotgun ')) else 'secondary',maxRank=a['maxRank'],cap=cap,duration=duration,source=SOURCES.get(a['englishName']),description=a['description']))
(ROOT/'data/ranged-arcanes.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Ranged arcane rules:',len(rows))
