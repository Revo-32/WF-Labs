"""Exercise release gates with deliberately broken private copies of a built package."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
from zipfile import ZIP_DEFLATED, ZipFile

parser = argparse.ArgumentParser()
parser.add_argument('directory', type=Path)
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
source, output = args.directory.resolve(), args.output.resolve()
if output.exists():
    raise ValueError('기존 검수 자료를 보존합니다. 새 출력 폴더를 사용하세요.')
output.mkdir(parents=True)
results = []
for case, expected in [('missing-manifest', '최상위 실행기의 필수 sq.version 포함'),
                       ('different-payload', '설치·압축 배포본 내용 SHA-256 일치 · release-config.json')]:
    folder = output / case
    folder.mkdir()
    for item in source.iterdir():
        if not item.is_file() or item.name in ('SHA256SUMS.txt', 'WFLabs-win-Portable.zip'):
            continue
        # Never mutate a linked input. Only the private ZIP/checksum file is new.
        try:
            os.link(item, folder / item.name)
        except OSError:
            shutil.copy2(item, folder / item.name)
    with ZipFile(source / 'WFLabs-win-Portable.zip') as original, ZipFile(folder / 'WFLabs-win-Portable.zip', 'w', ZIP_DEFLATED, compresslevel=1) as broken:
        for item in original.infolist():
            if case == 'missing-manifest' and item.filename == 'current/sq.version':
                continue
            data = original.read(item)
            if case == 'different-payload' and item.filename == 'current/release-config.json':
                data += b'\n '
            broken.writestr(item, data)
    # Keep checksums valid so failure must come from the intended deeper gate.
    sums = []
    for line in (source / 'SHA256SUMS.txt').read_text(encoding='utf-8-sig').splitlines():
        _, name = line.split('  ', 1)
        with (folder / name).open('rb') as stream:
            sums.append(hashlib.file_digest(stream, 'sha256').hexdigest() + '  ' + name)
    (folder / 'SHA256SUMS.txt').write_text('\n'.join(sums) + '\n', encoding='utf-8')
    run = subprocess.run([sys.executable, '-X', 'utf8', str(Path(__file__).with_name('verify-package.py')), str(folder), '--report', str(folder / 'unexpected-success.json')], capture_output=True, text=True, encoding='utf-8', timeout=120)
    (folder / 'stdout.txt').write_text(run.stdout, encoding='utf-8')
    (folder / 'stderr.txt').write_text(run.stderr, encoding='utf-8')
    passed = run.returncode != 0 and expected in run.stderr and not (folder / 'unexpected-success.json').exists()
    results.append({'case': case, 'passed': passed, 'exitCode': run.returncode, 'expectedRejection': expected})
report = {'passed': all(item['passed'] for item in results), 'checks': results, 'inputChanged': False}
(output / 'checks.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False))
raise SystemExit(0 if report['passed'] else 1)
