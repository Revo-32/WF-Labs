"""Extract class heavy rules from a pinned Wiki article without executing its markup."""
import hashlib,json,pathlib,re

root=pathlib.Path(__file__).resolve().parents[1]
directory=root/'data/combat-expansion-sources'
source=next(s for s in json.loads((directory/'manifest.json').read_text(encoding='utf-8')) if s['title']=='Melee')
text=(directory/source['file']).read_text(encoding='utf-8')
assert hashlib.sha256(text.encode()).hexdigest()==source['sha256']
table=text.split('The following table lists the damage values')[1].split('|}')[0]
classes=[]
for name,first,second,windup in re.findall(r'^\|\[\[(.*?)\]\] \|\| (.*?) \|\| (.*?) \|\| ([\d.]+)s',table,re.M):
    name=re.sub(r' \((?:Weapon Type|Melee)\)$','',name)
    # These are first/second animation totals, not individual hit multipliers.
    classes.append(dict(name=name,firstHeavyMultiplier=float(re.match(r'[\d.]+',first)[0]),
        secondHeavyMultiplier=float(re.match(r'[\d.]+',second.strip())[0]),windup=float(windup),
        firstHeavyText=first,secondHeavyText=second))
assert len(classes)==27
result=dict(source=source,classes=classes)
(root/'data/melee-classes.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'위키 무기군 강공격 표 {len(classes)}종 · 개정 {source["revision"]}')
