"""Original WF Labs notification: a short, soft three-note glass signal; no source samples."""
from pathlib import Path
import math,struct,wave,hashlib,json
root=Path(__file__).resolve().parents[1]
target=root/'src/TennoForge.Desktop/Assets/Audio/schedule-notification.wav'
target.parent.mkdir(parents=True,exist_ok=True)
rate=48000;duration=.74;samples=[]
for i in range(round(rate*duration)):
    t=i/rate;value=0
    for start,freq,length,level in [(0,440,.38,.21),(.13,660,.40,.18),(.28,880,.46,.16)]:
        age=t-start
        if 0<=age<length:
            envelope=(1-math.exp(-age/.008))*math.exp(-age/.105)*min(1,(length-age)/.04)
            tone=math.sin(2*math.pi*freq*age)+.16*math.sin(2*math.pi*freq*2.004*age)
            value+=level*envelope*tone
    samples.append(round(max(-1,min(1,value*1.6))*32767))
with wave.open(str(target),'wb') as stream:
    stream.setparams((1,2,rate,0,'NONE','not compressed'));stream.writeframes(struct.pack('<'+'h'*len(samples),*samples))
metadata=dict(title='WF Labs · Glass Signal',creator='WF Labs',source='Original additive synthesis; no third-party audio',sampleRate=rate,durationSeconds=duration,peak=max(abs(x) for x in samples)/32768,sha256=hashlib.sha256(target.read_bytes()).hexdigest())
target.with_suffix('.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps(metadata,ensure_ascii=False))
