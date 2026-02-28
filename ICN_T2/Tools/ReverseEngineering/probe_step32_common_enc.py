#!/usr/bin/env python3
"""
YW2 StreetPass EXEFS Step32: common_enc 테이블 4 슬롯/Weight 추출.
Common Encounter (0xD6181568) table containing Pandanoko(0xDB1CC069) 전체 슬롯 Weight 합계.
"""
from __future__ import annotations

import argparse
import struct
from pathlib import Path
from typing import List, Tuple, Optional

TARGET_HASH = 0xDB1CC069
SLOT_SIZE = 28  # ENCOUNT_CHARA: 7 ints from context (var_count=7)


def to_u32(data: bytes, offset: int) -> int:
    if offset + 4 > len(data):
        return 0
    return struct.unpack_from("<I", data, offset)[0]


def find_all(haystack: bytes, needle: bytes) -> List[int]:
    out: List[int] = []
    start = 0
    while True:
        idx = haystack.find(needle, start)
        if idx < 0:
            return out
        out.append(idx)
        start = idx + 1


def looks_like_slot(data: bytes, offset: int) -> bool:
    """Heuristic: ParamHash-like (0x8xxx/0x9xxx/0xAxxx), v1=Level 1-99, v2=Weight 1-1000."""
    h = to_u32(data, offset)
    v1 = to_u32(data, offset + 4)
    v2 = to_u32(data, offset + 8)
    if v1 == 0 or v1 > 99:
        return False
    if v2 == 0 or v2 > 10000:
        return False
    # Hash: typically 0x80000000-0xFFFFFFFF or 0x00000000-0x7FFFFFFF for small
    if h == 0 or h == 0xFFFFFFFF:
        return False
    return True


def extract_table_around_pandanoko(data: bytes, pandanoko_offset: int) -> dict:
    """
    Extract slots in the same table as Pandanoko.
    Heuristic: scan backward/forward by SLOT_SIZE until non-slot pattern.
    """
    slots: List[dict] = []
    # Align to slot boundary (28 bytes)
    base = (pandanoko_offset // SLOT_SIZE) * SLOT_SIZE
    # Scan backward
    start = base
    while start >= 0:
        if not looks_like_slot(data, start):
            break
        slots.insert(0, {
            "offset": start,
            "hash": to_u32(data, start),
            "v1": to_u32(data, start + 4),
            "v2": to_u32(data, start + 8),
            "v3": to_u32(data, start + 12),
            "v4": to_u32(data, start + 16),
        })
        start -= SLOT_SIZE
    # Scan forward from base + SLOT_SIZE (we already have base)
    start = base + SLOT_SIZE
    while start + SLOT_SIZE <= len(data):
        if not looks_like_slot(data, start):
            break
        slots.append({
            "offset": start,
            "hash": to_u32(data, start),
            "v1": to_u32(data, start + 4),
            "v2": to_u32(data, start + 8),
            "v3": to_u32(data, start + 12),
            "v4": to_u32(data, start + 16),
        })
        start += SLOT_SIZE

    total_weight = sum(s["v2"] for s in slots)
    pandanoko_slot = next((s for s in slots if s["hash"] == TARGET_HASH), None)
    return {
        "slots": slots,
        "total_weight": total_weight,
        "pandanoko_slot": pandanoko_slot,
        "slot_count": len(slots),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Step32 common_enc table 4 probe")
    parser.add_argument("--asset-root", default=r"C:\Users\home\Desktop\ICN_T2\Yw2Asset")
    parser.add_argument("--output-dir", default=r"md/04_Tech_Task/reports")
    args = parser.parse_args()
    root = Path(args.asset_root)
    out_dir = Path(args.output_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    romfs = root / "yw2_a.fa"
    if not romfs.exists():
        print(f"[ERROR] Not found: {romfs}")
        return 1

    data = romfs.read_bytes()
    needle = struct.pack("<I", TARGET_HASH)
    hits = find_all(data, needle)

    raw_lines: List[str] = []
    raw_lines.append("# YW2 Step32: common_enc 테이블 4 (Pandanoko 포함) 슬롯/Weight")
    raw_lines.append("")
    raw_lines.append(f"Pandanoko(0x{TARGET_HASH:08X}) hits: {len(hits)}")
    raw_lines.append("")

    all_results: List[dict] = []
    for hit in hits:
        result = extract_table_around_pandanoko(data, hit)
        all_results.append(result)
        raw_lines.append(f"## Hit @ 0x{hit:X}")
        raw_lines.append(f"  Slots in table: {result['slot_count']}, total_weight: {result['total_weight']}")
        if result["pandanoko_slot"]:
            p = result["pandanoko_slot"]
            raw_lines.append(f"  Pandanoko: v1={p['v1']} v2(Weight)={p['v2']} v3={p['v3']} v4={p['v4']}")
            if result["total_weight"] > 0:
                pct = 100.0 * p["v2"] / result["total_weight"]
                raw_lines.append(f"  P(slot) = {p['v2']}/{result['total_weight']} = {pct:.2f}%")
        raw_lines.append("")
        for s in result["slots"][:30]:
            raw_lines.append(f"  0x{s['offset']:X} hash=0x{s['hash']:08X} Lv={s['v1']} W={s['v2']}")
        if len(result["slots"]) > 30:
            raw_lines.append(f"  ... (+{len(result['slots'])-30} more)")
        raw_lines.append("")

    raw_path = out_dir / "yw2_exefs_step32_common_enc_table4_raw.txt"
    raw_path.write_text("\n".join(raw_lines), encoding="utf-8")
    print(f"[OK] Step32 Raw: {raw_path}")

    # Density
    best = all_results[0] if all_results else {}
    md_lines = [
        "# YW2 EXEFS Step32 Report (common_enc Table 4 Weight)",
        "",
        "- Date: 2026-02-28 (UTC)",
        "- Goal: Pandanoko 포함 테이블 total_weight 및 P(slot) 계산",
        "",
        "## 1) 결과",
        "",
    ]
    if best:
        md_lines.append(f"- Slot 수: {best['slot_count']}")
        md_lines.append(f"- total_weight: {best['total_weight']}")
        if best["pandanoko_slot"] and best["total_weight"] > 0:
            p = best["pandanoko_slot"]
            pct = 100.0 * p["v2"] / best["total_weight"]
            md_lines.append(f"- Pandanoko Weight: {p['v2']}")
            md_lines.append(f"- P(Pandanoko slot) = {p['v2']}/{best['total_weight']} = {pct:.2f}%")
    md_lines.extend([
        "",
        "## 2) Raw",
        "",
        "- `yw2_exefs_step32_common_enc_table4_raw.txt`",
        "",
        "## 3) Next",
        "",
        "- VIP 확률과 결합: P(VIP) * P(slot) = Pandanoko 등장 확률",
    ])
    (out_dir / "yw2_exefs_step32_common_enc_table4_density.md").write_text("\n".join(md_lines), encoding="utf-8")
    print(f"[OK] Step32 Density: {out_dir / 'yw2_exefs_step32_common_enc_table4_density.md'}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
