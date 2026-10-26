"""Reproducible local Korean OCR fine-tuning. Images from one source stay together."""
from __future__ import annotations
import argparse, concurrent.futures, hashlib, json, os, pathlib, random, re, subprocess, time, urllib.request
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageOps

ROOT = pathlib.Path(__file__).resolve().parents[1]
WORK = pathlib.Path('E:/CODEX/Workspaces/WFLabsOcrTraining')
BIN = WORK / 'tesseract'
BASE = ROOT / 'src/TennoForge.Desktop/Assets/Ocr'
REAL = [
 ('innodem','train','artifacts/riven-auto-2026-10-25/innodem-real.png',1000,[
  ((364,305,562,324),'+118.8% 상태 이상 지속'),((437,324,488,343),'시간'),
  ((364,344,563,365),'+108.1% 상태 이상 확률'),((394,365,533,384),'+106.4% 화염'),
  ((370,384,559,403),'슬라이드 공격에 치명타'),((391,402,539,421),'확률 -100% 추가')]),
 ('hirudo','train','artifacts/riven-range-2026-10-25/hirudo.png',1000,[
  ((812,335,945,351),'+101.1% 전기'),((811,351,947,368),'+63.9% 공격 속도'),
  ((837,369,921,382),'+2 사거리'),((786,384,957,401),'코퍼스에 대한 피해 x0.59')]),
 ('fang','test','artifacts/riven-labels-2026-10-25/fang.png',1000,[
  ((750,342,903,360),'+141.8% 상태 이상 피해'),((763,357,873,375),'+140.7% 전기'),
  ((742,372,902,391),'-76.1% 마무리 일격 피해')]),
 ('siam','validation','artifacts/riven-refresh-2026-10-25/siam.png',1000,[
  ((691,322,825,335),'+110.3% 치명타 피해'),((693,337,838,349),'+108.6% 상태 이상 피해'),
  ((708,350,814,363),'+95.2% 전기'),((688,365,825,378),'인페스티드에 대한 피해'),((737,378,780,392),'x0.58')]),
 ('wrapped','train','artifacts/relic-wrapped-2026-10-25/wrapped-orthos-rewards.png',1000,[
  ((222,182,336,200),'오르도스 프라임'),((248,198,327,216),'블레이드'),
  ((368,196,514,216),'듀얼 조런 프라임 핸들'),((521,196,675,216),'사로팡 프라임 핸들'),
  ((698,196,836,216),'세도 프라임 배럴')]),
 ('relic-live','test','artifacts/relic-auto-live-2026-10-25-02/reward-capture.png',1000,[
  ((313,166,404,181),'아쏘다이 프라임 설계도'),((410,166,499,181),'브롱코 프라임 리시버'),
  ((506,166,593,181),'패리스 프라임 설계도'),((599,166,683,181),'사로팡 프라임 핸들')]),
]

def save_sample(image, text, group, split, name, rows, **meta):
    folder=WORK/'dataset'/split;folder.mkdir(parents=True,exist_ok=True)
    path=folder/(name+'.png');image.save(path)
    path.with_suffix('.gt.txt').write_text(text+'\n',encoding='utf-8')
    w,h=image.size
    path.with_suffix('.box').write_text(''.join(f'{c} 0 0 {w} {h} 0\n' for c in text)+f'\t 0 0 {w} {h} 0\n',encoding='utf-8')
    rows.append(dict(image=str(path),text=text,group=group,split=split,**meta))

def text_mask(image, gold=False):
    out=Image.new('L',image.size,255)
    pixels=[]
    for r,g,b in image.convert('RGB').getdata():
        hi=max(r,g,b);lo=min(r,g,b)
        ink=(r>75 and r-g>10 and b-r>5) or (hi>140 and hi-lo<35)
        if gold:ink=(r>100 and g>75 and r>b*1.2) or (hi>170 and hi-lo<30)
        pixels.append(255-hi if ink else 255)
    out.putdata(pixels)
    return out

