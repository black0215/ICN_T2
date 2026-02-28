#!/usr/bin/env python3
"""
Probe code.bin for bank (+0x0C/+0x34/+0x5C) to path-table index (21/24/25) mapping.
Used for YW2 StreetPass EXEFS Step27: bank 21/24/25 final binding.
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


def find_u32_literal(data: bytes, value: int, start: int, end: int) -> List[int]:
    out: List[int] = []
    needle = struct.pack("<I", value)
    pos = start
    while pos <= end - 4:
        idx = data.find(needle, pos, end)
        if idx < 0:
            break
        out.append(idx)
        pos = idx + 1
    return out


def arm_decode_mov_imm(data: bytes, offset: int) -> Tuple[bool, int, int]:
    """Decode ARM MOV Rd, #imm (or MVN). Returns (is_mov, rd, imm12)."""
    if offset + 4 > len(data):
        return False, 0, 0
    insn = to_u32(data, offset)
    cond = (insn >> 28) & 0xF
    op = (insn >> 21) & 0x3F
    rd = (insn >> 12) & 0xF
    imm12 = insn & 0xFFF
    # MOV: cond 1110 00 0 1101 (0x3D) -> 0xE3A
    if (insn & 0x0FF00000) == 0x03A00000:  # MOV (no S)
        return True, rd, imm12
    if (insn & 0x0FF00000) == 0x03B00000:  # MOV with imm rotate
        # expand imm12 (8-bit rotated)
        rot = (imm12 >> 8) * 2
        imm8 = imm12 & 0xFF
        if rot == 0:
            return True, rd, imm8
        val = (imm8 >> rot) | (imm8 << (32 - rot))
        return True, rd, val & 0xFFFFFFFF
    return False, 0, 0


def scan_region_for_imm(data: bytes, start: int, end: int, targets: List[int]) -> List[Tuple[int, int]]:
    """Find ARM instructions that load one of targets as immediate in [start, end)."""
    hits: List[Tuple[int, int]] = []
    for off in range(start, min(end, len(data) - 4), 4):
        ok, rd, imm = arm_decode_mov_imm(data, off)
        if ok and imm in targets:
            hits.append((off, imm))
    return hits


