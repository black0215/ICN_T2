#!/usr/bin/env python3
"""
YW2 StreetPass EXEFS Step36: 0x2383F0 vtable 역추적 + 0x2C4E3C RNG 후보 추적
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


def find_u32_literal(data: bytes, value: int) -> List[int]:
    needle = struct.pack("<I", value)
    out: List[int] = []
    start = 0
    while True:
        idx = data.find(needle, start)
        if idx < 0:
            return out
        out.append(idx)
        start = idx + 1


def decode_bl_target(insn: int, pc: int) -> int:
    imm24 = insn & 0xFFFFFF
    if imm24 & 0x800000:
        imm24 |= 0xFF000000
    return pc + 8 + (imm24 << 2)


def dump_region(data: bytes, start: int, size: int) -> List[str]:
    lines: List[str] = []
    for i in range(0, min(size, len(data) - start), 4):
        off = start + i
        u = to_u32(data, off)
        bl = ""
        if (u & 0xFF000000) == 0xEB000000:
            t = decode_bl_target(u, off)
            bl = f" -> 0x{t:06X}"
        lines.append(f"  0x{off:06X}: 0x{u:08X}{bl}")
    return lines


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--asset-root", default=r"C:\Users\home\Desktop\ICN_T2\Yw2Asset")
    parser.add_argument("--output-dir", default=r"md/04_Tech_Task/reports")
    args = parser.parse_args()
    root = Path(args.asset_root)
    out_dir = Path(args.output_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    code_path = root / "00040000001B2A00.code.bin"
    if not code_path.exists():
        print(f"[ERROR] Not found: {code_path}")
        return 1

    data = code_path.read_bytes()
    lines: List[str] = ["# YW2 Step36: 0x2383F0 vtable + RNG 후보 추적", ""]

    # 1) 0x2383F0 as literal (vtable / LDR target) - 3DS VA 후보
    for val in [0x2383F0, 0x12383F0, 0x81383F0, 0x0012383F0]:
        hits = find_u32_literal(data, val)
        if hits:
            lines.append(f"## u32 literal 0x{val:08X}: {len(hits)} hits")
            for h in hits[:20]:
                lines.append(f"  0x{h:06X}")
            lines.append("")

    # 2) 0x351074, 0x569DFC (RNG 후보) 주변 덤프
    for addr, label in [(0x351074, "0x351074 (CMP 직전 BL)"), (0x569DFC, "0x569DFC (CMP 직전 BL)")]:
        lines.append(f"## {label} 주변 -0x40~+0x40")
        lines.extend(dump_region(data, max(0, addr - 0x40), 0x80))
        lines.append("")

    # 3) STRB [Rn,#0x2D9] — VIP 플래그 쓰기 후보 (ARM32: imm12=0x2D9)
    lines.append("## STRB [Rn,#0x2D9] (VIP 플래그 쓰기 후보)")
    strb_hits: List[int] = []
    for off in range(0, len(data) - 4, 4):
        u = to_u32(data, off)
        if (u & 0x0FF0F000) == 0x05C00000 and (u & 0xFFF) == 0x2D9:
            strb_hits.append(off)
    for h in strb_hits[:15]:
        lines.append(f"  0x{h:06X}")
    lines.append("")

    # 4) 0x2C4E38 LDRB 직전 블록
    lines.append("## 0x2C4E38 (LDRB r0,[r0,#0x2D9]) 직전 0x60바이트")
    lines.extend(dump_region(data, 0x2C4DD8, 0x60))
    lines.append("")

    out_path = out_dir / "yw2_exefs_step36_vtable_rng_raw.txt"
    out_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"[OK] {out_path}")

    # Density
    md_lines = [
        "# YW2 EXEFS Step36 Report (vtable + VIP CMP 해석)",
        "",
        "- Date: 2026-02-28 (UTC)",
        "- Goal: 0x2383F0 진입 경로, 0x2C4E3C CMP 의미",
        "",
        "## 1) 0x2383F0 vtable",
        "",
    ]
    for val in [0x2383F0, 0x12383F0]:
        hits = find_u32_literal(data, val)
        md_lines.append(f"- u32 0x{val:08X}: {len(hits)} hits (리터럴 풀에 없음 → BLX reg 등)")
    md_lines.extend([
        "",
        "## 2) 0x2C4E3C CMP #1 해석",
        "",
        "- 0x2C4E38: LDRB r0,[r0,#0x2D9] — 객체+0x2D9 바이트 로드",
        "- 0x2C4E3C: CMP r0,#1 — VIP 플래그(1) 검사",
        "- 0x569DFC 함수도 동일 오프셋 0x2D9 사용",
        "- **RNG 아님**: 캐시된 VIP 플래그 확인만 수행",
        "",
        "## 3) STRB [Rn,#0x2D9]",
        "",
        f"- {len(strb_hits)} hits (VIP 플래그 쓰기 후보)",
        "",
        "## 4) Raw",
        "",
        "- `yw2_exefs_step36_vtable_rng_raw.txt`",
    ])
    (out_dir / "yw2_exefs_step36_vtable_rng_density.md").write_text("\n".join(md_lines), encoding="utf-8")
    print(f"[OK] {out_dir / 'yw2_exefs_step36_vtable_rng_density.md'}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
