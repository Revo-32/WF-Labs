"""Validate the release feed, checksums, x64 payload and bundled notices without installing."""
import argparse
import hashlib
import json
import struct
import xml.etree.ElementTree as ET
from pathlib import Path
from zipfile import ZipFile

parser = argparse.ArgumentParser()
parser.add_argument("directory", type=Path)
parser.add_argument("--report", type=Path, default=Path("artifacts/package-verification.json"))
args = parser.parse_args()
folder = args.directory.resolve()
checks = []

def require(value, message):
    if not value:
        raise ValueError(message)
    checks.append(message)

def x64(data):
    offset = struct.unpack_from("<I", data, 0x3C)[0]
    return data[:2] == b"MZ" and data[offset:offset+4] == b"PE\0\0" and struct.unpack_from("<H", data, offset+4)[0] == 0x8664

feed = json.loads((folder / "releases.win.json").read_text(encoding="utf-8-sig"))
asset = next(a for a in feed["Assets"] if a["Type"] == "Full" and a["PackageId"] == "WFLabs")
require(Path(asset["FileName"]).name == asset["FileName"], "업데이트 패키지 경로 형식")
package = folder / asset["FileName"]
require(package.stat().st_size == asset["Size"], "업데이트 feed의 파일 크기 일치")
with package.open("rb") as stream:
    require(hashlib.file_digest(stream, "sha256").hexdigest().lower() == asset["SHA256"].lower(), "업데이트 feed의 SHA-256 일치")
for line in (folder / "SHA256SUMS.txt").read_text(encoding="utf-8-sig").splitlines():
    expected, name = line.split("  ", 1)
    require(Path(name).name == name, "배포 파일 경로 형식 · " + name)
    with (folder / name).open("rb") as stream:
        require(hashlib.file_digest(stream, "sha256").hexdigest() == expected, "배포 파일 SHA-256 · " + name)
require(x64((folder / "WFLabs-win-Setup.exe").read_bytes()), "설치 프로그램 Windows x64")
with ZipFile(package) as archive:
    require(archive.testzip() is None, "전체 업데이트 패키지 ZIP 무결성")
    metadata = ET.fromstring(archive.read("WFLabs.nuspec"))
    value = lambda key: metadata.findtext(".//{*}" + key)
    require(value("rid") == "win-x64" and value("machineArchitecture") == "x64", "설치·업데이트 아키텍처 메타데이터")
    require(value("version") == asset["Version"], "패키지와 feed 버전 일치")
    for name in ["COPYRIGHT.md", "THIRD_PARTY_NOTICES.txt", "WF-WIKI-NOTICE.txt", "PUBLIC-EXPORT-NOTICE.txt", "WFCD-LICENSE.txt", "VELOPACK-LICENSE.txt", "Fonts/OFL.txt", "Fonts/PRETENDARD-LICENSE.txt", "Fonts/PRETENDARD-SOURCE.json", "Fonts/THE-JAMSIL-LICENSE.txt", "Fonts/The_Jamsil_LICENSE_Korean.pdf", "Fonts/THE-JAMSIL-SOURCE.json", "WFLabs.Startup.exe", "coreclr.dll", "PresentationFramework.dll"]:
        require("lib/app/" + name in archive.namelist(), "배포 구성요소 포함 · " + name)
    if 'lib/app/Data/world-schedule-sources.json' in archive.namelist():
        for name in ['BROWSE-WF-NOTICE.txt', 'PUBLIC-EXPORT-PLUS-LICENSE.txt', 'WF-WIKI-NOTICE.txt']:
            require('lib/app/' + name in archive.namelist(), '일정 자료 출처·라이선스 포함 · ' + name)
    config = json.loads(archive.read("lib/app/release-config.json").decode("utf-8-sig"))
    if tuple(map(int, asset['Version'].split('-', 1)[0].split('.')[:2])) >= (0, 10):
        for name in ['WARFRAME-MARKET-NOTICE.txt', 'Tesseract.dll', 'x64/tesseract50.dll', 'x64/leptonica-1.82.0.dll',
                     'Assets/Ocr/kor.traineddata', 'Assets/Ocr/eng.traineddata', 'Assets/Ocr/SOURCES.json',
                     'Assets/Ocr/TESSERACT-NET-LICENSE.txt', 'Assets/Ocr/TESSERACT-LICENSE.txt',
                     'Assets/Ocr/TESSDATA-LICENSE.txt', 'Assets/Ocr/LEPTONICA-LICENSE.txt']:
            require('lib/app/' + name in archive.namelist(), '성유물 인식 구성요소 포함 · ' + name)
    require(config["RepositoryUrl"] == "https://github.com/Revo-32/WF-Labs", "공개 업데이트 저장소 연결")
    if 'lib/app/Microsoft.ML.OnnxRuntime.dll' in archive.namelist():
        for name in ['Assets/Ocr/korean-line-v5.onnx', 'Assets/Ocr/korean-line-v5.model.json',
                     'Assets/Ocr/PADDLEOCR-LICENSE.txt', 'Assets/Ocr/RAPIDOCR-LICENSE.txt',
                     'Assets/Ocr/ONNXRUNTIME-LICENSE.txt', 'Assets/Ocr/ONNXRUNTIME-THIRD-PARTY-NOTICES.txt']:
            require('lib/app/' + name in archive.namelist(), '한국어 보조 인식 구성요소 포함 · ' + name)
        model = json.loads(archive.read('lib/app/Assets/Ocr/korean-line-v5.model.json').decode('utf-8'))
        require(hashlib.sha256(archive.read('lib/app/Assets/Ocr/korean-line-v5.onnx')).hexdigest() == model['sha256'],
                '한국어 보조 인식 모델 SHA-256 일치')
    if tuple(map(int, asset['Version'].split('-', 1)[0].split('.')[:2])) >= (0, 10):
        model = json.loads(archive.read('lib/app/Assets/Ocr/wfkor.model.json').decode('utf-8'))
        require(hashlib.sha256(archive.read('lib/app/Assets/Ocr/wfkor.traineddata')).hexdigest() == model['sha256'],
                '추가 학습 한국어 OCR 모델 SHA-256 일치')
        if model.get('quantization'):
            require('lib/app/Assets/Ocr/runtime-models.json' in archive.namelist(), '변환된 OCR 모델의 런타임 해시 목록 포함')
        if 'lib/app/Assets/Ocr/runtime-models.json' in archive.namelist():
            runtime_models = json.loads(archive.read('lib/app/Assets/Ocr/runtime-models.json').decode('utf-8'))
            require('lib/app/Assets/Ocr/MODEL-MODIFICATIONS.txt' in archive.namelist(), 'OCR 모델 변경 고지 포함')
            for name, runtime_metadata in runtime_models['models'].items():
                require(hashlib.sha256(archive.read('lib/app/Assets/Ocr/' + name)).hexdigest() == runtime_metadata['sha256'],
                        '정수형 OCR 런타임 모델 SHA-256 일치 · ' + name)
        require('vcredist143-x64' in value('runtimeDependencies'), '설치 시 OCR Visual C++ 런타임 확인')
    if tuple(map(int, asset['Version'].split('-', 1)[0].split('.')[:2])) >= (1, 0):
        for name in ['PRIVACY.md','SUPPORT.md','RELEASE_READINESS_2026-10-27.md']:
            require('lib/app/' + name in archive.namelist(), '사용·데이터 처리 안내 포함 · ' + name)
    forbidden = {'snapshot.json','goals.json','client-settings.json','workspace.json','update-workspace.json','inventory-inbox.json'}
    require(not any(Path(name).name.lower() in forbidden or name.lower().endswith(('.wfriven','.pfx','.p12','.dmp')) or '/tests/fixtures/' in name.lower() for name in archive.namelist()), '개인 작업·인벤토리·키·검수 fixture 배포 제외')
    require(config["Prerelease"] == ("-" in asset["Version"]), "미리보기 업데이트 채널")
    require(x64(archive.read("lib/app/WFLabs.exe")), "앱 실행 파일 Windows x64")
    payload_hashes = {
        name.removeprefix("lib/app/"): hashlib.sha256(archive.read(name)).hexdigest()
        for name in archive.namelist() if name.startswith("lib/app/") and not name.endswith("/")
    }
