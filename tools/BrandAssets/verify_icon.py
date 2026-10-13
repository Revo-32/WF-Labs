"""실행 파일의 실제 PE 아이콘 리소스를 원본 ICO와 대조한다. Python 표준 라이브러리만 사용."""
import argparse
import hashlib
import json
import struct
from pathlib import Path


def verify(executable: Path, icon: Path) -> dict:
    pe = executable.read_bytes()
    ico = icon.read_bytes()
    u16 = lambda data, pos: struct.unpack_from("<H", data, pos)[0]
    u32 = lambda data, pos: struct.unpack_from("<I", data, pos)[0]
    pe_start = u32(pe, 0x3C)
    assert pe[pe_start:pe_start + 4] == b"PE\0\0", "PE 실행 파일이 아닙니다."
    optional = pe_start + 24
    directory = optional + (112 if u16(pe, optional) == 0x20B else 96)
    section_table = optional + u16(pe, pe_start + 20)
    sections = [struct.unpack_from("<IIII", pe, section_table + i * 40 + 8)
                for i in range(u16(pe, pe_start + 6))]

    def file_offset(rva):
        for virtual_size, address, raw_size, raw_offset in sections:
            if address <= rva < address + max(virtual_size, raw_size):
                return raw_offset + rva - address
        raise ValueError(f"리소스 RVA를 찾을 수 없습니다: {rva}")

    resource_root = file_offset(u32(pe, directory + 16))
    resources = {}

    def visit(relative=0, trail=()):
        offset = resource_root + relative
        count = u16(pe, offset + 12) + u16(pe, offset + 14)
        for index in range(count):
            resource_id, child = struct.unpack_from("<II", pe, offset + 16 + index * 8)
            key = trail + (resource_id,)
            if child & 0x80000000:
                visit(child & 0x7FFFFFFF, key)
            else:
                rva, length = struct.unpack_from("<II", pe, resource_root + child)
                start = file_offset(rva)
                resources[key] = pe[start:start + length]

    visit()
    expected = {}
    assert ico[:4] == b"\0\0\1\0", "ICO 헤더가 올바르지 않습니다."
    for index in range(u16(ico, 4)):
        width, height, _, _, planes, bits, length, offset = struct.unpack_from("<BBBBHHII", ico, 6 + index * 16)
        expected[(width or 256, height or 256, bits)] = ico[offset:offset + length]
        assert planes == 1
    assert sorted(size[0] for size in expected) == [16, 32, 48, 64, 128, 256]
    found = []
    for key, group in resources.items():
        if key[0] != 14:  # RT_GROUP_ICON
            continue
        entries = []
        for index in range(u16(group, 4)):
            width, height, _, _, planes, bits, length, resource_id = struct.unpack_from("<BBBBHHIH", group, 6 + index * 14)
            size = (width or 256, height or 256, bits)
            frame = resources[(3, resource_id, key[-1])]  # RT_ICON, same language
            assert size in expected and frame == expected[size], f"아이콘 리소스가 다릅니다: {size}"
            assert len(frame) == length and planes == 1
            entries.append({"size": size[0], "bits": bits, "sha256": hashlib.sha256(frame).hexdigest()})
        assert len(entries) == len(expected)
        found.append({"resource": key[1], "language": key[-1], "frames": entries})
    assert found, "실행 파일에 아이콘 그룹이 없습니다."
    return {"passed": True, "executable": str(executable.resolve()), "icon": str(icon.resolve()), "groups": found}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("executable", type=Path)
    parser.add_argument("icon", type=Path)
    parser.add_argument("report", type=Path)
    args = parser.parse_args()
    result = verify(args.executable, args.icon)
    args.report.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print("실행 파일의 16·32·48·64·128·256px 아이콘 리소스가 원본 ICO와 일치합니다.")
