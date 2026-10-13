"""Extract the first three part tables and two-handed factors from the pinned Zaw page."""
import hashlib,json,pathlib,re
from wiki_tables import parse
root=pathlib.Path(__file__).resolve().parents[1]
manifest=json.loads((root/'data/combat-expansion-sources/manifest.json').read_text(encoding='utf-8'))
source=next(x for x in manifest if x['title']=='Zaw')
raw=(root/'data/combat-expansion-sources'/source['file']).read_text(encoding='utf-8')
assert hashlib.sha256(raw.encode()).hexdigest()==source['sha256']
tables=re.findall(r'\{\|.*?\n\|\}',raw,re.S)
def rows(table):
    return [[line[1:].strip() for line in row.splitlines() if line.startswith('|') and not line.startswith('|}')]
        for row in table.split('|-') if "'''[[" in row]
def name(cell):
    value=re.search(r'\[\[(.*?)\]\]',cell)[1]
    return value.split('|')[-1].split('#')[-1]
def number(cell):
    m=re.search(r'[+-]?\d+(?:\.\d+)?',cell)
    return float(m[0]) if m else 0
strikes=[]
factors={'Balla':1,'Ooltha':1,'Sepfahn':1,'Plague Keewar':.85,'Cyath':1.08,'Dehtat':1.09,'Kronsh':1.07,'Mewan':1.09,'Plague Kripath':1.08,'Rabvee':1.08,'Dokrahm':.93}
for r in rows(tables[0]):
    def weapon(cell):return dict(type=cell.split(' (')[0],polarity=re.search(r'\{\{Pol\|(.*?)\}\}',cell)[1],range=number(cell.split(')')[-1]))
    strikes.append(dict(id=name(r[0]),one=weapon(r[1]),two=weapon(r[2]),damage=dict(zip(['Impact','Puncture','Slash','Viral'],map(number,r[4:8]))),speed=number(r[8]),criticalChance=number(r[9])/100,criticalMultiplier=number(r[10]),statusChance=number(r[11])/100,disposition=number(r[12]),twoHandedFactor=factors[name(r[0])]))
grips=[dict(id=name(r[0]),twoHanded='Two-handed' in r[1],damage=number(r[2]),speed=number(r[3])) for r in rows(tables[1])]
links=[dict(id=name(r[0]),speed=number(r[1]),criticalChance=number(r[2])/100,statusChance=number(r[3])/100,damage=number(r[4])) for r in rows(tables[2])]
assert (len(strikes),len(grips),len(links))==(11,10,16)
result=dict(version='wiki-zaw-'+str(source['revision']),source=source,strikes=strikes,grips=grips,links=links)
module_source=next(x for x in manifest if x['title']=='Module:Modular/data')
module_text=(root/'data/combat-expansion-sources'/module_source['file']).read_text(encoding='utf-8')
assert hashlib.sha256(module_text.encode()).hexdigest()==module_source['sha256']
module=parse(module_text)['Zaw']
for s in strikes:
    raw=module['Strike'][s['id']]
    assert s['twoHandedFactor']==raw['Multi']['TwoHanded']['Base']
    assert all(abs(s['damage'][k]-v)<.0001 for k,v in raw['Damage'].items())
    assert abs(s['speed']-raw['Speed'])<.0001
    assert s['criticalChance']==raw['CritChance'] and s['statusChance']==raw['StatusChance']
    for hand in ['one','two']:
        key='OneHanded' if hand=='one' else 'TwoHanded'
        assert s[hand]['type']==raw['Type'][key]
        s[hand]['slide']=raw['Multi'][key]['Slide']
result['moduleSource']=module_source
for g in grips:
    raw=module['Grip'][g['id']]
    assert g['speed']==raw['Speed'] and g['damage']==raw['Damage']
for l in links:
    raw=module['Link'][l['id']]
    assert all(abs(l[key]-raw[field])<.0001 for key,field in [('speed','Speed'),('damage','Damage'),('criticalChance','CritChance'),('statusChance','StatusChance')])
(root/'data/zaw-parts.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('조우 부품 11 / 10 / 16 · 조합 1760')
