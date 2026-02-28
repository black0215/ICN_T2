#!/usr/bin/env python3
"""
YW2 Step38D: ENCOUNT_TABLE/CHARA 문자열 + 0x538178 accessor + VIP 경로 완성
  A) 0x5938F8/0x593910/0x593920 (ENCOUNT_TABLE) 주변 컨텍스트
  B) 0x538178 (generic bank accessor) 내부 덤프
  C) 0x20B2B0 함수 → BANK_34 반환값 이용 → 인카운터 테이블 선택 흐름
  D) StreetPass RoomEntry +0x34 의미: common_enc table lookup 흐름
     → 0x2C1FA8 (앞서 나온 0x351074 caller) 컨텍스트
  E) 0x82E6B0 global singleton 구조 탐색 (뱅크 매니저)
"""
from __future__ import annotations

import struct
from pathlib import Path
from typing import List

ASSET_ROOT = Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset")
CODE_BIN   = ASSET_ROOT / "00040000001B2A00.code.bin"
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
        tgt = decode_bl(v, off)
        notes.append(f"B->0x{tgt:06X}")
    if (v & 0x0FF0F000) == 0x03500000:
        notes.append(f"CMP r{(v>>16)&0xF},#0x{v&0xFFF:X}")
    if (v & 0x0FF0F000) == 0x03510000:
        notes.append(f"CMP r{(v>>16)&0xF},#0x{v&0xFFF:X}")
    if (v & 0x0FF00000) == 0x05D00000:
        notes.append(f"LDRB r{(v>>12)&0xF},[r{(v>>16)&0xF},#0x{v&0xFFF:X}]")
    if (v & 0x0FF00000) == 0x05C00000:
        notes.append(f"STRB r{(v>>12)&0xF},[r{(v>>16)&0xF},#0x{v&0xFFF:X}]")
    if (v & 0x0FF00000) == 0x05900000:
        notes.append(f"LDR r{(v>>12)&0xF},[r{(v>>16)&0xF},#0x{v&0xFFF:X}]")
    if (v & 0x0FF00000) == 0x05800000:
        notes.append(f"STR r{(v>>12)&0xF},[r{(v>>16)&0xF},#0x{v&0xFFF:X}]")
    if (v & 0x0FF00000) == 0x05B00000:
        notes.append(f"LDRH r{(v>>12)&0xF},[r{(v>>16)&0xF},...]")
    return f"  0x{off:06X}: 0x{v:08X}" + (f"  ; {', '.join(notes)}" if notes else "")

def dump(data: bytes, start: int, size: int = 0x100) -> List[str]:
    out = []
    for i in range(0, min(size, len(data)-start), 4):
        off = start + i
        out.append(ann(data, off))
        v = u32(data, off)
        if v == 0xE12FFF1E and i > 0: break
        if (v & 0xFFFF0000) == 0xE8BD0000 and (v & (1<<15)) and i > 0: break
    return out

def bl_callers(data: bytes, target: int) -> List[int]:
    return [off for off in range(0, len(data)-4, 4)
            if (u32(data,off) & 0xFF000000) == 0xEB000000
            and decode_bl(u32(data,off), off) == target]


def main():
    code = CODE_BIN.read_bytes()
    lines = ["# YW2 Step38D: ENCOUNT_TABLE 문자열 + bank accessor + VIP 경로", ""]

    # ─────────────────────────────────────────
    # A) ENCOUNT_TABLE/CHARA 문자열 주변
    # ─────────────────────────────────────────
    for addr, label in [(0x5938F8, "ENCOUNT_TABLE_BEG"),
                        (0x593910, "ENCOUNT_TABLE_BEG2"),
                        (0x593920, "ENCOUNT_TABLE_BEG3"),
                        (0x593934, "ENCOUNT_CHARA_BEG"),
                        (0x593948, "ENCOUNT_CHARA_BEG2"),
                        (0x593958, "ENCOUNT_CHARA_BEG3")]:
        s = code[addr:addr+32]
        lines.append(f"## A) 0x{addr:06X} ({label}): {s}")
        # 앞 4바이트 (이름 해시?)
        lines.append(f"  -4: 0x{u32(code, addr-4):08X}")
        lines.append("")

    # ─────────────────────────────────────────
    # B) 0x538178 (generic bank accessor) 내부
    # ─────────────────────────────────────────
    lines.append("## B) 0x538178 (generic bank accessor) 내부")
    lines.extend(dump(code, 0x538178, 0x80))
    lines.append("")

    # ─────────────────────────────────────────
    # C) 0x2C1FA8 (0x351074 callers 중 하나) 컨텍스트
    #    → StreetPass 조우 판정 코드 흐름
    # ─────────────────────────────────────────
    lines.append("## C) 0x2C1FA8 (351074 caller) 컨텍스트")
    fn_start = 0x2C1FA8
    for off in range(0x2C1FA8-4, max(0, 0x2C1FA8-0x400), -4):
        if (u32(code,off) & 0xFFFF0000) == 0xE92D0000:
            fn_start = off; break
    lines.append(f"  함수 entry: 0x{fn_start:06X}")
    lines.extend(dump(code, fn_start, 0x300))
    callers_c = bl_callers(code, fn_start)
    lines.append(f"  callers: {[hex(c) for c in callers_c[:10]]}")
    lines.append("")

    # ─────────────────────────────────────────
    # D) 0x2C4D98/0x2C4DF8 (351074 callers) 컨텍스트
    # ─────────────────────────────────────────
    lines.append("## D) 0x2C4D98 (351074 caller) 컨텍스트 (-0x60~+0x40)")
    for addr in (0x2C4D98, 0x2C4DF8):
        lines.append(f"  @ 0x{addr:06X}:")
        for off in range(addr-0x60, addr+0x44, 4):
            if off >= 0: lines.append(ann(code, off))
        lines.append("")

    # ─────────────────────────────────────────
    # E) 0x82E6B0 global literal 탐색
    #    (bank 함수들이 참조하는 전역 싱글톤)
    # ─────────────────────────────────────────
    lines.append("## E) 0x82E6B0 pool 참조 (code.bin에서 이 값 검색)")
    needle = struct.pack("<I", 0x0082E6B0)
    hits_pool: List[int] = []
    idx = 0
    while True:
        pos = code.find(needle, idx)
        if pos < 0: break
        hits_pool.append(pos)
        idx = pos + 1
    lines.append(f"  hits: {len(hits_pool)}")
    for h in hits_pool[:5]:
        lines.append(f"  @ 0x{h:06X} : {ann(code, h)}")
    lines.append("")

    # ─────────────────────────────────────────
    # F) 0x329FE0 (351074 caller) 컨텍스트 - 간략
    # ─────────────────────────────────────────
    lines.append("## F) 0x329FE0 (351074 caller) 컨텍스트 (-0x40~+0x40)")
    fn_start_f = 0x329FE0
    for off in range(0x329FE0-4, max(0, 0x329FE0-0x200), -4):
        if (u32(code,off) & 0xFFFF0000) == 0xE92D0000:
            fn_start_f = off; break
    lines.append(f"  함수 entry: 0x{fn_start_f:06X}")
    for off in range(fn_start_f, fn_start_f+0x100, 4):
        lines.append(ann(code, off))
        v = u32(code, off)
        if (v == 0xE12FFF1E or ((v & 0xFFFF0000) == 0xE8BD0000 and (v&(1<<15)))) and off > fn_start_f:
            break
    lines.append("")

    out = OUT_DIR / "yw2_exefs_step38d_raw.txt"
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"[OK] {out}")

if __name__ == "__main__":
    main()
