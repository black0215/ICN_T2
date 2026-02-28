#!/usr/bin/env python3
"""
YW2 Step38B: EncountConfigHash 선택 로직 심층 분석
  A) code.bin 0x705BAC "common_enc" 문자열 주변 + LDR/ADR 참조 역추적
  B) yw2_a.fa 0x39089B78 (CfgBin 항목) 파일 경로 추정 (인접 문자열 검색)
  C) 뱅크(index 21/24/25)로 전달되는 r0/r1 인자 추적
     → 0x2383F0 전체 덤프 (인자 준비 코드)
  D) 0x37F24DD8 앞뒤 구조 (EncountTable raw 배열 패턴 확인)
"""
from __future__ import annotations

import struct
from pathlib import Path
from typing import List

ASSET_ROOT = Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset")
CODE_BIN   = ASSET_ROOT / "00040000001B2A00.code.bin"
ROMFS      = ASSET_ROOT / "yw2_a.fa"
OUT_DIR    = Path("md/04_Tech_Task/reports")

def u32(data: bytes, off: int) -> int:
    return struct.unpack_from("<I", data, off)[0] if off + 4 <= len(data) else 0

def decode_bl(insn: int, pc: int) -> int:
    imm24 = insn & 0xFFFFFF
    if imm24 & 0x800000: imm24 |= 0xFF000000
    return pc + 8 + (imm24 << 2)

def annotate(data: bytes, off: int) -> str:
    v = u32(data, off)
    anns = []
    if (v & 0xFF000000) == 0xEB000000:
        anns.append(f"BL->0x{decode_bl(v,off):06X}")
    if (v & 0xFFFFF000) == 0xE59F0000:
        pool = off + 8 + (v & 0xFFF)
        anns.append(f"LDR r0,[pc,+0x{v&0xFFF:X}]=>0x{pool:06X}")
    if (v & 0xFF000000) in (0xEA000000,):  # B (branch)
        imm24 = v & 0xFFFFFF
        if imm24 & 0x800000: imm24 |= 0xFF000000
        tgt = off + 8 + (imm24 << 2)
        anns.append(f"B->0x{tgt:06X}")
    if (v & 0x0FF0F000) == 0x03500000:
        anns.append(f"CMP r{(v>>16)&0xF},#0x{v&0xFFF:X}")
    if (v & 0x0FF00000) == 0x05D00000:
        anns.append(f"LDRB r{(v>>12)&0xF},[r{(v>>16)&0xF},#0x{v&0xFFF:X}]")
    if (v & 0x0FF00000) == 0x05C00000:
        anns.append(f"STRB r{(v>>12)&0xF},[r{(v>>16)&0xF},#0x{v&0xFFF:X}]")
    return f"  0x{off:06X}: 0x{v:08X}" + (f"  ; {', '.join(anns)}" if anns else "")

def dump_range(data: bytes, start: int, end: int) -> List[str]:
    return [annotate(data, off) for off in range(start, min(end, len(data)-4), 4)]


