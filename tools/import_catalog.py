"""Import a pinned WFCD snapshot. Never resolve records by localized display name."""
import json, pathlib, urllib.request, hashlib
from import_localization import apply

ROOT = pathlib.Path(__file__).resolve().parents[1]
COMMIT = '4a73b2fb016d7a79ec0694aec42a66778d762288'
BASE = f'https://raw.githubusercontent.com/WFCD/warframe-items/{COMMIT}'
def fetch(path):
    return urllib.request.urlopen(urllib.request.Request(f'{BASE}/{path}', headers={'User-Agent': 'TennoForge-development'}), timeout=45).read()

def main():
    folder = ROOT / 'data'
    folder.mkdir(exist_ok=True)
    raw_melee, raw_mods = fetch('data/json/Melee.json'), fetch('data/json/Mods.json')
    melee, mods = json.loads(raw_melee), json.loads(raw_mods)
    by_id = {m['uniqueName']: m for m in mods}
    weapon_ids = {
        '/Lotus/Weapons/Tenno/Melee/Swords/PrimeKatana/PrimeNikana': '니카나 프라임',
        '/Lotus/Weapons/Tenno/Melee/Tonfa/TonfaContestWinnerPrime/TonfaContestWinnerPrimeWeapon': '크로넨 프라임',
        '/Lotus/Weapons/Tenno/Melee/Swords/PrimeGram/PrimeGram': '그램 프라임',
    }
    weapons = []
    selected_raw = []
    for wid, ko in weapon_ids.items():
        w = next(w for w in melee if w['uniqueName'] == wid)
        a = next(a for a in w['attacks'] if a['name'] == 'Normal Attack')
        weapons.append(dict(id=wid, name=ko, englishName=w['name'], damage={k.capitalize(): v for k,v in a['damage'].items()}, criticalChance=a['crit_chance']/100, criticalMultiplier=a['crit_mult'], statusChance=a['status_chance']/100, attackSpeed=w['fireRate']))
        selected_raw.append(w)
    definitions = [
        ('WeaponMeleeDamageMod','프레셔 포인트','pressure',{'Damage':1.2},None,0),
        ('Expert/WeaponMeleeDamageModExpert','프라임드 프레셔 포인트','pressure',{'Damage':1.65},None,0),
        ('WeaponCritChanceMod','트루 스틸','steel',{'CriticalChance':1.2},None,0),
        ('WeaponCritDamageMod','오르건 섀터','shatter',{'CriticalDamage':.9},None,0),
        ('WeaponFireRateMod','퓨리','fury',{'AttackSpeed':.3},None,0),
        ('Expert/WeaponFireRateModExpert','프라임드 퓨리','fury',{'AttackSpeed':.55},None,0),
        ('Event/ComboCritChanceMod','블러드 러시','bloodrush',{'BloodRush':.4},None,0),
        ('Event/ComboStatusChanceMod','위핑 운즈','weeping',{'WeepingWounds':.4},None,0),
        ('WeaponDamageIfVictimProcActive','컨디션 오버로드','overload',{'ConditionOverload':.8},None,0),
        ('WeaponToxinDamageMod','피버 스트라이크','fever',{},'Toxin',.9),
        ('WeaponFreezeDamageMod','노스 윈드','north',{},'Cold',.9),
        ('WeaponFireDamageMod','몰튼 임팩트','molten',{},'Heat',.9),
        ('WeaponElectricityDamageMod','쇼킹 터치','shock',{},'Electricity',.9),
        ('DualStat/PoisonEventMeleeMod','비럴런트 스커지','virulent',{'StatusChance':.6},'Toxin',.6),
        ('DualStat/IceEventMeleeMod','비셔스 프로스트','vicious',{'StatusChance':.6},'Cold',.6),
        ('DualStat/FireEventMeleeMod','볼케이닉 엣지','volcanic',{'StatusChance':.6},'Heat',.6),
        ('DualStat/ElectEventMeleeMod','볼타익 스트라이크','voltaic',{'StatusChance':.6},'Electricity',.6),
    ]
    normalized = []
    for suffix,ko,family,effects,element,bonus in definitions:
        mid = '/Lotus/Upgrades/Mods/Melee/' + suffix
        m = by_id[mid]
        normalized.append(dict(id=mid,name=ko,englishName=m['name'],maxRank=m['fusionLimit'],baseDrain=m['baseDrain'],family=family,effects=effects,element=element,elementBonus=bonus))
        selected_raw.append(m)
    doc = dict(version='wfcd-'+COMMIT,source=f'https://github.com/WFCD/warframe-items/tree/{COMMIT}',retrievedAt='2026-10-10',weapons=weapons,mods=normalized)
    doc = apply(doc, json.loads((folder/'localization-ko.json').read_text(encoding='utf-8')))
    (folder/'catalog.json').write_text(json.dumps(doc,ensure_ascii=False,indent=2),encoding='utf-8')
    (folder/'source-records.json').write_text(json.dumps(selected_raw,ensure_ascii=False,indent=2),encoding='utf-8')
    (folder/'source-hashes.json').write_text(json.dumps({'Melee.json':hashlib.sha256(raw_melee).hexdigest(),'Mods.json':hashlib.sha256(raw_mods).hexdigest()},indent=2),encoding='utf-8')
    (folder/'WFCD-LICENSE.txt').write_bytes(fetch('LICENSE'))
    print(f'가져오기 완료: 무기 {len(weapons)}개, 모드 {len(normalized)}개, {COMMIT}')

if __name__ == '__main__': main()
