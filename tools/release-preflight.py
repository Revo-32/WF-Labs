"""Read-only review of the files Git would include; never stages, commits or uploads."""
from pathlib import Path
import json
import re
import subprocess

root = Path(__file__).resolve().parents[1]
paths = sorted(set(subprocess.check_output(
    ["git", "ls-files", "-co", "--exclude-standard", "-z"], cwd=root
).decode("utf-8").strip("\0").split("\0")))
findings = []
sizes = []
patterns = [re.compile(rb"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
            re.compile(rb"\bgh[pousr]_[A-Za-z0-9]{30,}\b"),
            re.compile(rb"\bgithub_pat_[A-Za-z0-9_]{40,}\b")]
for relative in paths:
    file = root / relative
    if not file.is_file():
        continue
    size = file.stat().st_size
    sizes.append((size, relative))
    if size > 90 * 1024 * 1024:
        findings.append({"path": relative, "reason": "GitHub 단일 파일 크기 검토 필요"})
    content = file.read_bytes()
    if any(pattern.search(content) for pattern in patterns):
        findings.append({"path": relative, "reason": "자격 증명 형태 검토 필요 (값은 출력하지 않음)"})
report = {"passed": not findings, "files": len(sizes), "totalBytes": sum(s[0] for s in sizes),
          "largest": [{"path": p, "bytes": s} for s, p in sorted(sizes, reverse=True)[:10]],
          "findings": findings,
          "note": "특정 토큰·비밀키 형식과 파일 크기만 검사합니다. 비밀정보 부재나 라이선스 권한을 보증하지 않습니다. Git 작업 트리는 변경하지 않습니다."}
out = root / "artifacts" / "release-preflight.json"
out.parent.mkdir(exist_ok=True)
out.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
raise SystemExit(0 if report["passed"] else 1)
