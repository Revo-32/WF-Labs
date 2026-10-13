"""Verify pinned bytes and normalization, without contacting the network or executing Lua."""
import hashlib,json,pathlib,re,sys
root=pathlib.Path(__file__).resolve().parents[1]
folder=root/'data/combat-expansion-sources'
manifest=json.loads((folder/'manifest.json').read_text(encoding='utf-8'))
records=[]
for source in manifest:
    if source.get('missing'):continue
    raw=(folder/source['file']).read_bytes()
    assert hashlib.sha256(raw).hexdigest()==source['sha256'],source['title']
    if source['url'].startswith('https://wiki.warframe.com/'):
        assert str(source['revision']) in source['url']
    else:
        # Official patch notes have release identifiers, not MediaWiki IDs.
        assert re.fullmatch(r'https://www.warframe.com/en/patch-notes/pc/\d+-\d+-\d+',source['url']),source['title']
    records.append(source)
assert len({r['title'] for r in records})==len(records),'Duplicate manifest title'
report=dict(passed=True,sourceCount=len(records),sources=records,note='원문 해시와 고정 개정 URL 검사. 모든 원문 내용을 구현했다는 뜻이 아닙니다.')
if len(sys.argv)>1:pathlib.Path(sys.argv[1]).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'고정 원문 {len(records)}개 해시·개정 확인')
