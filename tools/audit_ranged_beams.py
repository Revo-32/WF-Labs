"""Check normalized beam data against hash-verified pinned wiki statements."""
import json
from ranged_source_lib import ROOT, PAGES, SOURCES
from ranged_beam_source import beam_rule

rows = json.loads((ROOT / 'data/ranged-timing.json').read_text(encoding='utf-8'))['weapons']
checks = []
for row in rows:
    name = row['id'].split('/')[-1]
    expected = beam_rule(PAGES.get(name, ''))
    if expected is None:
        continue
    actual = {key: row.get(key, .6 if key == 'beamRampSeconds' else None) for key in expected}
    checks.append(dict(weapon=name, expected=expected, actual=actual,
                       passed=actual == expected, source=SOURCES[name]))
report = dict(passed=all(c['passed'] for c in checks), count=len(checks), checks=checks)
path = ROOT / 'artifacts/ranged-beam-source-audit.json'
path.parent.mkdir(exist_ok=True)
path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(dict(passed=report['passed'], count=report['count'])))
raise SystemExit(0 if report['passed'] else 1)