with ZipFile(folder / "WFLabs-win-Portable.zip") as archive:
    require(archive.testzip() is None, "압축 배포본 ZIP 무결성")
    require("WF Labs.exe" in archive.namelist() and x64(archive.read("WF Labs.exe")), "압축 배포본 최상위 실행 파일 · Windows x64")
    require("current/coreclr.dll" in archive.namelist() and "current/PresentationFramework.dll" in archive.namelist(), "압축 배포본 자체 포함 런타임")
    require("current/sq.version" in archive.namelist(), "최상위 실행기의 필수 sq.version 포함")
    portable_metadata = ET.fromstring(archive.read("current/sq.version"))
    portable_value = lambda key: portable_metadata.findtext(".//{*}" + key)
    require(portable_value("id") == "WFLabs" and portable_value("mainExe") == "WFLabs.exe", "압축 배포본 실행 대상 연결")
    require(portable_value("version") == asset["Version"], "압축 배포본 실행 메타데이터 버전 일치")
    require("current/" + portable_value("mainExe") in archive.namelist(), "압축 배포본 실행 대상 존재")
    # Velopack relocates its generated updater/stub to the portable root.
    portable_targets = {
        name: {"Squirrel.exe": "Update.exe", "WF Labs_ExecutionStub.exe": "WF Labs.exe"}.get(name, "current/" + name)
        for name in payload_hashes
    }
    require(all(target in archive.namelist() for target in portable_targets.values()), "설치·압축 배포본의 전체 앱 구성요소 일치")
    for name, expected in payload_hashes.items():
        require(hashlib.sha256(archive.read(portable_targets[name])).hexdigest() == expected, "설치·압축 배포본 내용 SHA-256 일치 · " + name)
report = {"passed": True, "version": asset["Version"], "directory": str(folder), "checks": checks}
args.report.parent.mkdir(parents=True, exist_ok=True)
args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"passed": True, "version": asset["Version"], "checks": len(checks)}, ensure_ascii=False))
