"""고정된 수집 캐시에서 수용량 기본값과 출처를 생성한다."""
import argparse
import hashlib
import json
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('--cache',type=Path,required=True)
args=parser.parse_args()
root=Path(__file__).resolve().parents[1]
load=lambda p:json.loads(p.read_text(encoding='utf-8'))
catalog=load(root/'data/catalog.json')
localized=load(root/'data/localization-ko.json')
weapons={x['uniqueName']:x for x in load(args.cache/'Melee.json')}
polarities={'madurai':'AP_ATTACK','vazarin':'AP_DEFENSE','naramon':'AP_TACTIC'}
defaults={}
for w in catalog['weapons']:
    raw=weapons[w['id']]
    slots=[polarities[p] for p in raw.get('polarities',[])]
    defaults[w['id']]={'slotPolarities':slots+['None']*(8-len(slots)),'stanceSlotPolarity':polarities[raw['stancePolarity']]}
stances={}
for wiki_id,export_id in localized['stanceIds'].items():
    raw=localized['records'][export_id]
    stances[wiki_id]={'modId':export_id,'polarity':raw['polarity'],'maxRank':raw['fusionLimit'],'bonus':-raw['baseDrain']+raw['fusionLimit']}
sources=[]
for page in load(args.cache/'capacity-wiki-sources.json')['query']['pages'].values():
    revision=page['revisions'][0]
    text=revision['slots']['main']['*']
    selected=[line for line in text.splitlines() if any(k in line.lower() for k in ['round','capacity','yellow','orange'])]
    sources.append({'title':page['title'],'revision':revision['revid'],'timestamp':revision['timestamp'],'url':f"https://wiki.warframe.com/w/{page['title'].replace(' ','_')}?oldid={revision['revid']}",'sha256':hashlib.sha256(text.encode()).hexdigest(),'selectedText':selected})
output={'version':'capacity-0.4.0','weaponDefaults':defaults,'stanceMods':stances,'notes':['기본 슬롯은 원본의 극성 개수를 앞 슬롯부터 배치한 편집 초기값이며 실제 게임의 슬롯 좌표를 주장하지 않는다.','현재 스탠스 계산 및 수용량은 최대 랭크 기준이다.','일반 모드 불일치 비용은 최신 고정 Polarity revision 2814647의 rounded up을 적용한다. 과거 Fandom 사본의 가장 가까운 정수 설명과 다르다.']}
(root/'data/capacity-data.json').write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
evidence={'retrievedAt':'2026-10-10','wiki':sources,'weaponSource':{'url':'https://raw.githubusercontent.com/WFCD/warframe-items/4a73b2fb016d7a79ec0694aec42a66778d762288/data/json/Melee.json','sha256':hashlib.sha256((args.cache/'Melee.json').read_bytes()).hexdigest()},'stanceSource':localized['sources']['ExportUpgrades'],'notes':output['notes']}
(root/'data/capacity-source-records.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('무기',len(defaults),'스탠스',len(stances),'출처',len(sources))
