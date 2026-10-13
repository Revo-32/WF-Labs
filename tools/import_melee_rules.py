"""Compile reviewed melee rules against internal IDs and pinned Wiki revisions.

The research cache is read-only input. Natural-language notes are not executed.
"""
from pathlib import Path
import json,re,hashlib,argparse
from incarnon_rules import compile_incarnons
from attack_rules import compile_attacks

ROOT=Path(__file__).resolve().parents[1]
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--cache',type=Path,required=True);args=parser.parse_args()
    reference=json.loads((ROOT/'data/reference-catalog.json').read_text(encoding='utf-8'))
    catalog=json.loads((ROOT/'data/catalog.json').read_text(encoding='utf-8'))
    research=json.loads((args.cache/'pages.json').read_text(encoding='utf-8'))
    pages={s['title']:s for s in research['sources']}
    def source(name):
        p=pages.get(name)
        return {k:v for k,v in p.items() if k!='content'} if p else None
    def clean(t):
        t=re.sub(r'<ref\b[^>]*>.*?</ref>','',t,flags=re.S)
        t=re.sub(r'<ref\b[^>]*/>','',t)
        t=re.sub(r'\[\[(?:[^\]|]+\|)?([^\]]+)\]\]',r'\1',t)
        t=re.sub(r'\{\{(?:D|M|Weapon|Arcane|WF|A|Stat|Faction|Resource)\|([^}|]+)(?:\|[^}]*)?\}\}',r'\1',t)
        t=re.sub(r'<[^>]+>','',t).replace("'''",'').replace("''",'')
        return t.strip()
    def notes(name):
        t=pages.get(name,{}).get('content','');parts=re.split(r'(^==[^=].*?==\s*$)',t,flags=re.M)
        return '\n\n'.join(clean(parts[i]+parts[i+1]) for i in range(1,len(parts),2) if any(k in parts[i].lower() for k in ['characteristics','unique','notes','evolution','passive']))
    rules={}
    def add(name,effects=None,**kw):rules[name]=dict(effects=effects or {},**kw)
    # Amounts are unrounded maximum-rank coefficients. Runtime scales by rank.
    for m in catalog['mods']:
        if not m.get('calculationIssues'):
            add(m['englishName'],m['effects'],element=m.get('element'),elementBonus=m.get('elementBonus',0))
    for name,amount,stat in [('Body Count',12,'ComboDuration'),('Corrupt Charge',30,'InitialCombo'),('Covert Lethality',16,'InitialCombo'),('Lasting Sting',1.1,'StatusDuration'),('Finishing Touch',.6,'FinisherDamage'),('Gladiator Might',.6,'CriticalDamage'),('Gladiator Rush',6,'ComboDuration'),('Gladiator Vice',.3,'AttackSpeed'),('Reflex Coil',.6,'HeavyEfficiency'),('Seismic Wave',2,'SlamDamage'),('True Punishment',1,'ComboChance')]:add(name,{stat:amount})
    rules['Corrupt Charge']['effects']['ComboDurationPercent']=-.5
    rules['True Punishment']['effects']['ComboDurationPercent']=-.5
    rules['Covert Lethality']['effects']['FinisherDamage']=1
    add('Focus Energy',{'HeavyEfficiency':.4},element='Electricity',elementBonus=.6)
    add('Focus Radon',{'HeavyEfficiency':.4},element='Radiation',elementBonus=.6)
    add('Magnetic Rush',{'AttackSpeed':.2},element='Magnetic',elementBonus=.6)
    add('Killing Blow',{'HeavyDamage':1.2,'Windup':.6})
    add('Melee Elementalist',{'StatusDamage':.9,'Windup':.6})
    add('Galvanized Steel',{'CriticalChance':1.1},conditional={'CriticalDamage':.3},condition='GalvanizedStacks',cap=4)
    add('Galvanized Elementalist',{'StatusDamage':.8},conditional={'StatusChance':.3},condition='GalvanizedStacks',cap=4)
    # Pinned article rev2657194 explicitly distinguishes true 1.85/rank from
    # the rounded in-game +20 display. Preserve fractional combo points.
    add('Galvanized Reflex',{'HeavyEfficiency':.5},conditional={'InitialCombo':20.35},condition='GalvanizedStacks',cap=4)
    add('Berserker Fury',conditional={'AttackSpeed':.35},condition='BerserkerStacks',cap=2)
    add('Flawed Berserker Fury',conditional={'AttackSpeed':.35*2/3},condition='BerserkerStacks',cap=2)
    for name,typ in [('Carnis Mandible','Slash'),('Saxum Thorax','Impact'),('Jugulus Barbs','Puncture')]:add(name,{typ+'Damage':.9,'StatusChance':.6},setEffect=name.split()[0])
    add('Sacrificial Pressure',{'Damage':1.1,'VsSentient':.33},setEffect='Sacrificial')
    add('Sacrificial Steel',{'CriticalChance':2.2,'VsSentient':.33},setEffect='Sacrificial')
    for faction in ['Corpus','Grineer','Infested','Orokin','The Murmur']:
        for prefix,value in [('',.3),('Primed ',.55),('Flawed ',.2)]:
            name=prefix+'Smite '+faction
            if any(m['englishName']==name for m in catalog['mods']):add(name,{'Vs'+faction.replace(' ',''):value})
    for m in catalog['mods']:
        n=m['englishName']
        if n.startswith('Flawed ') and n not in rules and n[7:] in rules:
            r=rules[n[7:]]
            add(n,{k:v*2/3 for k,v in r['effects'].items()},element=r.get('element'),elementBonus=r.get('elementBonus',0)*2/3)
    add('Maiming Strike',{'SlideCriticalChance':1.5})
    add('Enduring Affliction',conditional={'StatusChance':1},condition='Lifted',cap=1)
    add('Enduring Strike',conditional={'ComboChance':.2},condition='Lifted',cap=1)
    # rev.2804218: fixed +50 percentage points AFTER stance status multipliers.
    add('Proton Snap',conditional={'Toxin':1,'PostStanceStatus':.5},condition='ProtonSnap',cap=1)
    add('Strain Infection',conditional={'CriticalDamage':.2},condition='Cysts',cap=6)
    add('Healing Return',{'HealingPerStatus':11})
    add('Life Strike',{'HeavyLifeSteal':.2})
    add('Shattering Impact',{'BaseArmorStrip':6})
    add('Energy Channel',{'EnergyChannel':2})
    add('Melee Riven Mod',{'Riven':1})
    add('Zaw Riven Mod',{'Riven':1})
    add('Relentless Combination',{'SlashComboChance':1})
    add('Motus Impact',{'AerialRange':2})
    add('Focused Defense',{'BlockAngle':20})
    add('Guardian Derision',{'BlockingComboChance':.3},utility='도발 반경 15m')
    add('Parry',{'ParryChance':.96})
    add('Flawed Parry',{'ParryChance':.64})
    add('Dispatch Overdrive',{'HeavyMovementSpeed':.6})
    add('Tek Gravity',utility='표식 안 슬램 시 20m 내 적을 끌어당김')
    for name in ['Mentor\'s Legacy','Condition\'s Perfection','Discipline\'s Merit','Dreamer\'s Wrath','Master\'s Edge','Opportunity\'s Reach','Truth\'s Flame']:
        add(name,{'Tennokai':1})
    rules["Condition's Perfection"]['effects']['TennokaiStatus']=1
    rules["Dreamer's Wrath"]['effects'].update(TennokaiCriticalDamage=.32,TennokaiChance=.5)
    rules["Master's Edge"]['effects']['TennokaiDamage']=.6
    rules["Opportunity's Reach"]['effects'].update(TennokaiRange=3,TennokaiWindow=2)
    rules["Opportunity's Reach"]['rankEffects']=[{'Tennokai':1,'TennokaiRange':.75*(r+1),'TennokaiWindow':.8+.4*r} for r in range(4)]
    rules["Discipline's Merit"]['effects']['TennokaiEvery']=4
    rules["Discipline's Merit"]['rankEffects']=[{'Tennokai':1,'TennokaiEvery':7-r} for r in range(4)]
    rules["Truth's Flame"]['conditional']={'TennokaiDamage':1.2};rules["Truth's Flame"]['condition']='TruthFlame';rules["Truth's Flame"]['cap']=1
    rules["Truth's Flame"]['rankConditional']=[{'TennokaiDamage':value} for value in [.3,.4,.6,1.2]]
    rules['Amalgam Organ Shatter']['effects']['Windup']=.6
    rules['Drifting Contact']['effects']['ComboDuration']=10
    rules['Quickening']['effects']['ComboChance']=.2
    rules['Reach']['effects']['Range']=1.5;rules['Primed Reach']['effects']['Range']=3
    add('Spring-Loaded Blade',conditional={'Range':1},condition='SpringStacks',cap=2)
    for name in ['Gladiator Might','Gladiator Rush','Gladiator Vice']:rules[name]['setEffect']='Gladiator'
    add("Amar's Contempt",{'Damage':.9,'SlashDamage':.3},utility='아마르 세트: 강공격 대상에게 순간이동')
    add('Amalgam Furax Body Count',{'ComboDuration':15,'SecondaryFireRate':.45,'FuraxKillRadius':15},utility='처치 시 주위 폭발·충격 상태는 위키에 약 6~10중첩으로 명시되어 고정 중첩을 가정하지 않습니다.')
    add('Amalgam Ripkas True Steel',{'CriticalChance':1.87,'ShotgunReload':.2,'GoreChance':1})
    add('Amanata Pressure',{'Damage':3.3,'AmanataRandomProc':1},rankEffects=[{'Damage':.55*(r+1),'AmanataRandomProc':1} for r in range(6)])
    add('Avenging Truth',{'StoredDamageFraction':.5},rankEffects=[{'StoredDamageFraction':.2+.1*r} for r in range(4)],utility='진실 신디케이트 효과 · 저장 피해는 피해 모드 다음, 원소 모드 이전에 더해집니다.')
    for name in ['Blade of Truth','Bright Purity','Justice Blades']:add(name,{'Damage':1},utility='신디케이트 효과는 처치/친화도 조건에 따른 별도 효과입니다.')
    add('Burning Hate',conditional={'StatusVulnerability':1.2},condition='BurningHateApplied',cap=1)
    add('Combo Fury',utility='근접 처치 시 보조무기 재장전·탄창 +100%, 12초')
    add('Combo Killer',{'ComboDuration':5},utility='보조무기 처치 시 콤보 타이머 초기화')
    add('Electromagnetic Shielding',{'RedirectDamage':.5,'RedirectRadius':12})
    add('Entropy Detonation',{'GroundFinisherExplosion':1000,'GroundFinisherHealthExplosion':.2,'GroundFinisherExplosionRadius':10})
    add('Entropy Flight',{'ProjectileSpeed':1.4},utility='엔트로피 신디케이트 효과')
    add('Gleaming Blight',{'StatusChance':1},utility='블라이트 신디케이트 효과')
    add("Hunter's Bonesaw",conditional={'FinisherDamage':1.2},condition='ProneFinisher',cap=1)
    add('Mark of the Beast',utility='6초 내 근접 6처치: 보조무기 치확·상태 확률 +120%, 24초')
    add('Power Throw',{'PunchThrough':2},conditional={'ThrowWeaponDamage':1},condition='ConsecutiveThrows',cap=3)
    add('Flawed Power Throw',{'PunchThrough':4/3},conditional={'ThrowWeaponDamage':2/3},condition='ConsecutiveThrows',cap=3)
    add('Quick Return',{'Bounces':-4});add('Rebound',{'Bounces':4})
    add('Rift Strike',{'HeavyTeleportRange':25})
    add('Sentient Incision',{'AdaptiveElement':1.2})
    add('Static Discharge',{'StaticDischarge':.8},rankEffects=[{'StaticDischarge':.3+.1*r} for r in range(6)])
    add('Toxic Blight',element='Toxin',elementBonus=1,utility='블라이트 신디케이트 효과')
    add('Volatile Quick Return',{'Bounces':-4,'ExplosionRadius':3,'BounceExplosion':1,'DisablePunchThrough':1})
    add('Volatile Rebound',{'BounceExplosion':1,'DisablePunchThrough':1})
    add('Vulcan Blitz',{'KillExplosion':300,'KillHealthExplosion':.6,'KillExplosionRadius':6})
    add('Whirlwind',{'ProjectileSpeed':1.8});add('Flawed Whirlwind',{'ProjectileSpeed':1.2})
    additional=[];existing={m['id'] for m in catalog['mods']}
    polarity={'Madurai':'AP_ATTACK','Vazarin':'AP_DEFENSE','Naramon':'AP_TACTIC','Zenurik':'AP_POWER','Unairu':'AP_WARD','Umbra':'AP_UMBRA'}
    for m in reference['mods']:
        if m['id'] in existing or m['englishName'] not in rules:continue
        r=m['raw'];additional.append(dict(id=m['id'],name=m['name'],englishName=m['englishName'],maxRank=r.get('MaxRank',0),baseDrain=r.get('BaseDrain',0),family=m['id'],effects={},polarity=polarity.get(r.get('Polarity'),'None'),rankText=m['rankText'],calculationIssues=['확장 근접 계산 경로가 필요한 효과입니다.'],source=m['source']))
    working_mods=catalog['mods']+additional
    byid={m['id']:m for m in reference['mods']}
    result=[]
    for m in working_mods:
        n=m['englishName'];raw=byid[m['id']]['raw'];rule=rules.get(n,{})
        unsupported=[]
        if '/PvPMods/' in m['id']:unsupported=['콘클레이브 전용 모드입니다. PvE 계산에 장착할 수 없습니다.']
        elif rule.get('effects',{}).get('Riven'):unsupported=[]
        elif not rule:unsupported=['검토할 특수 규칙이 남아 있습니다. 효과를 제외한 계산을 하지 않습니다.']
        compatible=[]
        if m in additional or n=='Covert Lethality':
            typ=raw.get('Type')
            if n=='Covert Lethality':typ='Dagger'
            for w in reference['weapons']:
                if w['category']=='melee' and (w['raw'].get('Family')==typ or w['englishName']==typ or (typ=='Thrown Melee' and w['raw'].get('Class')=='Glaive') or (typ in ['Dagger','Dual Daggers'] and w['raw'].get('Class')==typ)):compatible.append(w['internalId'])
            if not compatible:raise ValueError('Weapon compatibility missing: '+n)
        result.append(dict(id=m['id'],name=n,maxRank=8 if rule.get('effects',{}).get('Riven') else 3 if n=='Energy Channel' else m['maxRank'],source=source(n) or source(n.removeprefix('Flawed ')),**rule,unsupported=unsupported,incompatible=[byid[x['id']]['id'] for x in working_mods if x['englishName'] in raw.get('Incompatible',[])],compatibleWeapons=compatible))
    passives={}
    def passive(names,key,label,maximum,effects,attack=''):
        for name in names.split('|'):passives.setdefault(name,[]).append(dict(key=key,label=label,maximum=maximum,effects=effects,attack=attack))
    passive('Lesion','Frenzy','프렌지 발동',1,{'SpeedMultiplier':.15,'Toxin':1,'FlatStatus':.25})
    # Arca Titron / Unique Trait explicitly places this with Pressure Point,
    # multiplicative with Seismic Wave. The generic Melee page's list of slam
    # bonuses is not a statement that they all share one additive bracket.
    passive('Arca Titron','SlamCapacitor','슬램 캐패시터',5,{'Damage':2.5,'SlamRange':2},'Slam')
    passive('Dual Viciss','GasBuff','가스 발동 중첩',5,{'AttackSpeed':.07,'MovementSpeed':.08})
    passive('Dorrclave','Vendetta','라이징 벤데타 발동',1,{'FlatStatus':1,'FollowThrough':.5})
    passive('Dorrclave','Signature','다카스 사용',1,{'HealingPerStatus':6})
    passive('Sibear','SibearBuff','강공격 상태 확률 버프',1,{'FlatStatus':.5})
    passive('Pennant','PennantKills','유효한 강공격 처치 중첩',100,{'AttackSpeed':.5})
    passive('Ekhein','HeavyInsight','헤비 인사이트 발동',1,{'Damage':.8,'AttackSpeed':.2})
    passive('Cobra & Crane|Cobra & Crane Prime|Sigma & Octantis|Silva & Aegis|Silva & Aegis Prime','BlockCharges','막기 충전',4,{'PassiveCriticalChance':.15,'StatusChance':.15})
    passive('Gazal Machete','DjinnBuff','진 페이탈 어트랙션 발동',1,{'Corrosive':.25})
    passive('Keratinos','ExtendedClaws','12배 강공격 사거리 버프',1,{'Range':2.8})
    passive('Hirudo|Coda Hirudo','Always','치명타 흡혈·인비거레이티드',1,{'CriticalLifeSteal':.05,'InvigoratedHealth':.05,'MovementSpeed':.05})
    passive('Secura Lecta','Always','마무리 공격 속도',1,{'FinisherSpeed':.5})
    passive('Fragor Prime','Always','기본 연속기',1,{'InitialCombo':30})
    passive('Furax Wraith','Always','기본 연속기',1,{'InitialCombo':20})
    passive('Fragor|Fragor Prime','Brokk','브로크 스킨',1,{'AttackSpeed':.05,'ComboDuration':-1})
    passive('Venato|Venato Prime','Signature','칼리반 사용',1,{'ComboChance':.5})
    passive('Corufell|Corufell Prime','Signature','시트린 사용',1,{'Windup':.2})
    passive('Tekko|Tekko Prime','Signature','아틀라스 사용',1,{'BaseStatus':.06})
    passive('Slaytra','Always','베기 지속 시간',1,{'SlashDuration':1})
    passive('Tonkkatt','Overheat','오버히트 중첩',30,{'SeparateHeat':.04})
    passive('Wrath','GraviticSlash','그라비틱 슬래시 중첩',10,{'PassiveCriticalChance':.15})
    passive('Pride','SlamStatus','슬램 상태 확률 버프 중첩',10,{'StatusChance':.15})
    passive('Xoris','Always','무한 콤보 지속',1,{'InfiniteCombo':1})
    passive('Venato Prime','Always','상태 발생 시 타우 추가',1,{'TauOnStatus':1})
    passive('Tenet Livia','BlockAngleStacks','막기 각도 중첩',5,{'BlockAngle':5})
    passive('Tenet Livia|Tenet Grigori','Always','수납 중 콤보 정지',1,{'HolsterPause':1})
    pending_mechanics={
        'Harmony':'강공격의 기존 상태 피해 소모와 처치 후 지속 시간 버프',
        'Thalys':'여러 대상에 박힌 가시의 연쇄 발동·폭발·진화 상호작용',
        'Azothane':'방어 콤보 특수 충격파와 직접 타격의 분리',
        'Sampotes':'슬램 연쇄 충격파의 겹침·유효 타격 수',
        'Spinnerex':'처치 시 독성 중첩에 따른 확률 폭발',
        'Sancti Magistar':'강공격으로 부여되는 영구 흡혈·주변 아군 회복',
        'Rakta Dark Dagger':'방사능 대상 타격·상태 피해에 따른 실드 회복',
        'Rumblejack':'고유 전후방 마무리의 분할 타격과 강제 전기 상태',
        'Ether Daggers':'고유 은신 마무리 피해 배수',
        'Secura Lecta':'고유 마무리 공격 속도',
        'Orvius':'공중 매달기 지속 피해·중단 시 폭발',
        'Tenet Exec':'슬램의 다중 충격파 겹침',
        'Tak & Lug':'투척의 연속 폭발과 회수 시간',
        'Vitrica':'유리화 상태와 지상 충격파가 만드는 유리 폭발',
        'Zenistar':'원반 접촉·폭발·오라의 복합 지속 피해',
        'Caustacyst':'파동의 이동 시간·대상 관통에 따른 수명 감소 및 웅덩이 중복',
        'Coda Caustacyst':'파동의 이동 시간·대상 관통에 따른 수명 감소 및 웅덩이 중복',
        'Tatsu':'소울 스웜의 저장 충전과 대상별 투사체 분배',
        'Tatsu Prime':'소울 스웜의 저장 충전과 대상별 투사체 분배',
    }
    weapons=[]
    for w in reference['weapons']:
        if w['category']!='melee':continue
        raw=w['raw'];name=raw.get('Link',w['englishName'])
        code={'Amanata':'Amanata','Coda Motovore':'Motovore','Galariak Prime':'Galariak','Dex Nikana':'DexNikana','Venato Prime':'VenatoPrime','Ack & Brunt':'AckBrunt','Paracesis':'Paracesis'}.get(w['englishName'],'')
        pending=[pending_mechanics[w['englishName']]] if w['englishName'] in pending_mechanics else []
        weapons.append(dict(id=w['internalId'],name=w['englishName'],source=source(name),notes=notes(name),raw=raw,passives=passives.get(w['englishName'],[]),code=code,attacks=compile_attacks(w),pendingMechanics=pending))
    arcane_codes={'Arcane Fury':'Fury','Arcane Strike':'Strike','Arcane Avenger':'Avenger','Arcane Blade Charger':'BladeCharger','Arcane Crepuscular':'Crepuscular','Arcane Hot Shot':'HotShot','Arcane Arachne':'Arachne','Theorem Demulcent':'Demulcent','Melee Animosity':'Animosity','Melee Assimilation':'Assimilation','Melee Careen':'Careen','Melee Crescendo':'Crescendo','Melee Doughty':'Doughty','Melee Duplicate':'Duplicate','Melee Exposure':'Exposure','Melee Fortification':'Fortification','Melee Influence':'Influence','Melee Retaliation':'Retaliation','Melee Vortex':'Vortex','Melee Afflictions':'Afflictions','Exodia Brave':'Brave','Exodia Contagion':'Contagion','Exodia Epidemic':'Epidemic','Exodia Force':'Force','Exodia Hunt':'Hunt','Exodia Might':'Might','Exodia Triumph':'Triumph','Exodia Valor':'Valor'}
    arcane_codes.update({'Arcane Guardian':'Guardian','Arcane Reaper':'Reaper','Arcane Ultimatum':'Ultimatum','Arcane Tanker':'Tanker','Arcane Trickery':'Trickery','Arcane Aegis':'Aegis','Arcane Barrier':'Barrier'})
    arcane_codes.update({'Arcane Eruption':'Eruption','Arcane Truculence':'Truculence','Arcane Universal Fallout':'UniversalFallout'})
    arcane_codes['Magus Aggress']='MagusAggress'
    arcanes=[dict(id=a['id'],code=arcane_codes.get(a['englishName'],'Utility'),source=source(a['englishName'])) for a in reference['arcanes'] if a['category'] in ['Melee','Zaw','Warframe'] or a['englishName']=='Magus Aggress']
    co_stances=[]
    co_page=pages.get('Condition Overload (Mechanic)',{})
    co_text=co_page.get('content','').split('===Stances===')[-1].split('=== Archguns ===')[0]
    keys=['Neutral','Forward','Forward Block','Block','Slide','Slam','Finisher','Wall','Aerial','Heavy']
    for line in co_text.splitlines():
        cells=line.lstrip('| ').split('||')
        if len(cells)<3:continue
        match=re.search(r'\{\{M\|([^}]+)\}\}',cells[1])
        if not match:continue
        for key,cell in zip(keys,cells[2:]):
            percent=re.fullmatch(r'\s*(\d+)%\s*',cell)
            if percent:co_stances.append(dict(stanceId=match[1],combo=key,factor=int(percent[1])/100,source=source('Condition Overload (Mechanic)')))
    overview=pages['Damage/Overview Table']['content']
    factions=re.findall(r'^!.*?\{\{D\|([^}]+)\}\}',overview,flags=re.M)
    assert len(factions)==15 and factions[0]=='Tenno',factions
    modifiers={f:{} for f in factions[1:]}
    for row in re.split(r'^\|-.*$',overview,flags=re.M):
        cells=re.findall(r'^\|(?![-}])([^\n]*)',row,flags=re.M)
        first=next((i for i,v in enumerate(cells) if v.startswith('{{D|')),None)
        if first is None:continue
        typ=re.match(r'\{\{D\|([^}]+)\}\}',cells[first])[1]
        values=cells[first+1:first+1+len(factions)]
        assert len(values)==len(factions),(typ,len(values))
        for f,cell in zip(factions,values):
            if f=='Tenno':continue
            if '>+</div>' in cell:modifiers[f][typ]=1.5
            elif '>-</div>' in cell:modifiers[f][typ]=.5
    labels={'Grineer':'그리니어','Kuva Grineer':'쿠바 그리니어','Corpus':'코퍼스','Corpus Amalgam':'코퍼스 아말감','Infested':'인페스티드','Infested Deimos':'데이모스 인페스티드','Orokin':'오로킨','Sentient':'센티언트','Narmer':'나르메르','The Murmur':'속삭임','Zariman':'자리만','Scaldra':'스칼드라','Techrot':'테크병','Anarchs':'아나키스트'}
    families={'Kuva Grineer':'Grineer','Corpus Amalgam':'Corpus','Infested Deimos':'Infested'}
    enemy_profiles=[dict(id='Neutral',name='중립 · 약점 없음',faction='Neutral',modifiers={},source=source('Damage/Overview Table'))]
    enemy_profiles += [dict(id=f.replace(' ',''),name=labels[f],faction=families.get(f,f).replace(' ',''),modifiers=v,source=source('Damage/Overview Table')) for f,v in modifiers.items()]
    output=dict(version='wiki-melee-rules-0.6.0',sources=[{k:v for k,v in s.items() if k!='content'} for s in research['sources']],weapons=weapons,mods=result,arcanes=arcanes,incarnons=compile_incarnons(reference['weapons'],pages),stanceCo=co_stances,additionalMods=additional,enemyProfiles=enemy_profiles)
    (ROOT/'data/melee-rules.json').write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (ROOT/'data/melee-source-records.json').write_text(json.dumps(research,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Rules:',len(result),'weapons:',len(weapons),'sources:',len(research['sources']))
    print('Explicitly unsupported mod records:',[m['name'] for m in result if m['unsupported'] and '콘클레이브' not in m['unsupported'][0]])
    print('Weapons with explicitly pending event mechanics:',sum(bool(w['pendingMechanics']) for w in weapons))
if __name__=='__main__':main()
