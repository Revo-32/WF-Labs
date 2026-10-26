"""동일한 분리 검증 행으로 한국어 PP-OCRv5 인식 모델을 평가한다.

앱 의존성이 아닌 실험용 도구. CPU 두 스레드, 로컬 추론만 사용한다.
입력은 이미 잘린 글줄이며 탐지/카드 파싱 시간과 혼동하지 않는다.
"""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import time

os.environ.setdefault('OMP_NUM_THREADS', '2')
os.environ.setdefault('FLAGS_enable_pir_api', '1')

import numpy as np
from PIL import Image
import paddle.inference as infer


def distance(a, b):
    previous = list(range(len(b) + 1))
    for i, x in enumerate(a, 1):
        current = [i]
        for j, y in enumerate(b, 1):
            current.append(min(current[-1] + 1, previous[j] + 1, previous[j - 1] + (x != y)))
        previous = current
    return previous[-1]


def normalized(text):
    return re.sub(r'\s', '', text).replace('×', 'x').replace('−', '-')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--model', required=True)
    parser.add_argument('--manifest', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--split', default='test')
    args = parser.parse_args()
    model = Path(args.model)
    metadata = json.loads((model / 'config.json').read_text('utf-8'))
    characters = [''] + metadata['PostProcess']['character_dict'] + [' ']
    started = time.perf_counter()
    config = infer.Config(str(model / 'inference.json'), str(model / 'inference.pdiparams'))
    config.disable_gpu()
    config.set_cpu_math_library_num_threads(2)
    config.disable_mkldnn()
    config.disable_glog_info()
    predictor = infer.create_predictor(config)
    load_ms = (time.perf_counter() - started) * 1000
    input_handle = predictor.get_input_handle(predictor.get_input_names()[0])
    output_handle = predictor.get_output_handle(predictor.get_output_names()[0])

    def recognize(path):
        im = Image.open(path).convert('RGB')
        width = max(320, math.ceil(48 * im.width / im.height))
        resized_width = min(width, math.ceil(48 * im.width / im.height))
        resized = np.asarray(im.resize((resized_width, 48), Image.Resampling.BILINEAR))[:, :, ::-1]
        batch = np.zeros((1, 3, 48, width), dtype=np.float32)
        batch[0, :, :, :resized_width] = resized.transpose(2, 0, 1).astype(np.float32) / 127.5 - 1
        input_handle.reshape(batch.shape)
        input_handle.copy_from_cpu(batch)
        predictor.run()
        output = output_handle.copy_to_cpu()
        indices, confidences = output.argmax(axis=2)[0], output.max(axis=2)[0]
        text, scores, previous = [], [], -1
        for index, confidence in zip(indices, confidences):
            if index and index != previous:
                text.append(characters[index])
                scores.append(float(confidence))
            previous = index
        return ''.join(text), sum(scores) / max(1, len(scores))

    rows = [r for r in json.loads(Path(args.manifest).read_text('utf-8')) if r['split'] == args.split]
    results = []
    for row in rows:
        start = time.perf_counter()
        actual, confidence = recognize(row['image'])
        elapsed = (time.perf_counter() - start) * 1000
        expected = row['text']
        a, e = normalized(actual), normalized(expected)
        results.append({**row, 'actual': actual, 'confidence': confidence, 'milliseconds': elapsed,
                        'errors': distance(a, e), 'characters': len(e), 'exact': a == e})
    summary = {}
    for name, subset in [('all', results), ('real', [r for r in results if not r['synthetic']]),
                         ('synthetic', [r for r in results if r['synthetic']])]:
        summary[name] = dict(lines=len(subset), exact=sum(r['exact'] for r in subset),
                             cer=sum(r['errors'] for r in subset) / max(1, sum(r['characters'] for r in subset)),
                             meanMilliseconds=sum(r['milliseconds'] for r in subset) / max(1, len(subset)))
    report = dict(model='korean_PP-OCRv5_mobile_rec', paddleVersion=__import__('paddle').__version__,
                  source='https://huggingface.co/PaddlePaddle/korean_PP-OCRv5_mobile_rec',
                  sha256=hashlib.sha256((model / 'inference.pdiparams').read_bytes()).hexdigest(),
                  loadMilliseconds=load_ms, cpuThreads=2, summary=summary, results=results)
    Path(args.output).write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(summary, ensure_ascii=False))


if __name__ == '__main__':
    main()