def main():
    code = CODE_BIN.read_bytes()
    romfs = ROMFS.read_bytes() if ROMFS.exists() else b""
    lines = ["# YW2 Step38B: EncountConfigHash 선택 로직 심층", ""]

    # ─────────────────────────────────────────────
    # A) "common_enc" 문자열 @ 0x705BAC 주변
    # ─────────────────────────────────────────────
    for str_off in (0x705BAC, 0x705BE6):
        lines.append(f"## A) code.bin 문자열 @ 0x{str_off:06X}: {code[str_off:str_off+32]}")
        lines.append("  주변 코드 (-0x60 ~ +0x20, 4byte 단위):")
        start = max(0, str_off - 0x60)
        for off in range(start, str_off + 0x20, 4):
            v = u32(code, off)
            lines.append(annotate(code, off))
        lines.append("")

    # 0x705BAC를 참조하는 LDR/ADR 역탐색 (pc-relative)
    target_addr = 0x705BAC
    refs = []
    for off in range(0, len(code)-4, 4):
        v = u32(code, off)
        # LDR Rn,[pc,#imm]: 0xE59Fxxxx
        if (v & 0xFFFFF000) in (0xE59F0000, 0xE59F1000, 0xE59F2000, 0xE59F3000,
                                  0xE59F4000, 0xE59F5000, 0xE59F6000, 0xE59F7000):
            pool = off + 8 + (v & 0xFFF)
            if 0 <= pool < len(code):
                pv = u32(code, pool)
                if pv == target_addr:
                    refs.append(off)
    lines.append(f"## A2) 0x705BAC 참조 LDR: {len(refs)} hits")
    for r in refs[:10]:
        lines.append(f"  @ 0x{r:06X}, pool={r+8+(u32(code,r)&0xFFF):06X}")
        lines.extend(dump_range(code, max(0,r-0x20), r+0x40))
        lines.append("")

    # ─────────────────────────────────────────────
    # B) 0x39089B78 주변 문자열 탐색 (파일명 힌트)
    # ─────────────────────────────────────────────
    region_start = 0x39089B78 - 0x400
    region_end   = 0x39089B78 + 0x400
    region = romfs[region_start:region_end]
    # 인쇄 가능한 ASCII 문자열 탐색
    import re
    strings = re.findall(rb'[\x20-\x7E]{6,}', region)
    lines.append(f"## B) 0x39089B78 인근 문자열 ({region_start:X}~{region_end:X}):")
    for s in strings[:30]:
        lines.append(f"  {s}")
    lines.append("")

    # ─────────────────────────────────────────────
    # C) 0x2383F0 전체 함수 덤프 (인자 준비 코드)
    #    (이미 DISPATCH_FN으로 알려진 블록)
    # ─────────────────────────────────────────────
    lines.append("## C) 0x2383F0 전체 덤프 (bank 디스패치 블록 + 인자)")
    # 함수 시작 찾기
    fn_start = 0x2383F0
    for off in range(0x2383F0 - 4, max(0, 0x2383F0 - 0x300), -4):
        v = u32(code, off)
        if (v & 0xFFFF0000) == 0xE92D0000:
            fn_start = off; break
    lines.append(f"  함수 entry: 0x{fn_start:06X}")
    lines.extend(dump_range(code, fn_start, fn_start + 0x200))
    lines.append("")

    # ─────────────────────────────────────────────
    # D) 0x37F24DA8 ~ 0x37F24E30 EncountTable 배열 패턴
    #    28바이트(7 u32) 단위로 파싱 시도
    # ─────────────────────────────────────────────
    lines.append("## D) 0x37F24DA8 주변 EncountTable 구조 (28byte 단위)")
    tbl_base = 0x37F24DA8 - 28  # 이전 2개 항목부터
    for t in range(6):
        entry_off = tbl_base + t * 28
        if entry_off < 0 or entry_off + 28 > len(romfs): continue
        cfg_hash = u32(romfs, entry_off)
        offsets = [struct.unpack_from("<i", romfs, entry_off + 4 + i*4)[0] for i in range(6)]
        is_pandanoko = cfg_hash == 0x713FE778
        marker = " <-- Pandanoko table" if is_pandanoko else ""
        lines.append(f"  entry[t={t}] @ 0x{entry_off:X}: cfgHash=0x{cfg_hash:08X} offsets={offsets}{marker}")
    lines.append("")

    # ─────────────────────────────────────────────
    # E) 432개 테이블 ConfigHash → 어떤 값이 자주 등장하는가
    #    common_enc 파일 크기 추정 (table 0의 hash를 기준으로)
    # ─────────────────────────────────────────────
    # CfgParser가 뽑은 table[0] config_hash = 0xEDFE7305
    TABLE0_HASH = 0xEDFE7305
    hits_t0 = []
    for i in range(0, len(romfs)-4, 4):
        if u32(romfs, i) == TABLE0_HASH:
            hits_t0.append(i)
    lines.append(f"## E) table[0] cfgHash(0xEDFE7305) in romFS: {len(hits_t0)} hits")
    for h in hits_t0[:3]:
        lines.append(f"  @ 0x{h:X}")
        offsets = [struct.unpack_from("<i", romfs, h + 4 + i*4)[0] for i in range(6)]
        lines.append(f"    offsets={offsets}")
    lines.append("")

    out = OUT_DIR / "yw2_exefs_step38b_deep_raw.txt"
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"[OK] {out}")

if __name__ == "__main__":
    main()
