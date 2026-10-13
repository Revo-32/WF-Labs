"""Compile finisher tables from a pinned Wiki original; do not execute Wiki code."""
import hashlib, json, pathlib, re

ROOT = pathlib.Path(__file__).resolve().parents[1]

def rows(text, heading):
    section = text.split(heading, 1)[1]
    table = section[section.index('{|'):section.index('|}')]
    table = re.sub(r'\[\[(?:[^\]|]*\|)?([^\]]+)\]\]', r'\1', table)
    table = re.sub(r'\{\{Weapon\|([^}]+)\}\}', r'\1', table)
    table = table.replace('{{mul}}', '×')
    result = {}
    for row in re.split(r'^\|-\s*$', table, flags=re.M):
        cells = []
        for line in row.splitlines():
            if line.startswith('|') and not line.startswith(('|-', '|}')):
                cells.extend(c.strip() for c in line[1:].split('||'))
        if cells and cells[0]: result[cells[0]] = cells[1:]
    return result

def parse_cell(cell):
    if cell == 'None': return [], '원문에 이 마무리 동작이 없습니다.'
    match = re.match(r'(\d+(?:\.\d+)?)\s*x?', cell)
    if not match: raise ValueError('Unknown multiplier: '+cell)
    total = float(match[1])
    sequence = re.search(r'\(([^)]+)\)', cell)
    hits = []
    if sequence:
        for term in sequence[1].split('+'):
            m = re.fullmatch(r'\s*(\d+(?:\.\d+)?)\s*x?\s*(?:×\s*(\d+)\s*hits?)?\s*', term)
            if not m: raise ValueError('Unknown hit term: '+term)
            hits.extend([float(m[1])]*int(m[2] or 1))
    else: hits = [total]
    issue = '' if abs(sum(hits)-total)<1e-8 else f'원문 총 배율 {total:g}과 분할 타격 합 {sum(hits):g}이 다릅니다: {cell}'
    return hits, issue

def main():
    folder = ROOT/'data/combat-expansion-sources'
    source = next(s for s in json.loads((folder/'manifest.json').read_text(encoding='utf-8')) if s['title']=='Finisher')
    raw = (folder/source['file']).read_text(encoding='utf-8')
    assert hashlib.sha256(raw.encode()).hexdigest() == source['sha256']
    stealth = rows(raw, '==Stealth Attack==')
    front = rows(raw, '==Front/Back/Universal Finisher==')
    combined = rows(raw, '==All Finishers Multiplier Comparison==')
    records = []
    for name, values in front.items():
        for kind, index, comp_index in [('Back',0,2),('Front',1,3),('Universal',2,4),('Stealth',0,1)]:
            cell = stealth.get(name,[])[0] if kind=='Stealth' and name in stealth else values[index] if kind!='Stealth' else None
            if cell is None: continue
            hits, issue = parse_cell(cell)
            other = combined.get(name,[])
            if len(other)>comp_index:
                other_cell = other[comp_index]; other_hits, other_issue = parse_cell(other_cell)
                if not issue and not other_issue:
                    if abs(sum(hits)-sum(other_hits))>1e-8 or (len(hits)>1 and len(other_hits)>1 and hits!=other_hits):
                        issue = f'개별 표와 비교표의 타격 정보가 다릅니다: {cell} / {other_cell}'
                    elif len(other_hits)>len(hits): hits=other_hits
            records.append(dict(weaponClass=name,kind=kind,hits=hits,issue=issue,sourceText=cell))
    output=dict(source={k:v for k,v in source.items() if k not in ('file','retrievedAt')},rules=records)
    (ROOT/'data/finishers.json').write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Finishers:',len(records),'classes/weapons:',len(front))
    print('Source gaps:',[(r['weaponClass'],r['kind'],r['issue']) for r in records if r['issue']])

if __name__=='__main__':main()