def prepare():
    rng=random.Random(20261025);rows=[];real_sheet=[]
    for group,split,source,preview_width,annotations in REAL:
        image=Image.open(ROOT/source);scale=image.width/preview_width
        for i,(box,text) in enumerate(annotations):
            b=tuple(round(v*scale) for v in box);crop=image.crop(b)
            real_sheet.append((group+': '+text,crop))
            gray=text_mask(crop,gold=group in ('wrapped','relic-live'))
            for j,factor in enumerate(([1,.8,1.2,1.5] if split=='train' else [1])):
                small=gray.resize((max(1,round(gray.width*factor)),max(1,round(gray.height*factor))),Image.Resampling.LANCZOS)
                small=ImageOps.expand(small,border=10,fill=255)
                save_sample(small,text,group,split,f'real-{group}-{i}-{j}',rows,source=source,sourceSha256=hashlib.sha256((ROOT/source).read_bytes()).hexdigest(),box=b,synthetic=False)
    fonts=[WORK/'NotoSansCJKkr-Regular.otf',pathlib.Path('C:/Windows/Fonts/malgun.ttf'),ROOT/'src/TennoForge.Desktop/Fonts/Pretendard-Regular.ttf']
    catalog=json.loads((ROOT/'data/relic-market-items.json').read_text('utf-8'))
    # Keep named groups independent of all real evaluation screenshots.
    def strings(node):
        if isinstance(node,dict):
            for k,v in node.items():
                if k in ('name','nameKo','koreanName') and isinstance(v,str) and re.search('[가-힣]',v):yield v
                elif isinstance(v,(dict,list)):yield from strings(v)
        elif isinstance(node,list):
            for v in node:yield from strings(v)
    names=list(dict.fromkeys(strings(catalog)))
    labels=['치명타 확률','치명타 피해','상태 이상 확률','상태 이상 피해','상태 이상 지속 시간','공격 속도','근접 피해','마무리 일격 피해','연속기 지속 시간','슬라이드 공격에 치명타 확률','화염','냉기','전기','독성','기본 연속기','두께 꿰뚫기','탄약 최대량','확대율','멀티샷','사거리','재장전 속도','그리니어에 대한 피해','코퍼스에 대한 피해','인페스티드에 대한 피해']
    for i in range(900):
        split='test' if i%12==0 else 'validation' if i%6==0 else 'train'
        if i%3==0 and names:text=rng.choice(names)
        else:
            label=rng.choice(labels);value=f'{rng.randint(1,280)}.{rng.randint(0,9)}'
            text=rng.choice(['+','-'])+value+'% '+label
            if label=='사거리':text=f'+{rng.randint(1,4)}.{rng.randint(0,9)} 사거리'
            elif label=='연속기 지속 시간':text=f'연속기 지속 시간 +{rng.randint(1,12)}초'
            elif '대한 피해' in label:text=label+' x'+rng.choice(['0.58','0.59','0.71','1.24','1.42'])
        font=ImageFont.truetype(str(fonts[i%len(fonts)]),rng.randint(23,39))
        box=font.getbbox(text);im=Image.new('L',(box[2]-box[0]+24,box[3]-box[1]+24),255)
        ImageDraw.Draw(im).text((12-box[0],12-box[1]),text,font=font,fill=rng.randrange(0,65))
        if i%4==0:im=im.filter(ImageFilter.GaussianBlur(.3))
        save_sample(im,text,'synthetic-'+str(i),split,f'synth-{i:04}',rows,synthetic=True,font=fonts[i%len(fonts)].name)
    (WORK/'dataset/manifest.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
    sheet=Image.new('RGB',(1100,len(real_sheet)*90),'#222222');draw=ImageDraw.Draw(sheet)
    font=ImageFont.truetype(str(fonts[0]),17)
    for i,(text,im) in enumerate(real_sheet):
        draw.text((8,i*90),text,font=font,fill='white');im.thumbnail((1050,57));sheet.paste(im,(8,i*90+26))
    sheet.save(WORK/'real-ground-truth.png')
    print(json.dumps({'train':sum(r['split']=='train' for r in rows),'test':sum(r['split']=='test' for r in rows),'real_sources':len(REAL),'names':len(names)}))

def charset():
    tools=WORK/'tesseract550';data=WORK/'langdata';data.mkdir(exist_ok=True)
    for name in ['Hangul.unicharset','Latin.unicharset','Han.unicharset','radical-stroke.txt']:
        path=data/name
        if not path.exists():path.write_bytes(urllib.request.urlopen('https://raw.githubusercontent.com/tesseract-ocr/langdata_lstm/main/'+name,timeout=30).read())
    chars=''.join(chr(i) for i in range(0xAC00,0xD7A4))+''.join(chr(i) for i in range(33,127))+'×−'
    (WORK/'all-characters.box').write_text(''.join(f'{c} 0 0 100 40 0\n' for c in chars)+'\t 0 0 100 40 0\n',encoding='utf-8',newline='\n')
    subprocess.run([str(BIN/'combine_tessdata.exe'),'-e',str(BASE/'kor.traineddata'),str(WORK/'kor.lstm-unicharset')],check=True)
    (WORK/'starter/wfkor').mkdir(parents=True,exist_ok=True)
    commands=[['unicharset_extractor.exe','--norm_mode','2','--output_unicharset',str(WORK/'new.unicharset'),str(WORK/'all-characters.box')],['merge_unicharsets.exe',str(WORK/'kor.lstm-unicharset'),str(WORK/'new.unicharset'),str(WORK/'merged.unicharset')],['combine_lang_model.exe','--input_unicharset',str(WORK/'merged.unicharset'),'--script_dir',str(data),'--output_dir',str(WORK/'starter'),'--lang','wfkor']]
    for command in commands:subprocess.run([str(tools/command[0]),*command[1:]],check=True)

def train(args):
    run=WORK/args.run;run.mkdir(parents=True,exist_ok=True)
    starter=WORK/'starter/wfkor/wfkor.traineddata'
    if not starter.exists():charset()
    base=WORK/'kor.lstm'
    if not base.exists():subprocess.run([str(BIN/'combine_tessdata.exe'),'-e',str(BASE/'kor.traineddata'),str(base)],check=True)
    rows=(WORK/'train.list').read_text().splitlines();real=[p for p in rows if '/real-' in p]
    listing=run/'training.list';listing.write_text('\n'.join(rows+real*(args.real_weight-1))+'\n',encoding='utf-8',newline='\n')
    command=[str(BIN/'lstmtraining.exe'),'--continue_from',args.resume or str(base),'--traineddata',str(starter),'--model_output',str(run/'wfkor'),'--train_listfile',str(listing),'--eval_listfile',str(WORK/'validation.list'),'--learning_rate',str(args.learning_rate),'--max_iterations',str(args.iterations),'--target_error_rate','0.01','--max_image_MB','1024']
    if not args.resume:command+=['--old_traineddata',str(BASE/'kor.traineddata')]
    (run/'command.json').write_text(json.dumps(command,indent=2),encoding='utf-8')
    with (run/'training.log').open('w',encoding='utf-8') as log:
        subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,env={**os.environ,'OMP_THREAD_LIMIT':'2'},check=True)
    if 'Encoding of string failed' in (run/'training.log').read_text('utf-8'):raise RuntimeError('학습 문자 집합 누락: 모델을 배포하지 마세요.')
    subprocess.run([str(BIN/'lstmtraining.exe'),'--stop_training','--continue_from',str(run/'wfkor_checkpoint'),'--traineddata',str(starter),'--model_output',str(run/'wfkor.traineddata')],check=True)

