#!/usr/bin/env python3
"""
YW2 StreetPass EXEFS Step31 + Step33 probe.
Step31: VIP CMP 후보(0x2C4E3C, 0x552928) 직전 RNG 역추적
Step33: Bank +0x34/+0x5C (0x538410, 0x5384E0) 호출자 전수 스캔
"""
from __future__ import annotations

import argparse
import struct
from pathlib import Path
from typing import List, Tuple, Optional

def to_u32(data: bytes, offset: int) -> int:
    if offset + 4 > len(data):
        return 0
    return struct.unpack_from("<I", data, offset)[0]


def decode_bl_target(insn: int, pc: int) -> int:
    """ARM BL: target = PC + 8 + (sign_extend(imm24) << 2)."""
    imm24 = insn & 0xFFFFFF
    if imm24 & 0x800000:
        imm24 |= 0xFF000000  # sign extend
    return pc + 8 + (imm24 << 2)


def find_bl_to_target(data: bytes, target: int) -> List[int]:
    """Find all BL instructions that branch to target. Returns caller offsets."""
    callers: List[int] = []
    for off in range(0, len(data) - 4, 4):
        insn = to_u32(data, off)
        if (insn & 0xFF000000) == 0xEB000000:  # BL
            t = decode_bl_target(insn, off)
            if t == target:
                callers.append(off)
    return callers


def dump_region(data: bytes, start: int, size: int, label: str) -> List[str]:
    """Dump instructions in region for analysis."""
    lines: List[str] = []
    lines.append(f"## {label} (0x{start:06X} + 0x{size:X})")
    lines.append("")
    for i in range(0, min(size, len(data) - start), 4):
        off = start + i
        u = to_u32(data, off)
        # Simple decode: BL, CMP
        bl_target = ""
        if (u & 0xFF000000) == 0xEB000000:
            t = decode_bl_target(u, off)
            bl_target = f" -> 0x{t:06X}"
        cmp_info = ""
        if (u & 0x0FF0F000) == 0x03500000:
            imm12 = u & 0xFFF
            cmp_info = f" CMP #imm12={imm12}"
        lines.append(f"  0x{off:06X}: 0x{u:08X}{bl_target}{cmp_info}")
    return lines


def probe_step31(data: bytes, candidates: List[int]) -> Tuple[List[str], dict]:
    """Step31: RNG 역추적 - CMP 후보 직전 0x80바이트 내 BL/CMP 패턴."""
    raw_lines: List[str] = []
    raw_lines.append("# YW2 Step31: VIP CMP 후보 RNG 역추적")
    raw_lines.append("")
    result: dict = {"candidates": [], "bl_before_cmp": []}
    for cmp_off in candidates:
        region_start = max(0, cmp_off - 0x80)
        region_end = min(len(data), cmp_off + 0x20)
        raw_lines.extend(dump_region(data, region_start, region_end - region_start,
                                     f"CMP @ 0x{cmp_off:06X}"))
        raw_lines.append("")
        # Collect BL in [cmp-0x80, cmp)
        bl_list: List[Tuple[int, int]] = []
        for off in range(region_start, cmp_off, 4):
            insn = to_u32(data, off)
            if (insn & 0xFF000000) == 0xEB000000:
                t = decode_bl_target(insn, off)
                bl_list.append((off, t))
        result["candidates"].append({"cmp": cmp_off, "bl_before": bl_list})
        for (call_off, tgt) in bl_list:
            result["bl_before_cmp"].append({"caller": call_off, "target": tgt, "cmp": cmp_off})
    return raw_lines, result


def probe_step33(data: bytes, targets: List[int]) -> Tuple[List[str], dict]:
    """Step33: 0x538410, 0x5384E0 호출자 전수 스캔."""
    raw_lines: List[str] = []
    raw_lines.append("# YW2 Step33: Bank +0x34/+0x5C 호출자 분리")
    raw_lines.append("")
    result: dict = {"callers_538410": [], "callers_5384E0": []}
    for tgt in targets:
        callers = find_bl_to_target(data, tgt)
        label = "0x538410 (+0x34)" if tgt == 0x538410 else "0x5384E0 (+0x5C)"
        raw_lines.append(f"## {label}: {len(callers)} callers")
        raw_lines.append("")
        for c in callers:
            raw_lines.append(f"  0x{c:06X}")
            if tgt == 0x538410:
                result["callers_538410"].append(c)
            else:
                result["callers_5384E0"].append(c)
        raw_lines.append("")
    return raw_lines, result


