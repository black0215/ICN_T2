#!/usr/bin/env python3
"""
YW2 Step38: EncountConfigHash 0x713FE778 매칭 로직 추적
  A) code.bin에서 0x713FE778 리터럴 검색 + 주변 컨텍스트
  B) code.bin에서 common_enc 관련 해시(EncountTable ConfigHash) 군집 검색
  C) 0x538410/0x5384E0 (bank +0x34/+0x5C) 호출 직전 r1 로드 패턴 분석
     → table-index 인자 또는 configHash 인자 찾기
  D) yw2_a.fa에서 0x713FE778 위치
"""
from __future__ import annotations

import struct
from pathlib import Path
from typing import List, Tuple

ASSET_ROOT = Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset")
CODE_BIN   = ASSET_ROOT / "00040000001B2A00.code.bin"
ROMFS      = ASSET_ROOT / "yw2_a.fa"
OUT_DIR    = Path("md/04_Tech_Task/reports")

TARGET_HASH  = 0x713FE778   # Pandanoko table EncountConfigHash
PANDANOKO    = 0xDB1CC069

# 이미 파악된 bank 함수 주소
BANK_34      = 0x538410     # +0x34 bank
BANK_5C      = 0x5384E0     # +0x5C bank
DISPATCH_FN  = 0x2383F0     # bank 디스패치 블록

def u32(data: bytes, off: int) -> int:
    return struct.unpack_from("<I", data, off)[0] if off + 4 <= len(data) else 0

def find_literal(data: bytes, val: int, step: int = 4) -> List[int]:
    needle = struct.pack("<I", val)
    hits: List[int] = []
    idx = 0
    while True:
        pos = data.find(needle, idx)
        if pos < 0: break
        hits.append(pos)
        idx = pos + 1
    return hits

def decode_bl(insn: int, pc: int) -> int:
    imm24 = insn & 0xFFFFFF
    if imm24 & 0x800000: imm24 |= 0xFF000000
    return pc + 8 + (imm24 << 2)

def bl_callers_of(data: bytes, target: int) -> List[int]:
    out = []
    for off in range(0, len(data)-4, 4):
        u = u32(data, off)
        if (u & 0xFF000000) == 0xEB000000 and decode_bl(u, off) == target:
            out.append(off)
    return out

def dump_region(data: bytes, start: int, size: int) -> List[str]:
    lines = []
    for i in range(0, min(size, len(data)-start), 4):
        off = start + i
        v = u32(data, off)
        ann = ""
        if (v & 0xFF000000) == 0xEB000000:
            ann = f" BL -> 0x{decode_bl(v, off):06X}"
        # LDR r?,#imm 패턴 (MOV imm)
        if (v & 0xFFF00000) == 0xE3A00000:
            ann = f" MOV r{(v>>12)&0xF},#0x{v&0xFFF:X}"
        # LDR r?,[pc,#imm] 패턴
        if (v & 0xFFFF0000) in (0xE59F0000, 0xE59F1000, 0xE59F2000, 0xE59F3000):
            ann = f" LDR r{(v>>12)&0xF},[pc,#0x{v&0xFFF:X}]"
        # LDRB [rn,#imm]
        if (v & 0x0FF00000) == 0x05D00000:
            ann = f" LDRB r{(v>>12)&0xF},[r{(v>>16)&0xF},#0x{v&0xFFF:X}]"
        lines.append(f"  0x{off:06X}: 0x{v:08X}{ann}")
    return lines

def find_all_config_hashes_in_code(data: bytes, config_hashes: List[int]) -> dict:
    """config_hashes 리스트를 code.bin에서 검색"""
    results = {}
    for h in config_hashes:
        hits = find_literal(data, h)
        if hits:
            results[h] = hits
    return results

