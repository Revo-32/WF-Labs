"""Audit UI item names against hash-pinned DE Korean Public Export records."""
import concurrent.futures
import hashlib
import json
import pathlib
import re
import sys
import urllib.request

sys.stdout.reconfigure(encoding='utf-8')
ROOT = pathlib.Path(__file__).resolve().parents[1]
CACHE = ROOT / 'artifacts/korean-audit'
CACHE.mkdir(parents=True, exist_ok=True)
load = lambda p: json.loads(p.read_text(encoding='utf-8-sig'), strict=False)
sources = load(ROOT / 'data/full-source-records.json')['exportSources']

def fetch(pair):
    kind, source = pair
    path = CACHE / (kind + '_ko.json')
    raw = path.read_bytes() if path.exists() else urllib.request.urlopen(source['url'], timeout=45).read()
    assert hashlib.sha256(raw).hexdigest() == source['sha256'], kind
    path.write_bytes(raw)
    return kind, len(raw)

if __name__ == '__main__':
    print(list(concurrent.futures.ThreadPoolExecutor().map(fetch, sources.items())))
    records = {}
    for kind in sources:
        for rows in load(CACHE / (kind + '_ko.json')).values():
            if isinstance(rows, list):
                records.update({r['uniqueName']: r for r in rows if isinstance(r, dict) and 'uniqueName' in r})
    catalog = load(ROOT / 'data/reference-catalog.json')
    aliases = load(ROOT / 'data/export-identity-aliases.json')
    explicit = {
        '/Lotus/Upgrades/Mods/Melee/Beginner/AvatarParryMeleeModBeginner': '/Lotus/Upgrades/Mods/Warframe/Beginner/AvatarParryMeleeModBeginner',
        '/Lotus/Upgrades/Mods/Melee/Beginner/WeaponMeleeChargeRateModBeginner': '/Lotus/Upgrades/Mods/Melee/Beginner/WeaponMeleeChannelingEfficiencyModBeginner',
        '/Lotus/Upgrades/Mods/Melee/Beginner/WeaponArmorPiercingDamageMod': '/Lotus/Upgrades/Mods/Melee/Beginner/WeaponArmorPiercingDamageModBeginner',
        '/Lotus/Weapons/Grineer/Melee/GrnGhoulSaw/GrnKuvaGhoulSawPlayer': '/Lotus/Weapons/Grineer/KuvaLich/Melee/Ghoulsaw/KuvaGhoulSaw',
        **{f'/Lotus/Weapons/Tenno/Melee/Swords/DarkSword/{x}': '/Lotus/Weapons/Tenno/Melee/Swords/DarkSword/DarkSwordDaggerHybridWeapon' for x in ['DarkSwordDaggerDuals','DarkSwordDaggerSingle']},
        '/Lotus/Upgrades/Mods/Shotgun/Beginner/WeaponColdDamageModBeginner': '/Lotus/Upgrades/Mods/Shotgun/Beginner/WeaponFreezeDamageModBeginner',
        **{f'/Lotus/Upgrades/Mods/Shotgun/Beginner/WeaponFactionDamage{x}Beginner': f'/Lotus/Upgrades/Mods/Shotgun/Beginner/WeaponShotgunFactionDamage{x}Beginner' for x in ['Corpus','Grineer','Infested']},
        **{f'/Lotus/Upgrades/Mods/{x}/Beginner/WeaponStunChanceModBeginner': f'/Lotus/Upgrades/Mods/{x}/Beginner/WeaponStatusChanceModBeginner' for x in ['Rifle','Pistol']},
    }
    changes=[]
    overrides=load(ROOT/'data/korean-name-overrides.json')['records']
    def clean(text):
        return re.sub('<[^>]*>', '', text).replace('\r', '').strip()
    for kind in ['weapons','mods','arcanes']:
        for row in catalog[kind]:
            identity = row.get('internalId') or row['raw'].get('InternalName') or row['id']
            official_id=explicit.get(identity, aliases['aliases'].get(identity,{}).get('exportId',identity))
            record=records.get(official_id)
            if record is None and kind=='mods':
                # Exact official English tag, never a fuzzy display-name match.
                matches=[r for r in records.values() if row['englishName'] in r.get('tags',[]) and r.get('fusionLimit')==row['raw'].get('MaxRank')]
                if len(matches)==1:record=matches[0];official_id=record['uniqueName']
            if record is None:
                if identity in overrides:
                    row['name']=overrides[identity]['name'];row['localized']=True
                continue
            if identity!=official_id:
                source=sources['ExportWeapons' if kind=='weapons' else 'ExportUpgrades' if kind=='mods' else 'ExportRelicArcane']
                aliases['aliases'][identity]=dict(exportId=official_id,wikiSource=row['source'],exportSource=source['url'],exportSha256=source['sha256'])
            name=clean(record['name'])
            if identity.endswith('DarkSwordDaggerDuals'):name+=' (쌍수 도검)'
            if identity.endswith('DarkSwordDaggerSingle'):name+=' (중도검)'
            if row['name']!=name:changes.append(dict(id=row['id'],before=row['name'],after=name))
            row['name']=name;row['localized']=True
            if kind!='weapons':row['rankText']=[[clean(t) for t in r['stats']] for r in record.get('levelStats',[])]
    if '--apply' in sys.argv:
        def write(path,data):
            compact=path.name in ['catalog.json','reference-catalog.json']
            path.write_text(json.dumps(data,ensure_ascii=False,indent=None if compact else 2,separators=(',',':') if compact else None)+('' if compact else '\n'),encoding='utf-8')
        write(ROOT/'data/reference-catalog.json',catalog)
        write(ROOT/'data/export-identity-aliases.json',aliases)
        lookup={r['id']:r for kind in ['weapons','mods','arcanes'] for r in catalog[kind]}
        stances=load(ROOT/'data/stances.json')['stances']
        classes={}
        for stance in stances:
            identity=stance.get('modId');identity=aliases['aliases'].get(identity,{}).get('exportId',identity)
            if identity in records and records[identity].get('compatName'):classes[stance['weaponType']]=records[identity]['compatName']
        for filename in ['catalog.json','ranged-mods.json','ranged-arcanes.json','stances.json']:
            path=ROOT/'data'/filename;data=load(path)
            groups=[data] if isinstance(data,list) else [v for v in data.values() if isinstance(v,list)]
            for rows in groups:
                for row in rows:
                    if filename=='stances.json' and isinstance(row,dict):
                        # Stance table keys use the Wiki name, while the mod has a
                        # stable internal ID. Join through its exact source title.
                        match=next((m for m in catalog['mods'] if m['englishName']==row.get('id') and m['localized']),None)
                        if match:row['name']=match['name']
                        elif row.get('isDefault') and row.get('id') in classes:row['name']=classes[row['id']]
                        elif row.get('uniqueToWeapon'):
                            match=next((w for w in catalog['weapons'] if w['englishName']==row.get('id') and w['localized']),None)
                            if match:row['name']=match['name']
                    if isinstance(row,dict) and row.get('id') in lookup:
                        official=lookup[row['id']];row['name']=official['name']
                        if 'rankText' in row:row['rankText']=official.get('rankText',row['rankText'])
            if filename=='ranged-mods.json':data['sourceSha256']=hashlib.sha256((ROOT/'data/reference-catalog.json').read_bytes()).hexdigest()
            write(path,data)
        write(CACHE/'changes.json',changes)
    print('공식 한국어 이름 변경',len(changes))
    missing = {}
    for kind in ['weapons', 'mods', 'arcanes']:
        missing[kind] = [r for r in catalog[kind] if not re.search('[가-힣]', r['name'])]
        print(kind, len(missing[kind]))
        for row in missing[kind][:50]:
            identity = row.get('internalId') or row['raw'].get('InternalName') or row['id']
            print(row['englishName'], identity, '=>', records.get(identity, {}).get('name'))
    (CACHE / 'missing.json').write_text(json.dumps(missing, ensure_ascii=False, indent=2), encoding='utf-8')
