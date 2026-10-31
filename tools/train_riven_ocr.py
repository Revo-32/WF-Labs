"""리벤 OCR의 고정 특징 추출·CTC 분류기 재학습·ONNX 내보내기.

공개 스크린샷은 재배포하지 않습니다. corpus manifest의 card 파일을 로컬에
확보한 뒤 실행합니다. 시험 split은 cache/train에서 열지 않습니다.
"""
from pathlib import Path
import argparse, hashlib, json, os, re, time, random, ctypes
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageOps
import onnx
import onnxruntime as ort
from onnx import helper, TensorProto, numpy_helper

BASE_SHA = 'cd6e2ea50f6943ca7271eb8c56a877a5a90720b7047fe9c41a2e541a25773c9b'
WEIGHT, BIAS = 'linear_8.w_0', 'linear_8.b_0'


def write(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf8')


def graph(path):
    if hashlib.sha256(Path(path).read_bytes()).hexdigest() != BASE_SHA:
        raise ValueError('검증된 기반 ONNX 모델의 SHA-256과 다릅니다.')
    return onnx.load(path)


def edit(a, b):
    prev = list(range(len(b) + 1))
    for i, x in enumerate(a, 1):
        cur = [i]
        for j, y in enumerate(b, 1):
            cur.append(min(cur[-1] + 1, prev[j] + 1, prev[j - 1] + (x != y)))
        prev = cur
    return prev[-1]


def normalize(text):
    return re.sub(r'\s', '', text).replace('×', 'x').replace('−', '-')


def mask(image):
    a = np.asarray(image.convert('RGB')).astype(np.int16)
    r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    h, w = r.shape
    region = np.s_[int(h*.52):int(h*.83), int(w*.15):int(w*.85)]
    floor = max(8, max(20, float(np.percentile((b-g)[region], 93))) * .25)
    red_floor = max(28, float(np.percentile(r[region], 93)) * .55)
    hi, lo = a.max(2), a.min(2)
    neutral = (hi > 60) & (hi-lo < 22)
    ink = neutral | ((r > red_floor) & (r-g > 3) & (b-r > 3) & (b-g > floor))
    strength = np.where(neutral, (hi-35)*4, (b-g-6)*8)
    return Image.fromarray(np.where(ink, 255-np.clip(strength, 0, 255), 255).astype(np.uint8))


def pixels(image):
    image = image.convert('RGB')
    rw = min(3200, int(np.ceil(48 * image.width / image.height)))
    x = np.zeros((1, 3, 48, max(320, rw)), np.float32)
    x[0, :, :, :rw] = np.asarray(image.resize((rw, 48), Image.Resampling.BILINEAR))[:, :, ::-1].transpose(2, 0, 1)/127.5-1
    return x


def cache(args):
    corpus = json.loads(args.manifest.read_text('utf8'))
    items = {i['id']: i for i in corpus['items']}
    rows = corpus['trainingRows']
    if any(r['split'] not in ('train', 'validation') for r in rows):
        raise ValueError('학습 특징에 시험 split이 들어 있습니다.')
    groups = {s: {items[r['card']]['sourceSha256'] for r in rows if r['split'] == s} for s in ('train', 'validation')}
    if groups['train'] & groups['validation']:
        raise ValueError('같은 원본 이미지가 학습과 검증에 중복되었습니다.')
    g = graph(args.base)
    chars = [''] + next(p.value for p in g.metadata_props if p.key == 'character').rstrip('\r\n').split('\n') + [' ']
    feature = next(n for n in g.graph.node if WEIGHT in n.input).input[0]
    del g.graph.output[:]
    g.graph.output.append(helper.make_tensor_value_info(feature, TensorProto.FLOAT, [None, None, 120]))
    options = ort.SessionOptions()
    options.intra_op_num_threads = 2
    options.inter_op_num_threads = 1
    options.add_session_config_entry('session.intra_op.allow_spinning', '0')
    session = ort.InferenceSession(g.SerializeToString(), options, providers=['CPUExecutionProvider'])
    args.output.mkdir(parents=True, exist_ok=True)
    index = {c: i for i, c in enumerate(chars)}
    samples, start = [], time.perf_counter()

    def add(image, text, split, card, view):
        text = normalize(text)
        if any(c not in index for c in text):
            raise ValueError('기반 모델에 없는 문자: ' + text)
        values = session.run(None, {session.get_inputs()[0].name: pixels(image)})[0][0]
        path = args.output / f'{len(samples):05}.npy'
        np.save(path, values)
        samples.append(dict(file=str(path.resolve()), text=text, target=[index[c] for c in text], split=split, card=card, view=view))

    images = {}
    for row in rows:
        card = row['card']
        if card not in images:
            source = args.images / items[card]['file']
            if hashlib.sha256(source.read_bytes()).hexdigest() != items[card]['sha256']:
                raise ValueError('원본 카드 해시 불일치: ' + card)
            image = Image.open(source).convert('RGB')
            images[card] = (image, mask(image))
        original, separated = images[card]
        crop = original.crop(row['box'])
        for view, image in [('inverse', ImageOps.invert(crop)), ('mask', separated.crop(row['box'])), ('raw', crop)]:
            add(image, row['text'], row['split'], card, row.get('geometry', 'native') + '-' + view)
    rng = random.Random(args.seed)
    fonts = args.font or [Path('C:/Windows/Fonts/malgun.ttf')]
    for i in range(args.synthetic):
        label = rng.choice(corpus['vocabulary'])
        value = str(rng.randrange(350)) + '.' + str(rng.randrange(10))
        text = rng.choice(['+', '-']) + value + '% ' + label
        if '에 대한 피해' in label:
            text = label + ' x' + str(rng.randrange(2)) + '.' + str(rng.randrange(10)) + str(rng.randrange(10))
        if rng.random() < .15:
            text = rng.choice(['+', '-', 'x']) + value + rng.choice(['%', '', 'm'])
        font = ImageFont.truetype(str(rng.choice(fonts)), rng.randrange(18, 34))
        box, pad = font.getbbox(text), rng.randrange(1, 5)
        image = Image.new('RGB', (box[2]-box[0]+pad*2, box[3]-box[1]+pad*2), 'white')
        ImageDraw.Draw(image).text((pad-box[0], pad-box[1]), text, font=font, fill=(rng.randrange(70),)*3)
        add(image, text, 'train', 'synthetic', 'synth')
    write(args.output/'manifest.json', dict(seed=args.seed, characters=chars, samples=samples, vocabulary=corpus['vocabulary'],
        corpusSha256=hashlib.sha256(args.manifest.read_bytes()).hexdigest(), frozenTestUsed=False, seconds=time.perf_counter()-start))
    print(f'특징 추출 완료: {len(samples)}개 · 시험 이미지 사용 0개')


def fit(args):
    import torch
    from torch import nn
    torch.set_num_threads(2)
    torch.manual_seed(args.seed)
    rng = random.Random(args.seed)
    data = json.loads(args.features.read_text('utf8'))
    chars, rows = data['characters'], data['samples']
    if any(r['split'] not in ('train', 'validation') for r in rows):
        raise ValueError('시험 split은 학습·선택에 사용할 수 없습니다.')
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    for r in rows:
        r['feature'] = torch.tensor(np.load(r['file']), device=device)
        r['targetTensor'] = torch.tensor(r['target'], dtype=torch.long, device=device)
    real = [r for r in rows if r['split'] == 'train' and r['card'] != 'synthetic']
    synth = [r for r in rows if r['view'] == 'synth']
    validation = [r for r in rows if r['split'] == 'validation']
    tensors = {x.name: numpy_helper.to_array(x) for x in graph(args.base).graph.initializer if x.name in (WEIGHT, BIAS)}
    model = nn.Linear(120, len(chars)).to(device)
    with torch.no_grad():
        model.weight.copy_(torch.tensor(tensors[WEIGHT].copy().T, device=device))
        model.bias.copy_(torch.tensor(tensors[BIAS].copy(), device=device))
    if args.resume:
        source = np.load(args.resume)
        with torch.no_grad():
            model.weight.copy_(torch.tensor(source['weight'].T, device=device))
            model.bias.copy_(torch.tensor(source['bias'], device=device))
    weight0, bias0 = model.weight.detach().clone(), model.bias.detach().clone()
    optimizer = torch.optim.Adam(model.parameters(), lr=args.learning_rate)
    loss_fn = nn.CTCLoss(blank=0, zero_infinity=True)
    metrics, hard, best = [], [], (float('inf'),)*3
    vocab = [normalize(v) for v in data.get('vocabulary', [])]
    if not vocab and args.manifest:
        vocab = [normalize(v) for v in json.loads(args.manifest.read_text('utf8'))['vocabulary']]
    args.output.mkdir(parents=True, exist_ok=True)

    def canonical(text):
        number = re.compile(r'(?:[+−-]\d+(?:[.,]\d+)?%?|[xX×]\d+(?:[.,]\d+)?)')
        label = number.sub('', text)
        matches = [v for v in vocab if len(v) >= 4 and len(label) >= 3 and edit(label, v) == 1]
        if label not in vocab and len(set(matches)) == 1:
            label = matches[0]
        return '|'.join(number.findall(text)) + '|' + label

    @torch.inference_mode()
    def predict(row):
        last, text = -1, ''
        for k in model(row['feature']).argmax(-1).tolist():
            if k and k != last:
                text += chars[k]
            last = k
        return text

    @torch.inference_mode()
    def evaluate(step):
        nonlocal best
        predictions = [dict(card=r['card'], view=r['view'], text=r['text'], actual=predict(r)) for r in validation]
        hard.clear()
        train_errors = 0
        for r in real:
            errors = edit(predict(r), r['text'])
            train_errors += errors
            if errors:
                hard.append(r)
        raw_errors = sum(edit(r['actual'], r['text']) for r in predictions)
        semantic_errors = sum(edit(canonical(r['actual']), canonical(r['text'])) for r in predictions)
        record = dict(step=step, errors=raw_errors, semanticErrors=semantic_errors, trainingErrors=train_errors, results=predictions)
        metrics.append(record)
        write(args.output/'metrics.json', metrics)
        score = (semantic_errors, train_errors, raw_errors)
        if score < best:
            best = score
            np.savez(args.output/'best-fc.npz', weight=model.weight.detach().cpu().numpy().T, bias=model.bias.detach().cpu().numpy())
            write(args.output/'best-validation.json', record)
        print(f'검증 {step}: 숫자·명칭 오류 {semantic_errors}, 학습 문자 오류 {train_errors}, 원문 문자 오류 {raw_errors}', flush=True)

    evaluate(0)
    for step in range(1, args.steps+1):
        batch = [rng.choice(hard if hard and rng.random() < .4 else real if rng.random() < .5 else synth) for _ in range(16)]
        lengths = [r['feature'].shape[0] for r in batch]
        x = torch.zeros(16, max(lengths), 120, device=device)
        for i, row in enumerate(batch):
            x[i, :lengths[i]] = row['feature']
        logits = model(x)
        loss = loss_fn(logits.log_softmax(-1).permute(1, 0, 2), torch.cat([r['targetTensor'] for r in batch]),
            torch.tensor(lengths), torch.tensor([len(r['target']) for r in batch]))
        loss = loss + .0002*((model.weight-weight0).square().sum()+(model.bias-bias0).square().sum())
        optimizer.zero_grad()
        loss.backward()
        nn.utils.clip_grad_norm_(model.parameters(), 5)
        optimizer.step()
        if step % 100 == 0 or step == args.steps:
            evaluate(step)
    write(args.output/'training-config.json', dict(seed=args.seed, steps=args.steps, learningRate=args.learning_rate, device=device,
        frozenBackbone=True, frozenSequenceEncoder=True, trainedClassifier=True, finalTestUsed=False,
        featureManifestSha256=hashlib.sha256(args.features.read_bytes()).hexdigest()))


def export(args):
    g = graph(args.base)
    source = np.load(args.weights)
    for i, x in enumerate(g.graph.initializer):
        if x.name in (WEIGHT, BIAS):
            values = source['weight' if x.name == WEIGHT else 'bias'].astype(np.float32)
            if list(values.shape) != list(x.dims):
                raise ValueError('분류기 가중치 shape 불일치')
            g.graph.initializer[i].CopyFrom(numpy_helper.from_array(values, name=x.name))
    old = {p.key: p.value for p in g.metadata_props}
    old.update(dict(characters=json.dumps(old['character'].rstrip('\r\n').split('\n')+[' '], ensure_ascii=False),
        architecture='PP-OCRv5 Korean CTC classifier fine-tune', height='48', baseSha256=BASE_SHA, classifierOnly='true'))
    helper.set_model_props(g, old)
    onnx.checker.check_model(g)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    onnx.save(g, args.output)
    original = graph(args.base)
    assert [n.SerializeToString() for n in original.graph.node] == [n.SerializeToString() for n in g.graph.node]
    assert all(a.SerializeToString() == b.SerializeToString() for a, b in zip(original.graph.initializer, g.graph.initializer) if a.name not in (WEIGHT, BIAS))
    print('ONNX 내보내기 완료 · SHA-256 ' + hashlib.sha256(args.output.read_bytes()).hexdigest())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['cache', 'fit', 'export'])
    parser.add_argument('--base', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--manifest', type=Path)
    parser.add_argument('--images', type=Path)
    parser.add_argument('--features', type=Path)
    parser.add_argument('--weights', type=Path)
    parser.add_argument('--resume', type=Path)
    parser.add_argument('--font', type=Path, action='append')
    parser.add_argument('--synthetic', type=int, default=1400)
    parser.add_argument('--seed', type=int, default=20261030)
    parser.add_argument('--steps', type=int, default=5400)
    parser.add_argument('--learning-rate', type=float, default=.0001)
    args = parser.parse_args()
    if os.name == 'nt':
        ctypes.windll.kernel32.SetPriorityClass(ctypes.c_void_p(-1), 0x4000)
    if args.command == 'cache' and not (args.manifest and args.images):
        parser.error('cache에는 --manifest와 --images가 필요합니다.')
    if args.command == 'fit' and not args.features:
        parser.error('fit에는 --features가 필요합니다.')
    if args.command == 'export' and not args.weights:
        parser.error('export에는 --weights가 필요합니다.')
    {'cache': cache, 'fit': fit, 'export': export}[args.command](args)


if __name__ == '__main__':
    main()