def main() -> int:
    parser = argparse.ArgumentParser(description="Step31+33 probe")
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

    # Step31: VIP CMP 후보
    vip_cmp_candidates = [0x2C4E3C, 0x552928, 0x55295C]
    s31_raw, s31_result = probe_step31(data, vip_cmp_candidates)

    s31_path = out_dir / "yw2_exefs_step31_vip_cmp_rng_raw.txt"
    s31_path.write_text("\n".join(s31_raw), encoding="utf-8")
    print(f"[OK] Step31 Raw: {s31_path}")

    # Step33: Bank callers
    bank_targets = [0x538410, 0x5384E0]
    s33_raw, s33_result = probe_step33(data, bank_targets)

    s33_path = out_dir / "yw2_exefs_step33_bank34_5c_callers_raw.txt"
    s33_path.write_text("\n".join(s33_raw), encoding="utf-8")
    print(f"[OK] Step33 Raw: {s33_path}")

    # Step31 density
    s31_md = [
        "# YW2 EXEFS Step31 Report (VIP CMP RNG 역추적)",
        "",
        "- Date: 2026-02-28 (UTC)",
        "- Goal: 0x2C4E3C, 0x552928 등 CMP 직전 RNG 호출 확인",
        "",
        "## 1) CMP 후보 직전 BL 목록",
        "",
    ]
    for c in s31_result["candidates"]:
        s31_md.append(f"- CMP @ 0x{c['cmp']:06X}: BL {len(c['bl_before'])}개 직전")
        for (co, tgt) in c["bl_before"][:5]:
            s31_md.append(f"  - 0x{co:06X} -> 0x{tgt:06X}")
    s31_md.extend([
        "",
        "## 2) Raw",
        "",
        "- `yw2_exefs_step31_vip_cmp_rng_raw.txt`",
        "",
        "## 3) Next",
        "",
        "- RNG 후보 함수(반복 호출, 0~N 범위 반환) 식별 후 CMP 레지스터 소스 추적",
    ])
    (out_dir / "yw2_exefs_step31_vip_cmp_rng_density.md").write_text("\n".join(s31_md), encoding="utf-8")

    # Step33 density
    s33_md = [
        "# YW2 EXEFS Step33 Report (Bank +0x34/+0x5C 호출자)",
        "",
        "- Date: 2026-02-28 (UTC)",
        "- Goal: 0x538410(+0x34), 0x5384E0(+0x5C) direct caller 분리",
        "",
        "## 1) 결과",
        "",
        f"- 0x538410: {len(s33_result['callers_538410'])} callers",
        f"- 0x5384E0: {len(s33_result['callers_5384E0'])} callers",
        "",
    ]
    for addr in s33_result["callers_538410"][:15]:
        s33_md.append(f"- 0x538410 caller: 0x{addr:06X}")
    for addr in s33_result["callers_5384E0"][:15]:
        s33_md.append(f"- 0x5384E0 caller: 0x{addr:06X}")
    s33_md.extend([
        "",
        "## 2) Raw",
        "",
        "- `yw2_exefs_step33_bank34_5c_callers_raw.txt`",
        "",
        "## 3) Next",
        "",
        "- 각 caller 상위 함수/객체 타입 분류, 0x81640C vs 0x816414 경로와 교차",
    ])
    (out_dir / "yw2_exefs_step33_bank34_5c_callers_density.md").write_text("\n".join(s33_md), encoding="utf-8")

    # Step33 follow-up: parent callers of 0x2383F0 and 0x238444, and shared function dump
    parent_2383f0 = find_bl_to_target(data, 0x2383F0)
    parent_238444 = find_bl_to_target(data, 0x238444)
    fu_raw: List[str] = [
        "# YW2 Step33 follow-up: Bank caller parent + 0x2383F0..0x238460 dump",
        "",
        "## Parent callers (who calls 0x2383F0 / 0x238444)",
        "",
        f"  BL -> 0x2383F0: {len(parent_2383f0)} callers",
    ]
    for a in parent_2383f0:
        fu_raw.append(f"    0x{a:06X}")
    fu_raw.append("")
    fu_raw.append(f"  BL -> 0x238444: {len(parent_238444)} callers")
    for a in parent_238444:
        fu_raw.append(f"    0x{a:06X}")
    fu_raw.append("")
    fu_raw.extend(dump_region(data, 0x2383F0, 0x70, "0x2383F0..0x238460 (0x538410/0x5384E0 call block)"))
    (out_dir / "yw2_exefs_step33_bank_callers_parent_raw.txt").write_text("\n".join(fu_raw), encoding="utf-8")
    print(f"[OK] Step33 follow-up: yw2_exefs_step33_bank_callers_parent_raw.txt")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
