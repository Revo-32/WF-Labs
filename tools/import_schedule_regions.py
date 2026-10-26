"""Normalize Korean node and mission labels from the existing public-export evidence."""
import pathlib,json,hashlib,datetime
root=pathlib.Path(__file__).resolve().parents[1]
source=root/'artifacts/worldstate-research'
def load(name): return json.loads((source/name).read_text(encoding='utf-8-sig'))
regions=load('ExportRegions.json');ko=load('dict.ko.json');en=load('dict.en.json')
nodes=[];missions={}
for id,r in regions.items():
    code=r.get('missionType','');label=ko.get(r.get('missionName',''),'')
    if label=='거울상 방어': code='MT_MIRROR_DEFENSE'
    if code and label: missions[code]=label
    nodes.append(dict(id=id,name=ko.get(r.get('name',''),id),englishName=en.get(r.get('name',''),id),planet=ko.get(r.get('systemName',''),'위치 확인 필요'),mission=code))
missions['MT_MIRROR_DEFENSE']=ko['/Lotus/Language/Missions/MissionName_DualDefense']
result=dict(nodes=nodes,missions=missions,sources=[dict(file=n,url='https://raw.githubusercontent.com/calamity-inc/warframe-public-export-plus/senpai/'+n,sha256=hashlib.sha256((source/n).read_bytes()).hexdigest()) for n in ['ExportRegions.json','dict.ko.json','dict.en.json']],generatedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(root/'data/schedule-regions.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(f'{len(nodes)} nodes, {len(missions)} mission labels')
