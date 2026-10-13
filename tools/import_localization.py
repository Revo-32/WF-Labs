"""Import Korean labels/polarities from pinned official Public Export manifests."""
import hashlib
import json
import pathlib
import re
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCES = {
    'ExportUpgrades': 'https://content.warframe.com/PublicExport/Manifest/ExportUpgrades_ko.json!00_ENNXpFrEP+1o4eixKHFDhg',
    'ExportWeapons': 'https://content.warframe.com/PublicExport/Manifest/ExportWeapons_ko.json!00_EbjufCglVfIC1GSoqzqSNg',
}
STANCE_IDS = dict(zip(
    ['Blind Justice', 'Decisive Judgement', 'Tranquil Cleave', 'Sovereign Outcast', 'Gemini Cross', 'Tempo Royale', 'Cleaving Whirlwind', 'Rending Crane'],
    ['KatanaCmbThree', 'KatanaCmbTwo', 'KatanaCmbOne', 'TonfaCmbTwo', 'TonfaCmbOne', 'AxeCmbThree', 'AxeCmbTwo', 'AxeCmbOne']))
STANCE_IDS = {name: '/Lotus/Weapons/Tenno/Melee/MeleeTrees/' + key + 'MeleeTree' for name, key in STANCE_IDS.items()}

def apply(catalog, localization):
    for row in catalog['mods'] + catalog['weapons']:
        record = localization['records'][row['id']]
        row['name'] = record['name']
        if 'family' in row:
            if (row['baseDrain'], row['maxRank']) != (record['baseDrain'], record['fusionLimit']):
                raise ValueError('Export numerical fields changed; review before importing: ' + row['id'])
            row['polarity'] = record['polarity']
            row['rankText'] = [[re.sub(r'<[^>]*>', '', line) for line in rank['stats']] for rank in record['levelStats']]
    return catalog

def main():
    folder = ROOT / 'data'
    catalog = json.loads((folder / 'catalog.json').read_text(encoding='utf-8'))
    ids = {row['id'] for row in catalog['mods'] + catalog['weapons']} | set(STANCE_IDS.values())
    records, sources = {}, {}
    for key, url in SOURCES.items():
        raw = urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': 'WFLabs/0.3 data import'}), timeout=45).read()
        data = json.loads(raw, strict=False)
        records.update({row['uniqueName']: row for row in data[key] if row['uniqueName'] in ids})
        sources[key] = dict(url=url, sha256=hashlib.sha256(raw).hexdigest())
    if set(records) != ids:
        raise ValueError('Missing official IDs: ' + str(ids - set(records)))
    snapshot = dict(retrievedAt='2026-10-10', authority='Digital Extremes Public Export / ko', sources=sources, stanceIds=STANCE_IDS, records=records)
    def save(name, data):
        (folder / name).write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    save('localization-ko.json', snapshot)
    save('catalog.json', apply(catalog, snapshot))
    stances = json.loads((folder / 'stances.json').read_text(encoding='utf-8'))
    for stance in stances['stances']:
        stance['name'] = records[STANCE_IDS[stance['id']]]['name']
    save('stances.json', stances)
    print(f'Official Korean labels: {len(records)} records, including polarity and rank text.')

if __name__ == '__main__':
    main()
