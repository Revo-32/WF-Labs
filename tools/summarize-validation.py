"""Summarize completed checks; missing checks never count as a successful run."""
from pathlib import Path
import argparse
import json
import os
import re

parser = argparse.ArgumentParser()
parser.add_argument("directory", type=Path)
args = parser.parse_args()
folder = args.directory.resolve()
script = Path(__file__).with_name("validate.ps1").read_text(encoding="utf-8-sig")
expected = re.findall(r"-Mode (--[\w-]+) -Output \(Join-Path \$report '([^']+)'\)", script)
rows = []
try:
    core = json.loads((folder / "checks.json").read_text(encoding="utf-8-sig"))
    passed = bool(core.get("passed")) and not core.get("failed")
    rows.append({"name": "계산·데이터", "passed": passed, "detail": str(core.get("passed"))})
except (OSError, ValueError):
    rows.append({"name": "계산·데이터", "passed": False, "detail": "완료 보고서 없음"})
for mode, name in expected:
    try:
        data = json.loads((folder / name / "isolation.json").read_text(encoding="utf-8-sig"))
        passed = data.get("exitCode") == 0 and data.get("mode") == mode
        detail = "통과" if passed else "실패 · " + str(data.get("exitCode"))
    except (OSError, ValueError):
        passed, detail = False, "미실행·미완료"
    rows.append({"name": name, "mode": mode, "passed": passed, "detail": detail})
complete = bool(expected) and all(row["passed"] for row in rows)
folder.mkdir(parents=True, exist_ok=True)
report = {"passed": complete, "requiredStages": len(rows), "completedStages": sum(row["passed"] for row in rows), "stages": rows}
(folder / "validation-summary.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
markdown = "## WF Labs Windows 검증\n\n" + ("모든 필수 단계 통과" if complete else "실패 또는 완료되지 않은 단계가 있습니다") + "\n\n| 단계 | 결과 |\n|---|---|\n"
markdown += "".join(f"| {row['name']} | {row['detail']} |\n" for row in rows)
(folder / "validation-summary.md").write_text(markdown, encoding="utf-8")
if path := os.environ.get("GITHUB_STEP_SUMMARY"):
    with open(path, "a", encoding="utf-8") as summary:
        summary.write(markdown)
print(json.dumps({key: report[key] for key in ("passed", "requiredStages", "completedStages")}, ensure_ascii=False))
raise SystemExit(0 if complete else 1)
