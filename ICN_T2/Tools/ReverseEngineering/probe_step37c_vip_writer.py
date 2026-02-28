#!/usr/bin/env python3
"""
YW2 Step37C: 0x1050E0(STRB r4,[r3,#0x2D9]) callers + 0x351074 함수 요약
"""
from __future__ import annotations

import struct
from pathlib import Path
from typing import List

def to_u32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0] if offset + 4 <= len(data) else 0

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
            if decode_bl_target(u, off) == target:
                out.append(off)
    return out

def dump_region(data: bytes, start: int, size: int) -> List[str]:
    lines: List[str] = []
    for i in range(0, min(size, len(data) - start), 4):
        off = start + i
        u = to_u32(data, off)
        bl = f" -> 0x{decode_bl_target(u, off):06X}" if (u & 0xFF000000) == 0xEB000000 else ""
        ldrb = ""
        if (u & 0x0FF00000) == 0x05D00000:
            ldrb = f" LDRB r{(u>>12)&0xF},[r{(u>>16)&0xF},#0x{u&0xFFF:X}]"
        strb = ""
        if (u & 0x0FF00000) == 0x05C00000:
            strb = f" STRB r{(u>>12)&0xF},[r{(u>>16)&0xF},#0x{u&0xFFF:X}]"
        cmp = f" CMP r{(u>>16)&0xF},#0x{u&0xFFF:X}" if (u & 0x0FF0F000) == 0x03500000 else ""
        lines.append(f"  0x{off:06X}: 0x{u:08X}{bl}{ldrb}{strb}{cmp}")
    return lines


def main() -> int:
    code_path = Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset\00040000001B2A00.code.bin")
    out_dir = Path(r"md/04_Tech_Task/reports")
    if not code_path.exists():
        print("[ERROR]"); return 1
    data = code_path.read_bytes()
    lines = ["# YW2 Step37C: VIP 플래그 쓰기(0x1050E0) callers + 0x351074 분석", ""]

    # 0x1050E0 context dump
    lines.append("## 0x1050E0 주변 함수 (STRB r4,[r3,#0x2D9])")
    lines.append("")
    # 함수 entry 탐색
    func_start_1050 = 0x1050E0
    for off in range(0x1050E0 - 4, max(0, 0x1050E0 - 0x200), -4):
        if (to_u32(data, off) & 0xFFFF0000) == 0xE92D0000:
            func_start_1050 = off; break
    lines.append(f"  함수 entry 후보: 0x{func_start_1050:06X}")
    lines.extend(dump_region(data, func_start_1050, 0x180))
    lines.append("")

    # callers of 0x1050E0's function
    callers_105 = find_bl_to_target(data, func_start_1050)
    if func_start_1050 != 0x1050E0:
        callers_105_direct = find_bl_to_target(data, 0x1050E0)
        callers_105 = sorted(set(callers_105) | set(callers_105_direct))
    lines.append(f"## 0x{func_start_1050:06X} callers: {len(callers_105)}")
    for c in callers_105:
        lines.append(f"  0x{c:06X}")
    lines.append("")

    # 0x351074 summary (already known, add callers list)
    callers_351 = find_bl_to_target(data, 0x351074)
    lines.append(f"## 0x351074 함수 요약")
    lines.append("  기능: [r0+0x226], [r0+0x1F0], [r0+0x224] 중 하나라도 비제로면 r0=1 반환 (VIP check)")
    lines.append(f"  callers: {callers_351}")
    lines.append("")

    out = Path(out_dir) / "yw2_exefs_step37c_vip_writer_raw.txt"
    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"[OK] {out}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