def convert():
    rows=json.loads((WORK/'dataset/manifest.json').read_text('utf-8'))
    def one(row):
        p=pathlib.Path(row['image']);out=p.with_suffix('')
        run=subprocess.run([str(BIN/'tesseract.exe'),str(p),str(out),'--tessdata-dir',str(BASE),'-l','kor','--psm','7',str(BIN/'tessdata/configs/lstm.train')],capture_output=True,text=True,encoding='utf-8',errors='replace',env={**os.environ,'OMP_THREAD_LIMIT':'1'})
        if run.returncode or not p.with_suffix('.lstmf').exists():raise RuntimeError(str(p)+': '+run.stderr)
        return str(p.with_suffix('.lstmf'))
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:results=list(pool.map(one,rows))
    for split in ['train','validation','test']:
        (WORK/f'{split}.list').write_text('\n'.join(p.replace('\\','/') for p,r in zip(results,rows) if r['split']==split)+'\n',encoding='utf-8',newline='\n')
    print('lstmf',len(results))

def distance(a,b):
    prev=list(range(len(b)+1))
    for i,x in enumerate(a,1):
        curr=[i]
        for j,y in enumerate(b,1):curr.append(min(curr[-1]+1,prev[j]+1,prev[j-1]+(x!=y)))
        prev=curr
    return prev[-1]

def evaluate(model,output,split):
    rows=[r for r in json.loads((WORK/'dataset/manifest.json').read_text('utf-8')) if r['split']==split]
    folder=pathlib.Path(model).parent;language=pathlib.Path(model).stem
    def one(row):
        start=time.perf_counter()
        run=subprocess.run([str(BIN/'tesseract.exe'),row['image'],'stdout','--tessdata-dir',str(folder),'-l',language,'--psm','7'],capture_output=True,text=True,encoding='utf-8',errors='replace',env={**os.environ,'OMP_THREAD_LIMIT':'1'})
        actual=run.stdout.strip();expected=row['text'];a=re.sub(r'\s','',actual).replace('×','x').replace('−','-');e=re.sub(r'\s','',expected).replace('×','x').replace('−','-')
        return {**row,'actual':actual,'errors':distance(a,e),'characters':len(e),'exact':a==e,'milliseconds':(time.perf_counter()-start)*1000}
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:results=list(pool.map(one,rows))
    summary={}
    for name,subset in [('all',results),('real',[r for r in results if not r['synthetic']]),('synthetic',[r for r in results if r['synthetic']])]:
        summary[name]={'lines':len(subset),'cer':sum(r['errors'] for r in subset)/max(1,sum(r['characters'] for r in subset)),'exact':sum(r['exact'] for r in subset)}
    report={'model':str(model),'sha256':hashlib.sha256(pathlib.Path(model).read_bytes()).hexdigest(),'summary':summary,'results':results}
    pathlib.Path(output).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(summary))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','convert','eval','charset','train']);p.add_argument('--model');p.add_argument('--output');p.add_argument('--split',default='test',choices=['train','validation','test']);p.add_argument('--run',default='run-new');p.add_argument('--resume');p.add_argument('--iterations',type=int,default=4000);p.add_argument('--learning-rate',type=float,default=.0001);p.add_argument('--real-weight',type=int,default=5);a=p.parse_args()
    if a.command=='prepare':prepare()
    elif a.command=='convert':convert()
    elif a.command=='charset':charset()
    elif a.command=='train':train(a)
    else:evaluate(a.model,a.output,a.split)