def main() -> int:
    parser = argparse.ArgumentParser(description="Probe bank->index mapping in code.bin")
    parser.add_argument("--code", default=None, help="Path to 00040000001B2A00.code.bin")
    parser.add_argument("--out", default=None, help="Output report path (optional)")
    args = parser.parse_args()
    asset = Path(args.code or r"C:\Users\home\Desktop\ICN_T2\Yw2Asset")
    if asset.suffix != ".bin":
        code_path = asset / "00040000001B2A00.code.bin"
    else:
        code_path = Path(args.code or str(asset))
    if not code_path.exists():
        print(f"[ERROR] Not found: {code_path}")
        return 1
    data = code_path.read_bytes()

    # Path table (file offset): index 21 @ 0x71CC94 -> ptr 0x00705B9C, 24 @ 0x71CCA0, 25 @ 0x71CCA4
    path_ptrs_va = [0x00805B9C, 0x008061DC, 0x008062E8]
    path_ptrs_file = [0x00705B9C, 0x007061DC, 0x007062E8]

    out_lines: List[str] = []
    out_lines.append("# YW2 EXEFS Step27 Bank-Index Mapping Probe")
    out_lines.append("")
    out_lines.append(f"- code.bin: `{code_path}`")
    out_lines.append("")

    # 1) 0x538178 full function dump (first 0x200 bytes)
    addr_538178 = 0x538178
    size_538178 = 0x200
    if addr_538178 + size_538178 <= len(data):
        out_lines.append("## 1) F_538178 region (0x538178 + 0x200)")
        out_lines.append("")
        for i in range(0, size_538178, 4):
            off = addr_538178 + i
            u = to_u32(data, off)
            out_lines.append(f"  0x{off:06X}: 0x{u:08X}")
        out_lines.append("")
        # Check for path table ptr or 21/24/25 in literal pool nearby
        for ptr in path_ptrs_file:
            hits = find_u32_literal(data, ptr, addr_538178, addr_538178 + size_538178)
            if hits:
                out_lines.append(f"  Path ptr 0x{ptr:08X} in region: {[hex(h) for h in hits]}")
        for imm in (21, 24, 25):
            hits = find_u32_literal(data, imm, addr_538178, addr_538178 + size_538178)
            if hits:
                out_lines.append(f"  Literal {imm} in region: {[hex(h) for h in hits]}")
        out_lines.append("")

    # 2) Bank init 0x1C657C–0x1C69AC: search for mov rX, #21/#24/#25
    bank_start = 0x1C657C
    bank_end = 0x1C69AC + 0x40
    out_lines.append("## 2) Bank init region 0x1C657C-0x1C69AC: MOV #21/#24/#25")
    out_lines.append("")
    mov_hits = scan_region_for_imm(data, bank_start, bank_end, [21, 24, 25])
    if mov_hits:
        for off, imm in mov_hits:
            out_lines.append(f"  0x{off:06X}: MOV # {imm}")
    else:
        out_lines.append("  No MOV #21/24/25 in bank wrapper region (immediates may be in literal pool).")
    out_lines.append("")
    # Literal pool in same region
    for imm in (21, 24, 25):
        hits = find_u32_literal(data, imm, bank_start, bank_end)
        if hits:
            out_lines.append(f"  Literal u32 {imm} in bank region: {[hex(h) for h in hits]}")
    out_lines.append("")

    # 3) 0x5480BC region: path table or index use
    addr_5480bc = 0x5480BC
    size_5480 = 0x200
    out_lines.append("## 3) F_5480BC region (0x5480BC + 0x200) path/index refs")
    out_lines.append("")
    for ptr in path_ptrs_file:
        hits = find_u32_literal(data, ptr, addr_5480bc, addr_5480bc + size_5480)
        if hits:
            out_lines.append(f"  Path ptr 0x{ptr:08X}: {[hex(h) for h in hits]}")
    for imm in (21, 24, 25):
        hits = find_u32_literal(data, imm, addr_5480bc, addr_5480bc + size_5480)
        if hits:
            out_lines.append(f"  Literal {imm}: {[hex(h) for h in hits]}")
    out_lines.append("")

    # 4) Callers of 0x5382CC (+0x0C), 0x538410 (+0x34), 0x5384E0 (+0x5C): which path is used
    # We already know: 0x5382CC -> +0x0C, 0x538410 -> +0x34, 0x5384E0 -> +0x5C. Search for 0x71CC94/0x71CCA0/0x71CCA4 (path table entry addrs in VA 0x81CC94 etc) in code that calls these.
    out_lines.append("## 4) Path table base 0x71CC40 refs near 0x538xxx")
    path_base = 0x0071CC40
    for delta in (0x54, 0x60, 0x64):  # indices 21,24,25 from base
        va = path_base + delta
        hits = find_u32_literal(data, va, 0x538000, 0x53A000)
        if hits:
            out_lines.append(f"  0x{va:08X} (path entry): {[hex(h) for h in hits]}")
    out_lines.append("")

    # 5) 0x1C65DC (shared bank writer): any 21/24/25
    addr_1c65dc = 0x1C65DC
    size_1c65 = 0x400
    out_lines.append("## 5) 0x1C65DC shared bank writer region: 21/24/25")
    for imm in (21, 24, 25):
        hits = find_u32_literal(data, imm, addr_1c65dc, addr_1c65dc + size_1c65)
        if hits:
            out_lines.append(f"  Literal {imm}: {[hex(h) for h in hits]}")
    mov_1c = scan_region_for_imm(data, addr_1c65dc, addr_1c65dc + size_1c65, [21, 24, 25])
    if mov_1c:
        for off, imm in mov_1c:
            out_lines.append(f"  MOV # {imm} @ 0x{off:06X}")
    out_lines.append("")

    report = "\n".join(out_lines)
    print(report)
    if args.out:
        Path(args.out).write_text(report, encoding="utf-8")
        print(f"[OK] Wrote {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
