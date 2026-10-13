"""Normalize all fixed Wiki partitions and official Korean IDs; never execute Lua.

Usage: python import_full_catalog.py --cache <folder containing API snapshots>
Install tools/requirements-data.txt for a fresh Lua parse. Runtime app has no Python dependency.
"""
import argparse,collections,hashlib,json,pathlib,re
from wiki_tables import parse

ROOT=pathlib.Path(__file__).resolve().parents[1]
POLARITY={'Madurai':'AP_ATTACK','Vazarin':'AP_DEFENSE','Naramon':'AP_TACTIC','Zenurik':'AP_POWER','Unairu':'AP_WARD','Penjaga':'AP_PRECEPT','Umbra':'AP_UMBRA','None':'None','Universal':'Universal','Aura':'Omni'}
NORMAL=['Neutral','Forward','Block','Forward Block']
SHARED=['Heavy','Slide','Aerial','Wall','Finisher','Slam']
DAMAGE={'Impact','Puncture','Slash','Heat','Cold','Electricity','Toxin','Blast','Radiation','Gas','Magnetic','Viral','Corrosive'}

def clean(s): return re.sub(r'<[^>]*>','',s).replace('\\r\\n','\n').replace('\r\n','\n')
def url(name): return 'https://wiki.warframe.com/w/'+name.replace(' ','_')
def save(path,data): path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--cache',type=pathlib.Path,required=True);args=ap.parse_args();cache=args.cache
    load=lambda p:json.loads(p.read_text(encoding='utf-8'),strict=False)
    sources=[];tables={};raw_text={}
    for filename in ['modules-api.json','weapon-partitions-api.json']:
        for page in load(cache/filename)['query']['pages']:
            if 'revisions' not in page: continue
            title=page['title'];rev=page['revisions'][0];text=rev['slots']['main']['content']
            if title in ['Module:Weapons','Module:Weapons/data','Module:Mods']:continue
            variable={'Module:Stances/data':'StanceData','Module:Mods/data':'modData'}.get(title)
            tables[title]=parse(text,variable);raw_text[title]=text
            sources.append(dict(title=title,revision=rev['revid'],timestamp=rev['timestamp'],url=url(title)+'?oldid='+str(rev['revid']),sha256=hashlib.sha256(text.encode()).hexdigest()))
    exports={}
    for kind in ['ExportWeapons','ExportUpgrades','ExportRelicArcane']:
        for rows in load(cache/(kind+'_ko.json')).values():
            if isinstance(rows,list): exports.update({x['uniqueName']:x for x in rows if isinstance(x,dict) and 'uniqueName' in x})
    mods=tables['Module:Mods/data']['Mods'];stances=tables['Module:Stances/data'];arcanes=tables['Module:Arcane/data']['Arcanes']
    old=load(ROOT/'data/catalog.json');oldmods=[m for m in old['mods'] if m['family'] in ['pressure','steel','shatter','fury','bloodrush','weeping','overload','fever','north','molten','shock','virulent','vicious','volcanic','voltaic'] and m['englishName']!='Amalgam Organ Shatter' and m['englishName']!='Primed Fever Strike'];oldweapons=old['weapons'][:3]
    issues=[];references=[];weapons=[];capacity={};stance_meta={};normalized_stances=[]
    aliases=load(ROOT/'data/export-identity-aliases.json')['aliases']
    def local(w):
        identity=w.get('InternalName')
        official_id=aliases.get(identity,{}).get('exportId',identity)
        record=exports.get(official_id,{})
        return clean(record.get('name') or w.get('Name','')).strip(),record
    def inspect(value,path):
        if isinstance(value,dict):
            if 'unresolved' in value or '$duplicateFields' in value:issues.append(dict(path=path,value=value.get('unresolved') or value['$duplicateFields']))
            for k,v in value.items():inspect(v,path+'/'+str(k))
        elif isinstance(value,list):
            for i,v in enumerate(value):inspect(v,path+'/'+str(i))
    for title,table in tables.items():inspect(table,title)
    partition_counts={}
    for title,rows in tables.items():
        if not title.startswith('Module:Weapons/data/'):continue
        partition=title.rsplit('/',1)[1];partition_counts[partition]=len(rows)
        for key,w in rows.items():
            name,official=local(w);name=name or key
            attacks=w.get('Attacks',[]);attacks=attacks if isinstance(attacks,list) else []
            normal=next((a for a in attacks if a.get('AttackName')=='Normal Attack'),None)
            reasons=[]
            if partition!='melee':reasons.append('근접 외 공격·모드 규칙은 자료 열람 단계입니다.')
            if w.get('Class')=='Exalted Weapon':reasons.append('어빌리티 위력·전용 상호작용 설정이 필요합니다.')
            if not normal:reasons.append('일반 근접 공격 수치가 없습니다.')
            if normal and (not isinstance(normal.get('Damage'),dict) or not set(normal['Damage']).issubset(DAMAGE)):reasons.append('별도 피해 유형 처리가 필요합니다.')
            if normal and any(not isinstance(normal.get(f),(int,float)) for f in ['CritChance','CritMultiplier','StatusChance','FireRate']):reasons.append('기본 공격 수치가 누락되었습니다.')
            if normal and '$duplicateFields' in normal:reasons.append('위키 기본 공격에 중복 필드가 있습니다.')
            if not isinstance(w.get('InternalName'),str):reasons.append('공식 내부 식별자가 없습니다.')
            if any(x not in POLARITY for x in w.get('Polarities',[])):reasons.append('특수 슬롯의 수용량 규칙이 필요합니다.')
            if not reasons:
                weapons.append(dict(id=w['InternalName'],name=name,englishName=key,damage=normal['Damage'],criticalChance=normal['CritChance'],criticalMultiplier=normal['CritMultiplier'],statusChance=normal['StatusChance'],attackSpeed=normal['FireRate'],weaponType=w['Class'],maxRank=w.get('MaxRank',30),maxCombo=13 if key=='Venka Prime' else 12,source=url(w.get('Link',key)),innateNotes=['기본 일반 공격 기준. 인카논 진화·무기 고유 발동·프레임 시너지는 미적용.'] if len(attacks)>3 or any(x in w for x in ['DefaultUpgrades','IncarnonDuration']) else []))
                pol=[POLARITY[x] for x in w.get('Polarities',[])]
                capacity[w['InternalName']]=dict(slotPolarities=pol+['None']*(8-len(pol)),stanceSlotPolarity=POLARITY.get(w.get('StancePolarity'),'None'))
            # Keep every source row, including components, enemy weapons and alternate modes.
            references.append(dict(id='wiki:'+partition+'/'+key,internalId=w.get('InternalName',''),name=name,englishName=key,category=partition,weaponType=w.get('Class','기타'),source=url(w.get('Link',key)),localized=bool(official),calculable=not reasons,limitations=reasons,raw=w))
    unique=collections.Counter(w['id'] for w in weapons)
    if any(n!=1 for n in unique.values()):raise ValueError('Duplicate calculable weapon internal ID')
    # Preserve fixture ordering, while using Wiki numeric values for every weapon.
    first=[w['id'] for w in oldweapons]
    weapons.sort(key=lambda w:(first.index(w['id']) if w['id'] in first else 3,w['name']))
    stance_source=next(s for s in sources if s['title']=='Module:Stances/data')
    for key,s in stances.items():
        if key=='__Legend':continue
        combos=[];m=mods.get(key,{});name,official=local(m);name=name or key
        if m.get('InternalName') and 'BaseDrain' in m:
            stancemod=dict(modId=m['InternalName'],polarity=POLARITY[m['Polarity']],maxRank=m['MaxRank'],bonus=-m['BaseDrain']+m['MaxRank'])
            if official and any(official.get(f)!=m[g] for f,g in [('baseDrain','BaseDrain'),('fusionLimit','MaxRank')]):issues.append(dict(path='stance-export/'+key,value='공식 용량 수치 불일치'))
            stance_meta[key]=stancemod
        else:stance_meta[key]=dict(modId='',polarity='None',maxRank=0,bonus=0)
        for ck in NORMAL+SHARED:
            c=s.get(ck);origin=key
            if not c and ck in SHARED and key!='Ravenous Wraith':
                origin=s.get('WeaponType','');c=stances.get(origin,{}).get(ck)
            if not isinstance(c,dict):continue
            hits=[];unsupported=[]
            if ck not in NORMAL:unsupported.append('강공격·공중·슬라이드·마무리 전용 규칙은 자료 열람 단계입니다.')
            if s.get('ConclaveOnly'):unsupported.append('콘클레이브 전용 규칙입니다.')
            if not isinstance(c.get('Duration'),(int,float)) or c['Duration']<=0:unsupported.append('위키에 콤보 시간이 없습니다. DPS를 임의 추정하지 않습니다.')
            for group,a in enumerate(c.get('Attacks',[])):
                ds=a['Dmg'];counts=a['Hits']
                if len(ds)!=len(counts):raise ValueError('Stance Dmg/Hits mismatch: '+key+'/'+ck)
                for i,(d,n) in enumerate(zip(ds,counts)):
                    def at(field,default):
                        v=a.get(field,[])
                        return v[i] if isinstance(v,list) and i<len(v) else default
                    procs=at('Procs',[]);procs=procs if isinstance(procs,list) else [procs]
                    procs=[('unresolved:'+p['unresolved']) if isinstance(p,dict) else p for p in procs if p]
                    kind=at('Types','')
                    if kind in ['Slam','Ranged']:unsupported.append('별도 '+('스탠스 슬램 고정 피해' if kind=='Slam' else '투사체')+' 규칙이 필요합니다.')
                    if any(p.startswith('unresolved:') for p in procs):unsupported.append('원문 강제 상태 식별자가 미해결입니다.')
                    hits.append(dict(group=group+1,multiplier=d/100,count=n,impact=at('ImpactMultiplier',1),puncture=at('PunctureMultiplier',1),slash=at('SlashMultiplier',1),statusMultiplier=at('StatusMultiplier',1),forcedProcs=procs,attackType=kind))
            combos.append(dict(key=ck,name=c.get('Name',ck),duration=c.get('Duration'),hits=hits,unsupported=sorted(set(unsupported)),inheritedFrom=origin if origin!=key else None))
        normalized_stances.append(dict(id=key,name=name,weaponType=s.get('WeaponType',''),source=url(key),combos=combos,isDefault=key==s.get('WeaponType'),conclaveOnly=s.get('ConclaveOnly',False),uniqueToWeapon=s.get('UniqueToWeapon',False),modId=m.get('InternalName'),abilityOnly=key in ['Exalted Blade','Hysteria','Primal Fury','Ravenous Wraith','Serene Storm']))
    arcane_rows=[]
    supported={'Arcane Fury':'Fury','Arcane Strike':'Strike','Arcane Avenger':'Avenger','Arcane Blade Charger':'BladeCharger','Melee Exposure':'Exposure','Melee Retaliation':'Retaliation','Melee Doughty':'Doughty','Melee Duplicate':'Duplicate','Melee Careen':'Careen','Melee Fortification':'Utility','Melee Crescendo':'ComboSetup','Melee Vortex':'Utility','Melee Animosity':'HeavyOnly','Melee Assimilation':'HeavyOnly'}
    for key,a in arcanes.items():
        name,official=local(a)
        arcane_rows.append(dict(id=a.get('InternalName') or 'wiki:arcane/'+key,name=name or key,englishName=key,category=a['Type'],maxRank=a['MaxRank'],description=clean(a.get('Description','')),rankText=[[clean(x) for x in r['stats']] for r in official.get('levelStats',[])],source=url(key),localized=bool(official),rule=supported.get(key,'Unsupported'),raw=a))
    mod_rows=[]
    for key,m in mods.items():
        name,official=local(m)
        mod_rows.append(dict(id=m.get('InternalName') or 'wiki:mod/'+key,name=name or key,englishName=key,category=m.get('Type','기타'),source=url(m.get('Link',key)),description=clean(m.get('Description','')),rankText=[[clean(x) for x in r['stats']] for r in official.get('levelStats',[])],localized=bool(official),raw=m))
    # Explicit, reviewed atomic rules; all other general melee mods remain selectable with a blocking explanation.
    rules={
        'Spoiled Strike':({'Damage':1,'AttackSpeed':-.2},None,0,'spoiled'),
        'Auger Strike':({'PunctureDamage':1.2},None,0,'auger'),
        'Collision Force':({'ImpactDamage':1.2},None,0,'collision'),
        'Buzz Kill':({'SlashDamage':1.2},None,0,'buzz'),
        'Heavy Trauma':({'ImpactDamage':.9},None,0,'trauma'),
        'Primed Heavy Trauma':({'ImpactDamage':1.65},None,0,'trauma'),
        'Jagged Edge':({'SlashDamage':.9},None,0,'jagged'),
        'Rending Strike':({'SlashDamage':.6,'PunctureDamage':.8},None,0,'rending'),
        'Sundering Strike':({'PunctureDamage':.9},None,0,'sundering'),
        'Primed Fever Strike':({},'Toxin',1.65,'fever'),
        'Melee Prowess':({'StatusChance':.9},None,0,'prowess'),
        'Drifting Contact':({'StatusChance':.4},None,0,'drifting'),
        'Quickening':({'AttackSpeed':.4},None,0,'quickening'),
        'Amalgam Organ Shatter':({'CriticalDamage':.85},None,0,'shatter'),
        'Reach':({},None,0,'reach'),'Primed Reach':({},None,0,'reach'),
        'Spring-Loaded Blade':({},None,0,'spring'),
    }
    known_ids={m['id'] for m in oldmods}
    for key,m in mods.items():
        if m.get('Type')!='Melee' or m.get('InternalName') in known_ids:continue
        if not m.get('InternalName') or not isinstance(m.get('BaseDrain'),int) or m['BaseDrain']<0:continue
        name,e=local(m)
        effects,element,bonus,family=rules.get(key,({},None,0,'unsupported:'+m['InternalName']))
        issue=[] if key in rules else ['이 모드의 조건부/특수 효과 계산은 아직 지원하지 않습니다.']
        if m.get('ConclaveOnly'):issue=['콘클레이브 전용 모드입니다.']
        max_rank=3 if key=='Reach' else m['MaxRank'] # Reach page revision 2760400 Stats and official Export agree; module says 5.
        oldmods.append(dict(id=m['InternalName'],name=name or key,englishName=key,maxRank=max_rank,baseDrain=m['BaseDrain'],family=family,effects=effects,element=element,elementBonus=bonus,polarity=POLARITY.get(m.get('Polarity'),'None'),rankText=[[clean(x) for x in r['stats']] for r in e.get('levelStats',[])],calculationIssues=issue,source=url(key)))
        known_ids.add(m['InternalName'])
    # Cross-check by official internal ID. Wiki remains authoritative; retain every disagreement.
    comparisons=[]
    for w in weapons:
        e=exports.get(w['id'],{})
        for key,field in [('criticalChance','criticalChance'),('criticalMultiplier','criticalMultiplier'),('procChance','statusChance'),('fireRate','attackSpeed')]:
            if key in e and abs(e[key]-w[field])>0.002:comparisons.append(dict(id=w['id'],field=field,wiki=w[field],official=e[key]))
        if 'totalDamage' in e and abs(e['totalDamage']-sum(w['damage'].values()))>0.05:comparisons.append(dict(id=w['id'],field='totalDamage',wiki=sum(w['damage'].values()),official=e['totalDamage']))
    version='wiki-full-0.5.0-20261011'
    audit=dict(version=version,weaponPartitions=partition_counts,weaponRecords=len(references),weaponClasses=len({w['weaponType'] for w in references}),calculableMelee=len(weapons),stanceTables=len(normalized_stances),stanceMods=sum(bool(s.get('modId')) for s in normalized_stances),combos=sum(len(s['combos']) for s in normalized_stances),directCombos=sum(not c['unsupported'] for s in normalized_stances for c in s['combos']),arcanes=len(arcane_rows),mods=len(mod_rows),equippableMeleeMods=len(oldmods),calculatedMeleeMods=sum(not m.get('calculationIssues') for m in oldmods),missingKorean=dict(weapons=sum(not w['localized'] for w in references),arcanes=sum(not a['localized'] for a in arcane_rows),mods=sum(not m['localized'] for m in mod_rows)),sourceIssues=issues,officialDifferences=comparisons)
    folder=ROOT/'data'
    audit['reviewedOverrides']=[dict(item='Reach',field='MaxRank',module=5,selected=3,evidence='Reach Stats table revision 2760400 and official Export both have ranks 0-3'),dict(item='Venka Prime',field='MaxCombo',selected=13,evidence='Venka Prime revision 2783040; Blood Rush / Weeping Wounds remain capped at 12x'),dict(item='Exalted stance compatibility',field='AbilityOnly',selected=['Exalted Blade','Hysteria','Primal Fury','Ravenous Wraith','Serene Storm'],evidence='Warframe-specific stance InternalName; do not attach Exalted Blade to normal swords')]
    rules=[]
    for page in load(cache/'rules-api.json')['query']['pages']:
        if 'revisions' not in page:continue
        r=page['revisions'][0];text=r['slots']['main']['content'];rules.append(dict(title=page['title'],revision=r['revid'],timestamp=r['timestamp'],url=url(page['title'])+'?oldid='+str(r['revid']),sha256=hashlib.sha256(text.encode()).hexdigest(),text=text.split('==Patch')[0]))
    save(folder/'combat-rule-sources-v05.json',dict(retrievedAt='2026-10-11',sources=rules,reviewedOverrides=audit['reviewedOverrides']))
    save(folder/'reference-catalog.json',dict(version=version,sources=sources,weapons=references,arcanes=arcane_rows,mods=mod_rows,audit=audit))
    save(folder/'full-catalog-audit.json',audit)
    save(folder/'full-source-records.json',dict(sources=sources,exportSources=load(cache/'export-sources.json'),tables=tables))
    save(folder/'catalog.json',dict(version=version,source=next(s['url'] for s in sources if s['title']=='Module:Weapons/data/melee'),retrievedAt='2026-10-11',weapons=weapons,mods=oldmods))
    save(folder/'stances.json',dict(revision=stance_source['revision'],revisionTime=stance_source['timestamp'],retrievedAt='2026-10-11',source=stance_source['url'],authority='WF Wiki 기준 사양',sourceSha256=stance_source['sha256'],stances=normalized_stances))
    save(folder/'capacity-data.json',dict(version='capacity-0.5.0',weaponDefaults=capacity,stanceMods=stance_meta))
    print(json.dumps({k:v for k,v in audit.items() if k not in ['sourceIssues','officialDifferences']},ensure_ascii=False,indent=2))
    print('Source issues:',len(issues),'Official differences:',len(comparisons))

if __name__=='__main__': main()
