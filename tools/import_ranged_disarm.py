"""Resolve curated unit behavior to pinned internal IDs, never runtime name matching."""
import json
from ranged_source_lib import ROOT,SOURCES
catalog=json.loads((ROOT/'data/enemy-catalog.json').read_text(encoding='utf-8'))
armed={'Heavy Gunner','Lancer','Trooper','Elite Lancer','Bombard','Kuva Lancer','Kuva Heavy Gunner','Crewman','MOA','Elite Crewman','Corpus Tech','Amalgam MOA','Corrupted Lancer','Corrupted Crewman','Corrupted Bombard','Corrupted Heavy Gunner','Scaldra Jaeger'}
immune={'Butcher','Prod Crewman','Charger','Deimos Charger','Lumbering Fragment','Anarch Arcus','Anarch Gladius'}
visual={'Runner','Leaper','Deimos Runner'}
rows=[dict(enemyId=e['id'],canDisarm=True if e['englishName'] in armed|visual else False if e['englishName'] in immune else None,visualOnly=e['englishName'] in visual,source=SOURCES['Disarm']) for e in catalog['enemies']]
(ROOT/'data/ranged-disarm.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Disarm target mappings:',len(rows))
