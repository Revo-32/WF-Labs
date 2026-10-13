"""Import per-hit CO behavior from the source explicitly cited by WF Wiki.

Only matching input order, hit count, motion multiplier and physical multiplier
are accepted. Unmatched / irregular observations never become default 100%.
The downloaded workbook is input data; no spreadsheet formulas are executed.
"""
import argparse, datetime, hashlib, json, pathlib, re, zipfile
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]
URL = 'https://docs.google.com/spreadsheets/d/1FJ_RIEG2mlFGBQ__szeGoFM6azj23n0CqelBFO_MGGw/edit'
N = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}

def read_sheet(path, name):
    with zipfile.ZipFile(path) as z:
        strings = [''.join(x.itertext()) for x in ET.fromstring(z.read('xl/sharedStrings.xml'))]
        rels = {x.get('Id'): x.get('Target') for x in ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))}
        sheets = ET.fromstring(z.read('xl/workbook.xml')).find('s:sheets', N)
        sh = next(s for s in sheets if s.get('name') == name)
        target = rels[sh.get('{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id')]
        target = target.lstrip('/') if target.startswith('/') else 'xl/' + target
        rows = {}
        for row in ET.fromstring(z.read(target)).find('s:sheetData', N):
            cells = {}
            for c in row:
                v = c.find('s:v', N)
                if v is not None and v.text is not None:
                    cells[re.sub(r'\d+', '', c.get('r'))] = strings[int(v.text)] if c.get('t') == 's' else v.text
            rows[int(row.get('r'))] = cells
        return rows

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('workbook', type=pathlib.Path); args = parser.parse_args()
    rows = read_sheet(args.workbook, 'Melee Stances sheet v2')
    stances = {s['id']: s for s in json.loads((ROOT/'data/stances.json').read_text(encoding='utf-8'))['stances']}
    accepted, rejected, evidence = [], [], []
    name = None
    counts = {'two':2, 'three':3, 'four':4, 'five':5, 'six':6, 'seven':7, 'eight':8, 'nine':9, 'ten':10}
    for r, cells in rows.items():
        if cells.get('B') and cells.get('C')=='Class': name = cells['B'] if cells['B'] in stances else None
        if cells.get('E') not in ['Neutral', 'Forward', 'Block', 'Forward Block'] or not name: continue
        key = cells['E']; combos = stances[name]['combos']
        combo = next((c for c in combos if c['key']==key), None)
        if combo is None: continue
        entries = []; error = None
        for col in 'FGHIJKLMNOPQRSTU':
            label = cells.get(col, '')
            if not label.startswith('Input '): continue
            m = re.match(r'Input (\d+)(?: - (\w+) hits)?$', label)
            if not m: error = 'input label'; break
            behavior = rows.get(r+8, {}).get(col)
            if behavior not in ['Works','Ignores Mult','Mult is 1x']: error = 'unresolved behavior'; break
            motion = float(rows[r+2][col]); physical = rows[r+3].get(col, 'Normal')
            entries += [(int(m[1]), motion, physical, behavior, col+str(r+8))] * counts.get(m[2], 1)
        hits = [h for h in combo['hits'] if h['multiplier']>0 and h['attackType']!='Slam']
        expanded = [h for h in hits for _ in range(h['count'])]
        if error or not entries or len(entries)!=len(expanded):
            rejected.append(dict(stanceId=name, combo=key, reason=error or 'hit count mismatch')); continue
        factors = []; at = 0
        for h in hits:
            group_factors=[]
            for _ in range(h['count']):
                group, motion, physical, behavior, cell = entries[at]; at+=1
                expected_physical = {'impact':1.,'puncture':1.,'slash':1.}
                if physical!='Normal':
                    pm = re.fullmatch(r'(Impact|Puncture|Slash) \+(\d+)%', physical)
                    if pm: expected_physical[pm[1].lower()] += int(pm[2])/100
                    else: error='physical annotation'; break
                if group!=h['group'] or abs(motion-h['multiplier'])>.03 or any(abs(h[k]-v)>1e-6 for k,v in expected_physical.items()):
                    error='wiki hit signature mismatch'; break
                group_factors.append(1/h['multiplier'] if behavior=='Ignores Mult' else 1.)
            if error: break
            if len(set(group_factors))!=1: error='mixed repeated-hit behavior'; break
            factors.append(group_factors[0])
        if error:
            rejected.append(dict(stanceId=name, combo=key, reason=error)); continue
        accepted.append(dict(stanceId=name, combo=key, factors=factors, sourceRow=r))
        evidence.append(dict(stanceId=name, combo=key, rows={str(i):rows.get(i,{}) for i in range(r,r+9)}))
    source=dict(url=URL, citedBy='https://wiki.warframe.com/w/Condition_Overload_(Mechanic)?oldid=2815998#Stances',
                retrievedAt=datetime.datetime.now(datetime.timezone.utc).isoformat(), workbookSha256=hashlib.sha256(args.workbook.read_bytes()).hexdigest(), sheet='Melee Stances sheet v2')
    for filename,data in [('stance-hit-co.json',dict(version='wiki-cited-hit-co-1',source=source,rules=accepted)),
                           ('stance-hit-co-evidence.json',dict(source=source,accepted=evidence,rejected=rejected))]:
        (ROOT/'data'/filename).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(accepted=len(accepted),rejected=len(rejected),blindJustice=[r for r in accepted if r['stanceId']=='Blind Justice'])))

if __name__=='__main__': main()
