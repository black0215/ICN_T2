#!/usr/bin/env python3
"""
YW2 StreetPass EXEFS Step37:
  A) 0x351074 함수 전체 디스어셈블 (VIP 플래그 설정 경로)
  B) STRB 패턴 전수 검색 (VIP 플래그 쓰기 후보)
  C) 0x351074 callers (BL 역추적)
"""
from __future__ import annotations

import argparse
import struct
from pathlib import Path
from typing import List, Tuple

def to_u32(data: bytes, offset: int) -> int:
    if offset + 4 > len(data):
        return 0
    return struct.unpack_from("<I", data, offset)[0]

def decode_bl_target(insn: int, pc: int) -> int:
    imm24 = insn & 0xFFFFFF
    if imm24 & 0x800000:
        imm24 |= 0xFF000000
    return pc + 8 + (imm24 << 2)

def find_bl_to_target(data: bytes, target: int) -> List[int]:
    out: List[int] = []
    for off in range(0, len(data) - 4, 4):
        u = to_u32(data, off)
        if (u & 0xFF000000) == 0xEB000000:
            t = decode_bl_target(u, off)
            if t == target:
                out.append(off)
    return out

def is_function_end(u: int) -> bool:
    # LDMFD/POP with PC (0xE8BD????), or BX LR
    if (u & 0xFFFF0000) == 0xE8BD0000 and (u & (1 << 15)):
        return True
    if u == 0xE12FFF1E:  # BX LR
        return True
    return False

def disasm_function(data: bytes, start: int, max_size: int = 0x400) -> List[str]:
    """Very simple ARM32 dump stopping at first function return."""
    lines: List[str] = []
    for i in range(0, min(max_size, len(data) - start), 4):
        off = start + i
        u = to_u32(data, off)
        bl = ""
        if (u & 0xFF000000) == 0xEB000000:
            t = decode_bl_target(u, off)
            bl = f" -> 0x{t:06X}"
        # LDRB imm: 0x05D0???? lower12 = imm
        ldrb = ""
        if (u & 0x0FF00000) == 0x05D00000:
            imm12 = u & 0xFFF
            rn = (u >> 16) & 0xF
            rd = (u >> 12) & 0xF
            ldrb = f" LDRB r{rd},[r{rn},#0x{imm12:X}]"
        # STRB imm: 0x05C0????
        strb = ""
        if (u & 0x0FF00000) == 0x05C00000:
            imm12 = u & 0xFFF
            rn = (u >> 16) & 0xF
            rd = (u >> 12) & 0xF
            strb = f" STRB r{rd},[r{rn},#0x{imm12:X}]"
        # CMP
        cmp_info = ""
        if (u & 0x0FF0F000) == 0x03500000:
            imm12 = u & 0xFFF
            rn = (u >> 16) & 0xF
            cmp_info = f" CMP r{rn},#0x{imm12:X}"
        lines.append(f"  0x{off:06X}: 0x{u:08X}{bl}{ldrb}{strb}{cmp_info}")
        if is_function_end(u) and i > 0:
            break
    return lines

def scan_strb_with_offset(data: bytes, target_offset: int) -> List[Tuple[int, int, int, int]]:
    """Find all STRB [Rn,#imm12] where imm12 == target_offset."""
    out: List[Tuple[int, int, int, int]] = []
    for off in range(0, len(data) - 4, 4):
        u = to_u32(data, off)
        if (u & 0x0FF00000) == 0x05C00000:
            imm12 = u & 0xFFF
            rn = (u >> 16) & 0xF
            rd = (u >> 12) & 0xF
            if imm12 == target_offset:
                out.append((off, rd, rn, imm12))
    return out


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--asset-root", default=r"C:\Users\home\Desktop\ICN_T2\Yw2Asset")
    parser.add_argument("--output-dir", default=r"md/04_Tech_Task/reports")
    args = parser.parse_args()
    out_dir = Path(args.output_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    code_path = Path(args.asset_root) / "00040000001B2A00.code.bin"
    if not code_path.exists():
        print(f"[ERROR] {code_path}")
        return 1
    data = code_path.read_bytes()

    lines: List[str] = ["# YW2 Step37: 0x351074 VIP 분석 + STRB 0x2D9 검색", ""]

    # A) 0x351074 함수 전체 디스어셈블 (function entry 후보 찾기)
    # 0x351074는 중간 지점일 수 있음 - 앞쪽으로 함수 entry 탐색
    # ARM32 PUSH (STMFD sp!,...) 패턴
    entry_351074 = 0x351074
    func_start = entry_351074
    for off in range(entry_351074 - 4, max(0, entry_351074 - 0x200), -4):
        u = to_u32(data, off)
        if (u & 0xFFFF0000) == 0xE92D0000:  # PUSH/STMFD
            func_start = off
            break
    lines.append(f"## A) 0x351074 함수 entry 후보: 0x{func_start:06X}")
    lines.append("")
    lines.extend(disasm_function(data, func_start, 0x600))
    lines.append("")

    # B) STRB [Rn,#0x2D9] 검색
    strb_2d9 = scan_strb_with_offset(data, 0x2D9)
    lines.append(f"## B) STRB [Rn,#0x2D9]: {len(strb_2d9)} hits")
    lines.append("")
    for (off, rd, rn, imm) in strb_2d9:
        u = to_u32(data, off)
        lines.append(f"  0x{off:06X}: 0x{u:08X}  STRB r{rd},[r{rn},#0x{imm:X}]")
    lines.append("")

    # C) 0x351074 callers
    callers_351074 = find_bl_to_target(data, 0x351074)
    lines.append(f"## C) 0x351074 callers: {len(callers_351074)}")
    lines.append("")
    for c in callers_351074[:30]:
        lines.append(f"  0x{c:06X}")
    lines.append("")

    out_path = out_dir / "yw2_exefs_step37_vip_flag_raw.txt"
    out_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"[OK] {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
