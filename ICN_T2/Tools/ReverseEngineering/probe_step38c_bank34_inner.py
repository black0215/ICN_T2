#!/usr/bin/env python3
"""
YW2 Step38C: 뱅크 함수 내부 + StreetPass 수신 코드 추적
  A) 0x538410 (BANK_34) 전체 내부 덤프
  B) 0x5384E0 (BANK_5C) 전체 내부 덤프
  C) yw2_a.fa 0x39089B78 인근 파일 경로 추정
     (CfgBin 파일명/경로 문자열 역탐색)
  D) 0x20B308 컨텍스트 (StreetPass 수신 후보)
  E) 0x216290 컨텍스트
  F) 공통 문자열 테이블 "EncountConfigHash" 패턴 검색
"""
from __future__ import annotations

import struct, re
from pathlib import Path
from typing import List

ASSET_ROOT = Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset")
CODE_BIN   = ASSET_ROOT / "00040000001B2A00.code.bin"
ROMFS      = ASSET_ROOT / "yw2_a.fa"
OUT_DIR    = Path("md/04_Tech_Task/reports")

def u32(data: bytes, off: int) -> int:
    return struct.unpack_from("<I", data, off)[0] if off + 4 <= len(data) else 0

def decode_bl(v: int, pc: int) -> int:
    imm = v & 0xFFFFFF
    if imm & 0x800000: imm |= 0xFF000000
    return pc + 8 + (imm << 2)

def ann(data: bytes, off: int) -> str:
    v = u32(data, off)
    notes = []
    if (v & 0xFF000000) == 0xEB000000:
        notes.append(f"BL->0x{decode_bl(v,off):06X}")
    if (v & 0xFF000000) == 0xEA000000:
        notes.append(f"B->0x{decode_bl(v,off):06X}")
    if (v & 0x0FF0F000) == 0x03500000:
        notes.append(f"CMP r{(v>>16)&0xF},#0x{v&0xFFF:X}")
    if (v & 0x0FF00000) == 0x05D00000:
        notes.append(f"LDRB r{(v>>12)&0xF},[r{(v>>16)&0xF},#0x{v&0xFFF:X}]")
    if (v & 0x0FF00000) == 0x05C00000:
        notes.append(f"STRB r{(v>>12)&0xF},[r{(v>>16)&0xF},#0x{v&0xFFF:X}]")
    if (v & 0x0FF00000) == 0x05900000:
        notes.append(f"LDR r{(v>>12)&0xF},[r{(v>>16)&0xF},#0x{v&0xFFF:X}]")
    if (v & 0x0FF00000) == 0x05800000:
        notes.append(f"STR r{(v>>12)&0xF},[r{(v>>16)&0xF},#0x{v&0xFFF:X}]")
    return f"  0x{off:06X}: 0x{v:08X}" + (f"  ; {', '.join(notes)}" if notes else "")

def dump(data: bytes, start: int, size: int = 0x80) -> List[str]:
    out = []
    for i in range(0, min(size, len(data)-start), 4):
        off = start + i
        out.append(ann(data, off))
        # BX LR = 0xE12FFF1E
        if u32(data, off) == 0xE12FFF1E and i > 0:
            break
        # LDMFD sp!,{...pc} = 0xE8BD??-- with bit15 set
        v = u32(data, off)
        if (v & 0xFFFF0000) == 0xE8BD0000 and (v & (1<<15)) and i > 0:
            break
    return out

def bl_callers(data: bytes, target: int) -> List[int]:
    out = []
    for off in range(0, len(data)-4, 4):
        v = u32(data, off)
        if (v & 0xFF000000) == 0xEB000000 and decode_bl(v, off) == target:
            out.append(off)
    return out


def main():
    code = CODE_BIN.read_bytes()
    romfs = ROMFS.read_bytes() if ROMFS.exists() else b""
    lines = ["# YW2 Step38C: 뱅크 내부 + 수신 코드", ""]

    # ─────────────────────────────────────────
    # A) 0x538410 내부
    # ─────────────────────────────────────────
    lines.append("## A) 0x538410 (BANK_34, +0x34) 내부")
    lines.extend(dump(code, 0x538410, 0x60))
    lines.append("")

    # ─────────────────────────────────────────
    # B) 0x5384E0 내부
    # ─────────────────────────────────────────
    lines.append("## B) 0x5384E0 (BANK_5C, +0x5C) 내부")
    lines.extend(dump(code, 0x5384E0, 0x60))
    lines.append("")

    # ─────────────────────────────────────────
    # C) yw2_a.fa 0x39089B78 인근 파일명 탐색
    # ─────────────────────────────────────────
    region_start = 0x39089B78 - 0x1000
    region_end   = 0x39089B78 + 0x500
    region = romfs[region_start:region_end]
    # null-terminated ASCII 문자열 탐색
    strings = re.findall(rb'[\x20-\x7E]{8,}', region)
    lines.append(f"## C) 0x39089B78 인근 ASCII 문자열 (넓은 범위):")
    for s in strings[:40]:
        try:
            lines.append(f"  {s.decode('ascii')}")
        except Exception:
            lines.append(f"  {s}")
    lines.append("")

    # ─────────────────────────────────────────
    # D) 0x20B308 컨텍스트 (StreetPass 수신 후보)
    # ─────────────────────────────────────────
    lines.append("## D) 0x20B308 함수 컨텍스트 (-0x100 ~ +0x80)")
    # 함수 entry 탐색
    fn_start = 0x20B308
    for off in range(0x20B308 - 4, max(0, 0x20B308 - 0x300), -4):
        v = u32(code, off)
        if (v & 0xFFFF0000) == 0xE92D0000:
            fn_start = off; break
    lines.append(f"  함수 entry: 0x{fn_start:06X}")
    lines.extend(dump(code, fn_start, 0x200))
    callers_d = bl_callers(code, fn_start)
    lines.append(f"  callers: {[hex(c) for c in callers_d[:10]]}")
    lines.append("")

    # ─────────────────────────────────────────
    # E) 0x216290 컨텍스트
    # ─────────────────────────────────────────
    lines.append("## E) 0x216290 함수 컨텍스트")
    fn_start_e = 0x216290
    for off in range(0x216290 - 4, max(0, 0x216290 - 0x300), -4):
        v = u32(code, off)
        if (v & 0xFFFF0000) == 0xE92D0000:
            fn_start_e = off; break
    lines.append(f"  함수 entry: 0x{fn_start_e:06X}")
    lines.extend(dump(code, fn_start_e, 0x180))
    callers_e = bl_callers(code, fn_start_e)
    lines.append(f"  callers: {[hex(c) for c in callers_e[:10]]}")
    lines.append("")

    # ─────────────────────────────────────────
    # F) "streetpass" / "sp_room" 문자열 in code.bin
    # ─────────────────────────────────────────
    for needle in (b"streetpass", b"StreetPass", b"sp_room", b"vip", b"VIP",
                   b"ENCOUNT_TABLE", b"ENCOUNT_CHARA", b"encount_table"):
        hits = []
        idx = 0
        while True:
            pos = code.find(needle, idx)
            if pos < 0: break
            hits.append(pos)
            idx = pos + 1
        if hits:
            lines.append(f"## F) '{needle.decode()}' in code.bin: {len(hits)} @ {[hex(h) for h in hits[:5]]}")
    lines.append("")

    out = OUT_DIR / "yw2_exefs_step38c_bank34_inner_raw.txt"
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"[OK] {out}")

if __name__ == "__main__":
    main()
