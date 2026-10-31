"""Pin real WFCD/Public Export game artwork for mastery material rows.

No artwork is generated. Original downloads stay in the supplied workspace;
only transparent thumbnails and ID/provenance metadata are shipped.
Usage: python tools/import_material_icons.py <cache-directory>
"""
import concurrent.futures
import hashlib
import io
import json
import pathlib
import sys
import time
import urllib.request

from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]
CACHE = pathlib.Path(sys.argv[1])
CACHE.mkdir(parents=True, exist_ok=True)
OUT = ROOT / "src/TennoForge.Desktop/Assets/Materials"
OUT.mkdir(parents=True, exist_ok=True)


def download(url, path):
    if path.exists():
        return path.read_bytes()
    for attempt in range(3):
        try:
            request = urllib.request.Request(url, headers={"User-Agent": "WF-Labs-Material-Assets/1.0"})
            with urllib.request.urlopen(request, timeout=35) as response:
                data = response.read(12 * 1024 * 1024)
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            return data
        except Exception:
            if attempt == 2:
                raise
            time.sleep(attempt + 1)


sources = json.loads((ROOT / "data/inventory-sources.json").read_text(encoding="utf-8"))["sources"]
catalog = json.loads((ROOT / "data/inventory-catalog.json").read_text(encoding="utf-8"))
metadata = {}
for category in ("Resources", "Misc", "Components", "Primary", "Secondary", "Melee"):
    source = sources["WFCD:" + category]
    raw = download(source["url"], CACHE / (category + ".json"))
    if hashlib.sha256(raw).hexdigest() != source["sha256"]:
        raise ValueError("고정 자료 해시 불일치: " + category)
    for item in json.loads(raw):
        if item.get("imageName"):
            metadata[item["uniqueName"]] = (item, source["revision"])

items = {i["id"]: i for i in catalog["items"]}
recipes = {}
for recipe in catalog["recipes"]:
    recipes.setdefault(recipe["resultId"], recipe)


def component(id):
    return id.startswith(("/Lotus/Types/Recipes/WarframeRecipes/", "/Lotus/Types/Recipes/Weapons/WeaponParts/",
                          "/Lotus/Types/Gameplay/InfestedMicroplanet/Resources/Mechs/", "/Lotus/Weapons/")) or id == "/Lotus/Types/Keys/BardQuest/BardQuestSequencerItem"


def finished(id):
    item = items.get(id, {})
    return not item.get("masterable") and not component(id) and item.get("category") in ("Resources", "Misc")


pending = [i["id"] for i in items.values() if i["masterable"] and i["id"] in recipes]
seen, leaves = set(), set()
while pending:
    id = pending.pop()
    if id in seen:
        continue
    seen.add(id)
    if id in recipes and not finished(id):
        pending += [x["id"] for x in recipes[id]["ingredients"]]
    else:
        leaves.add(id)

jobs, missing = {}, []
for id in sorted(leaves):
    if id not in metadata:
        missing.append({"id": id, "name": items.get(id, {}).get("name", ""), "reason": "고정 원문에 imageName 없음"})
        continue
    item, revision = metadata[id]
    name = item["imageName"]
    if pathlib.Path(name).name != name:
        raise ValueError("이미지 경로 오류")
    jobs.setdefault((revision, name), []).append(id)


def render(job):
    (revision, name), ids = job
    url = f"https://raw.githubusercontent.com/WFCD/warframe-items/{revision}/data/img/{name}"
    raw = download(url, CACHE / "originals" / revision / name)
    picture = Image.open(io.BytesIO(raw)).convert("RGBA")
    original_size = list(picture.size)
    picture.thumbnail((128, 128), Image.Resampling.LANCZOS)
    # Content-addressed filenames avoid invalid paths and preserve deduplication.
    filename = hashlib.sha256(raw).hexdigest()[:20] + ".png"
    target = OUT / filename
    picture.save(target, optimize=True)
    return {"ids": ids, "file": filename, "sourceUrl": url,
            "originalSha256": hashlib.sha256(raw).hexdigest(), "originalSize": original_size,
            "sha256": hashlib.sha256(target.read_bytes()).hexdigest(), "size": list(picture.size)}


results = []
with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
    futures = {pool.submit(render, job): job for job in jobs.items()}
    for index, future in enumerate(concurrent.futures.as_completed(futures), 1):
        try:
            results.append(future.result())
        except Exception as error:
            (_, name), ids = futures[future]
            missing += [{"id": id, "name": items.get(id, {}).get("name", ""), "reason": str(error), "imageName": name} for id in ids]
        if index % 50 == 0:
            print(f"{index}/{len(jobs)} 이미지 확인", flush=True)

manifest = {"schemaVersion": 1, "reviewedDate": "2026-10-31", "artworkGenerated": False,
            "transformation": "원본 RGBA를 유지해 긴 변 128px 이하로 Lanczos 축소·PNG 저장. 재작화·합성 없음.",
            "notice": "게임 이미지 권리: Digital Extremes. WFCD의 MIT 라이선스는 게임 이미지의 권리를 대체하지 않습니다.",
            "catalogSha256": hashlib.sha256((ROOT / "data/inventory-catalog.json").read_bytes()).hexdigest(),
            "requiredIds": len(leaves), "coveredIds": sum(len(x["ids"]) for x in results),
            "images": sorted(results, key=lambda x: x["file"]), "missing": missing}
(OUT / "sources.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"required": len(leaves), "covered": manifest["coveredIds"], "images": len(results), "missing": missing}, ensure_ascii=False), flush=True)
