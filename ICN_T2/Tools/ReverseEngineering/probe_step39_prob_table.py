#!/usr/bin/env python3
"""
YW2 Step39: 0x329F84 확률 테이블 + 0x4C8500 테이블 선택 함수
  A) 0x329FF0 pool 값 → 실제 확률 테이블 위치 + 내용
  B) 0x329F84 전체 흐름 상세 재분석 (VIP 체크 이후 분기)
  C) 0x4C8500 (테이블 선택 함수) 내부
  D) 0x538178 accessor 완전 해석
  E) 세이브 파일 diff용 EncountConfigHash 위치 추정
     → game1.yw에서 0x713FE778 검색 방법
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

def f32(data: bytes, off: int) -> float:
    return struct.unpack_from("<f", data, off)[0] if off + 4 <= len(data) else 0.0

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


def main():
    code = CODE_BIN.read_bytes()
    lines = ["# YW2 Step39: 확률 테이블 + 테이블 선택 함수", ""]

    # ─────────────────────────────────────────
    # A) 0x329FF0 pool 값 → 확률 테이블 위치 및 내용
    # ─────────────────────────────────────────
    # 0x329F84+8+0x3DC = 0x329F84 + 0x3E4 = 0x32A368
    pool_addr = 0x329F84 + 8 + 0x3DC  # LDR r0,[pc,#0x3DC] at 0x329FF0... wait
    # 실제: LDR r0,[r15,#0x3DC] at 0x329FF0 → pc = 0x329FF0+8 = 0x329FF8
    # 풀 주소 = 0x329FF8 + 0x3DC = 0x32A3D4
    pool_addr_a = 0x329FF8 + 0x3DC
    tbl_ptr = u32(code, pool_addr_a)
    lines.append(f"## A) 확률 테이블 포인터")
    lines.append(f"  LDR r0,[pc,#0x3DC] at 0x329FF0 → pool @ 0x{pool_addr_a:06X} = 0x{tbl_ptr:08X}")
    lines.append("")

    if tbl_ptr and tbl_ptr < len(code):
        lines.append(f"  테이블 내용 @ 0x{tbl_ptr:06X} (u32 + float 해석):")
        for i in range(64):
            off = tbl_ptr + i * 4
            if off + 4 > len(code): break
            iv = u32(code, off)
            fv = f32(code, off)
            # float 해석: 0x3F800000 = 1.0, 0x3F000000 = 0.5 등
            lines.append(f"    [{i:2d}] 0x{iv:08X} = {fv:10.6f}")
    else:
        lines.append(f"  포인터 0x{tbl_ptr:08X} 이 code.bin 범위 밖 (VA, 런타임 주소)")
    lines.append("")

    # ─────────────────────────────────────────
    # B) 0x329F84 전체 흐름 재분석 (더 큰 범위)
    # ─────────────────────────────────────────
    lines.append("## B) 0x329F84 함수 전체 (-0x20 ~ +0x300)")
    fn = 0x329F84
    for off in range(fn-4, max(0, fn-0x100), -4):
        if (u32(code, off) & 0xFFFF0000) == 0xE92D0000:
            fn = off; break
    lines.append(f"  함수 entry: 0x{fn:06X}")
    lines.extend(dump(code, fn, 0x400))
    lines.append("")

    # ─────────────────────────────────────────
    # C) 0x4C8500 (테이블 선택 함수) 내부
    # ─────────────────────────────────────────
    lines.append("## C) 0x4C8500 (encounter table selector) 내부")
    lines.extend(dump(code, 0x4C8500, 0x100))
    lines.append("")

    # ─────────────────────────────────────────
    # D) 0x238390 caller 역추적 (VIP 패킷 생성 경로)
    # ─────────────────────────────────────────
    lines.append("## D) 0x238390 (직렬화 함수) callers")
    callers = [off for off in range(0, len(code)-4, 4)
               if (u32(code,off) & 0xFF000000) == 0xEB000000
               and decode_bl(u32(code,off), off) == 0x238390]
    lines.append(f"  hits: {len(callers)}")
    for c in callers[:5]:
        lines.append(f"  caller @ 0x{c:06X}:")
        lines.extend(dump(code, max(0, c-0x10), 0x30))
        lines.append("")
    lines.append("")

    # ─────────────────────────────────────────
    # E) 세이브 파일에서 EncountConfigHash 찾기
    #    게임 세이브 위치 탐색
    # ─────────────────────────────────────────
    lines.append("## E) 세이브 파일 분석 힌트")
    save_paths = [
        Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset") / "game1.yw",
        Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset") / "game1.bin",
    ]
    # 실제 세이브 폴더 탐색
    for base in [ASSET_ROOT, Path(r"C:\Users\home\Desktop\ICN_T2")]:
        for ext in ["*.yw", "*.bin", "*.sav"]:
            for p in base.rglob(ext):
                if p.stat().st_size > 0x1000:
                    save_paths.append(p)

    found_saves = []
    for sp in save_paths:
        if sp.exists() and sp.stat().st_size > 0:
            found_saves.append(sp)

    lines.append(f"  발견된 세이브 후보: {len(found_saves)}")
    TARGET_HASH = 0x713FE778
    PANDANOKO   = 0xDB1CC069
    for sp in found_saves[:5]:
        data = sp.read_bytes()
        lines.append(f"  {sp.name} ({len(data)} bytes)")
        # EncountConfigHash 검색
        needle_v = struct.pack("<I", TARGET_HASH)
        needle_p = struct.pack("<I", PANDANOKO)
        pos_v = data.find(needle_v)
        pos_p = data.find(needle_p)
        lines.append(f"    0x713FE778 @ {hex(pos_v) if pos_v>=0 else 'not found'}")
        lines.append(f"    0xDB1CC069 @ {hex(pos_p) if pos_p>=0 else 'not found'}")
        if pos_v >= 0:
            ctx = data[max(0,pos_v-0x20):pos_v+0x40]
            lines.append(f"    context: {ctx.hex()}")
    lines.append("")

    out = OUT_DIR / "yw2_exefs_step39_prob_table_raw.txt"
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"[OK] {out}")

if __name__ == "__main__":
    main()
