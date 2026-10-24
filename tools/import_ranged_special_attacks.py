"""Pinned exceptions for firing, reloading and augment-produced attacks.
All mappings are deliberately authored against the cited pages, not inferred by name.
"""
import json
from ranged_source_lib import ROOT,WEAPONS,SOURCES

def row(name,source=None,**values):
 return dict(weaponId=WEAPONS[name]['id'],**values,source=SOURCES[source or name])
def save(name,rows):
 (ROOT/'data'/f'{name}.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
save('ranged-airbursts',[row('Corinth',attack=2,lifetime=20/80,manual=False),row('Corinth Prime',attack=2,lifetime=2,manual=True)])
save('ranged-attack-overrides',[row(n,attack=1,values={'ShotType':'Hit-Scan'},reason='본문의 일반 탄환 보조 사격. 원본 모듈에서 ShotType만 누락됨.') for n in ['Hind','Pandero Prime']]+[row('Paracyst',attack=1,values={'Trigger':'Charge'},reason='작살 충전 0.25초'),row('Paracyst',attack=2,values={'ShotType':'DoT','Trigger':'Semi-Auto'},reason='4초간 5틱 작살 접촉 피해')])
save('ranged-reload-grenades',[row('Catabolyst',count=1),row('Coda Catabolyst',count=3)])
save('ranged-recall',[row('Riot-848',contact=0,explosion=1,limit=32)])
save('ranged-returning-blades',[row('Cantare',limit=30,criticalChance=3,reloadBonus=.2,duration=30,ensembleBonus=.6,ensembleDuration=60)])
save('ranged-augment-fields',[row(n,'Nightwatch Napalm',parent=1,child=2,kind='NightwatchNapalm',baseDamage=150,statusChance=.68,statusDuration=3) for n in ['Ogris','Kuva Ogris']])
print('특수 발사·재장전·장판 6개 데이터 파일 재생성')

# 시뮬러의 본문 예외는 RangedSimulor.cs와 ranged-simulor.json에 명시되어 있다.