def main():
    lines = ["# YW2 Step38: EncountConfigHash 0x713FE778 매칭 추적", ""]
    code_data = CODE_BIN.read_bytes()
    romfs_data = ROMFS.read_bytes() if ROMFS.exists() else b""

    # ─────────────────────────────────────────
    # A) 0x713FE778 literal 검색 (code.bin)
    # ─────────────────────────────────────────
    hits_code = find_literal(code_data, TARGET_HASH)
    lines.append(f"## A) 0x713FE778 in code.bin: {len(hits_code)} hits")
    for h in hits_code[:10]:
        lines.append(f"  0x{h:06X}")
        lines.extend(dump_region(code_data, max(0, h-0x20), 0x50))
    lines.append("")

    # ─────────────────────────────────────────
    # B) 0x713FE778 in yw2_a.fa (romFS)
    # ─────────────────────────────────────────
    hits_romfs = find_literal(romfs_data, TARGET_HASH) if romfs_data else []
    lines.append(f"## B) 0x713FE778 in yw2_a.fa: {len(hits_romfs)} hits")
    for h in hits_romfs[:5]:
        start = max(0, h - 0x30)
        end   = min(len(romfs_data), h + 0x50)
        lines.append(f"  @ 0x{h:X}")
        for off in range(start, end, 4):
            v = u32(romfs_data, off)
            marker = " <-- TARGET" if off == h else ""
            lines.append(f"    0x{off:X}: 0x{v:08X} ({v}){marker}")
    lines.append("")

    # ─────────────────────────────────────────
    # C) BANK_34(0x538410), BANK_5C(0x5384E0) callers
    #    → BL 직전 30개 명령 덤프로 인자 파악
    # ─────────────────────────────────────────
    for bank_addr, bank_name in [(BANK_34, "BANK_34(+0x34)"), (BANK_5C, "BANK_5C(+0x5C)")]:
        callers = bl_callers_of(code_data, bank_addr)
        lines.append(f"## C) {bank_name} @ 0x{bank_addr:06X} callers: {len(callers)}")
        for c in callers[:5]:
            lines.append(f"  caller @ 0x{c:06X}, context (-0x30 ~ +0x10):")
            lines.extend(dump_region(code_data, max(0, c - 0x30), 0x44))
            lines.append("")
        lines.append("")

    # ─────────────────────────────────────────
    # D) DISPATCH_FN(0x2383F0) callers
    # ─────────────────────────────────────────
    dispatch_callers = bl_callers_of(code_data, DISPATCH_FN)
    lines.append(f"## D) DISPATCH_FN(0x2383F0) callers: {len(dispatch_callers)}")
    for c in dispatch_callers[:5]:
        lines.append(f"  caller @ 0x{c:06X}")
        lines.extend(dump_region(code_data, max(0, c - 0x20), 0x34))
        lines.append("")
    lines.append("")

    # ─────────────────────────────────────────
    # E) code.bin의 common_enc 해시 패턴 검색
    #    EncountTable.ConfigHash는 CRC/FNV 기반으로 map 이름에서 유도
    #    "common_enc" 문자열 자체 검색
    # ─────────────────────────────────────────
    needle_str = b"common_enc"
    str_hits: List[int] = []
    idx = 0
    while True:
        pos = romfs_data.find(needle_str, idx)
        if pos < 0: break
        str_hits.append(pos)
        idx = pos + 1
    lines.append(f"## E) 'common_enc' in yw2_a.fa: {len(str_hits)} hits (first 5)")
    for h in str_hits[:5]:
        lines.append(f"  @ 0x{h:X}: {romfs_data[h:h+20]}")
    lines.append("")

    # code.bin에도 검색
    str_hits_code: List[int] = []
    idx = 0
    while True:
        pos = code_data.find(needle_str, idx)
        if pos < 0: break
        str_hits_code.append(pos)
        idx = pos + 1
    lines.append(f"## F) 'common_enc' in code.bin: {len(str_hits_code)} hits")
    for h in str_hits_code[:5]:
        lines.append(f"  @ 0x{h:06X}: {code_data[h:h+20]}")
    lines.append("")

    out = OUT_DIR / "yw2_exefs_step38_config_hash_raw.txt"
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"[OK] {out}")

if __name__ == "__main__":
    main()
