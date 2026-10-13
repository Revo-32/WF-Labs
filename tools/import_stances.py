"""Import selected stance tables from a fixed WF Wiki revision without executing Lua."""
import hashlib
import json
import pathlib
import re
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]
REVISION = 2815256
URL = f'https://wiki.warframe.com/api.php?action=query&prop=revisions&revids={REVISION}&rvprop=ids|timestamp|content&rvslots=main&format=json&formatversion=2'
NAMES = {'Blind Justice': '블라인드 저스티스', 'Decisive Judgement': '디사이시브 저지먼트', 'Tranquil Cleave': '트랭퀼 클리브', 'Sovereign Outcast': '소버린 아웃캐스트', 'Gemini Cross': '제미니 크로스', 'Tempo Royale': '템포 로얄', 'Cleaving Whirlwind': '클리빙 휠윈드', 'Rending Crane': '렌딩 크레인'}
TOKEN = re.compile(r'\s+|--[^\n]*|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|-?\d+(?:\.\d+)?|[A-Za-z_][A-Za-z_0-9]*|[{}\[\],=;]')

class Parser:
    def __init__(self, text):
        self.tokens = []
        pos = 0
        while pos < len(text):
            m = TOKEN.match(text, pos)
            if not m: raise ValueError(f'Unsupported Lua syntax at {pos}: {text[pos:pos+20]}')
            token = m.group(); pos = m.end()
            if not token.isspace() and not token.startswith('--'): self.tokens.append(token)
        self.i = 0
    def pop(self, expected=None):
        v = self.tokens[self.i]; self.i += 1
        if expected is not None and v != expected: raise ValueError(f'{v} != {expected}')
        return v
    def value(self):
        t = self.pop()
        if t == '{':
            arr, obj = [], {}
            while self.tokens[self.i] != '}':
                if self.tokens[self.i] == '[':
                    self.pop('['); key = self.value(); self.pop(']'); self.pop('='); obj[key] = self.value()
                elif self.i + 1 < len(self.tokens) and self.tokens[self.i+1] == '=':
                    key = self.pop(); self.pop('='); obj[key] = self.value()
                else: arr.append(self.value())
                if self.tokens[self.i] in [',',';']: self.pop()
            self.pop('}')
            if arr and obj: raise ValueError('Mixed indexed/mapped Lua table requires explicit handling')
            return obj if obj else arr
        if t.startswith('"'): return json.loads(t)
        if t.startswith("'"): return t[1:-1]
        if re.fullmatch(r'-?\d+(?:\.\d+)?', t): return float(t) if '.' in t else int(t)
        if t == 'true': return True
        if t == 'false': return False
        return f'unresolved:{t}'

def entry(text, name):
    start = text.index('{', text.index('["'+name+'"] ='))
    level, quote, escape = 0, None, False
    for p in range(start,len(text)):
        c=text[p]
        if quote:
            if escape: escape=False
            elif c=='\\': escape=True
            elif c==quote: quote=None
        elif c in ['"',"'"]: quote=c
        elif c=='{': level+=1
        elif c=='}':
            level-=1
            if level==0: return Parser(text[start:p+1]).value()
    raise ValueError('Unclosed table')

def main():
    req=urllib.request.Request(URL,headers={'User-Agent':'WFLabs/0.2 (local research prototype)'})
    response=json.load(urllib.request.urlopen(req,timeout=30))
    rev=response['query']['pages'][0]['revisions'][0]
    assert rev['revid']==REVISION
    text=rev['slots']['main']['content']
    sources={name:entry(text,name) for name in NAMES}
    stances=[]
    localization=json.loads((ROOT/'data/localization-ko.json').read_text(encoding='utf-8'))
    for name,s in sources.items():
        combos=[]
        for key in ['Neutral','Forward','Block','Forward Block']:
            c=s[key]; hits=[]; issues=[]
            for group,a in enumerate(c['Attacks']):
                assert len(a['Dmg'])==len(a['Hits'])
                known={'Dmg','Hits','Procs','Types','ImpactMultiplier','PunctureMultiplier','SlashMultiplier','StatusMultiplier','Shape'}
                issues.extend('추가 규칙: '+k for k in a if k not in known)
                for i,(damage,count) in enumerate(zip(a['Dmg'],a['Hits'])):
                    def at(field,default):
                        v=a.get(field,[])
                        return v[i] if i<len(v) else default
                    procs=at('Procs',[]); procs=[procs] if isinstance(procs,str) else procs
                    procs=[p for p in procs if p]
                    kind=at('Types','')
                    if kind not in ['', '360','Sweep']: issues.append('별도 공격 처리: '+kind)
                    if any(p.startswith('unresolved:') for p in procs): issues.append('원본의 미해결 식별자')
                    hits.append(dict(group=group+1,multiplier=damage/100,count=count,impact=at('ImpactMultiplier',1),puncture=at('PunctureMultiplier',1),slash=at('SlashMultiplier',1),statusMultiplier=at('StatusMultiplier',1),forcedProcs=procs,attackType=kind))
            combos.append(dict(key=key,name=c['Name'],duration=c.get('Duration'),hits=hits,unsupported=sorted(set(issues))))
        korean=localization['records'][localization['stanceIds'][name]]['name']
        stances.append(dict(id=name,name=korean,weaponType=s['WeaponType'],source='https://wiki.warframe.com/w/'+name.replace(' ','_'),combos=combos))
    folder=ROOT/'data'; folder.mkdir(exist_ok=True)
    def save(name,obj): (folder/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
    save('stances.json',dict(revision=REVISION,revisionTime=rev['timestamp'],retrievedAt='2026-10-10',source=f'https://wiki.warframe.com/w/Module:Stances/data?oldid={REVISION}',authority='WF Wiki 기준 사양',sourceSha256=hashlib.sha256(text.encode()).hexdigest(),stances=stances))
    save('stance-source-records.json',sources)
    print(f'Imported {len(stances)} stances / {sum(len(s["combos"]) for s in stances)} combos, revision {REVISION}')

if __name__=='__main__': main()
