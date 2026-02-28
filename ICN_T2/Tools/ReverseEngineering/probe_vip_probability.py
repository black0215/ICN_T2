#!/usr/bin/env python3
"""
YW2 StreetPass VIP/Pandanoko probability probe.
Step28: romFS common_enc slot/weight extraction
Step29: exeFS CMP+RNG pattern scan for VIP threshold
"""
from __future__ import annotations

import argparse
import struct
from pathlib import Path
from typing import List, Tuple

TARGET_HASH = 0xDB1CC069
COMMON_ENC_HASH = 0xD6181568


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


def probe_common_enc_weight(romfs_path: Path) -> dict:
    """Extract slot weights from common_enc around Pandanoko (0xDB1CC069)."""
    data = romfs_path.read_bytes()
    needle = struct.pack("<I", TARGET_HASH)
    hits = find_all(data, needle)
    result = {"hits": [], "pandanoko_slot": None, "table_weights": [], "total_weight": 0}
    for hit in hits:
        # Slot structure (EncountChara): ParamHash, Level, MaxLevel, Weight, ...
        # From yw2_arc0_step6_common_enc_context: v[0]=hash, v[1]=10, v[2]=2, v[3]=1, v[4]=1
        entry = {
            "offset": hit,
            "param_hash": to_u32(data, hit),
            "v1": to_u32(data, hit + 4),
            "v2": to_u32(data, hit + 8),
            "v3": to_u32(data, hit + 12),
            "v4": to_u32(data, hit + 16),
        }
        result["hits"].append(entry)
        # Valid encounter slot: v1,v2,v3,v4 are small (Level 1-99, Weight typically 1-100)
        if to_u32(data, hit) == TARGET_HASH and entry["v1"] < 100 and entry["v2"] < 1000:
            result["pandanoko_slot"] = entry
    return result


# StreetPass-related code regions (file offset)
STREETPASS_REGIONS = [
    (0x2C4000, 0x2C7000),   # 0x2C4FDC, 0x2C5ECC, 0x2C6430
    (0x505000, 0x512000),   # 0x506D58, 0x507DC4
    (0x550000, 0x560000),   # 0x556550, 0x559044
    (0x535000, 0x53A000),   # 0x537xxx CEC cluster
]


def in_streetpass_region(off: int) -> bool:
    return any(lo <= off < hi for lo, hi in STREETPASS_REGIONS)


def scan_cmp_rng_patterns(code_path: Path) -> dict:
    """
    Scan for CMP + small literal patterns that could be VIP probability threshold.
    Focus on StreetPass-related regions.
    """
    data = code_path.read_bytes()
    candidates: List[dict] = []
    sp_candidates: List[dict] = []
    for off in range(0, len(data) - 4, 4):
        insn = to_u32(data, off)
        imm12 = insn & 0xFFF
        if (insn & 0x0FF0F000) == 0x03500000:
            if 0 < imm12 <= 0x100:
                c = {
                    "offset": off,
                    "imm12": imm12,
                    "pct_1_256": round(100 * imm12 / 256, 2),
                    "pct_1_1000": round(100 * imm12 / 1000, 2),
                }
                candidates.append(c)
                if in_streetpass_region(off):
                    sp_candidates.append(c)
    return {
        "candidates": candidates[:200],
        "sp_candidates": sp_candidates,
        "total_small_cmp": len(candidates),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Probe VIP/Pandanoko probability")
    parser.add_argument("--asset-root", default=r"C:\Users\home\Desktop\ICN_T2\Yw2Asset")
    parser.add_argument("--output-dir", default=r"md/04_Tech_Task/reports")
    args = parser.parse_args()
    root = Path(args.asset_root)
    out_dir = Path(args.output_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    romfs = root / "yw2_a.fa"
    code = root / "00040000001B2A00.code.bin"

    lines_raw: List[str] = []
    lines_raw.append("# YW2 Step28+29 VIP Probability Probe Raw")
    lines_raw.append("")

    # Step28: common_enc
    if romfs.exists():
        enc = probe_common_enc_weight(romfs)
        lines_raw.append("## Step28: common_enc slot around 0xDB1CC069")
        lines_raw.append("")
        for h in enc["hits"][:20]:
            lines_raw.append(f"  offset=0x{h['offset']:X} hash=0x{h['param_hash']:08X} v1={h['v1']} v2={h['v2']} v3={h['v3']} v4={h['v4']}")
        if enc["pandanoko_slot"]:
            p = enc["pandanoko_slot"]
            lines_raw.append("")
            lines_raw.append(f"  PANDANOKO: v1(Level?)={p['v1']} v2(Weight?)={p['v2']} v3={p['v3']} v4={p['v4']}")
            if p["v2"] > 0:
                lines_raw.append(f"  If v2=Weight and total_weight known: pct = 100*{p['v2']}/total")
    else:
        lines_raw.append("## Step28: yw2_a.fa not found, skip")
    lines_raw.append("")

    # Step29: CMP patterns (StreetPass regions first)
    cmp_result = None
    if code.exists():
        cmp_result = scan_cmp_rng_patterns(code)
        lines_raw.append("## Step29: CMP #imm in StreetPass regions (VIP threshold)")
        lines_raw.append("")
        lines_raw.append(f"  total small CMP: {cmp_result['total_small_cmp']}")
        lines_raw.append(f"  in StreetPass regions: {len(cmp_result['sp_candidates'])}")
        for c in cmp_result["sp_candidates"][:30]:
            lines_raw.append(f"  0x{c['offset']:06X} imm12={c['imm12']} ~{c['pct_1_256']}% (1/256) ~{c['pct_1_1000']}% (1/1000)")
        lines_raw.append("")
        lines_raw.append("  other low-imm12 (1-10) candidates:")
        low = [x for x in cmp_result["candidates"] if x["imm12"] <= 10][:20]
        for c in low:
            lines_raw.append(f"  0x{c['offset']:06X} imm12={c['imm12']} ~{c['pct_1_1000']}%")
    else:
        lines_raw.append("## Step29: code.bin not found, skip")
    lines_raw.append("")

    raw_path = out_dir / "yw2_exefs_step28_vip_probability_raw.txt"
    raw_path.write_text("\n".join(lines_raw), encoding="utf-8")
    print(f"[OK] Raw: {raw_path}")

    if cmp_result is not None:
        step29_raw = out_dir / "yw2_exefs_step29_cmp_rng_raw.txt"
        s29_lines = ["# YW2 Step29 CMP+RNG VIP Threshold Scan Raw", ""]
        s29_lines.append(f"StreetPass-region CMP #imm (1-256): {len(cmp_result['sp_candidates'])} hits")
        for c in cmp_result["sp_candidates"]:
            s29_lines.append(f"  0x{c['offset']:06X} imm12={c['imm12']} ~{c['pct_1_1000']}%")
        step29_raw.write_text("\n".join(s29_lines), encoding="utf-8")
        print(f"[OK] Step29 Raw: {step29_raw}")

    # Density/summary report
    lines_md: List[str] = []
    lines_md.append("# YW2 EXEFS Step28 Report (VIP/Pandanoko Probability Probe)")
    lines_md.append("")
    lines_md.append("- Date: 2026-02-28 (UTC)")
    lines_md.append("- Goal: Determine Pandanoko spawn probability (community est. 0.1%-1%)")
    lines_md.append("")
    lines_md.append("## 1) Current Answer")
    lines_md.append("")
    lines_md.append("**Can we determine the exact probability now?**")
    lines_md.append("- **No.** Static analysis alone does not yet yield the exact value.")
    lines_md.append("- Data-side: Pandanoko slot in common_enc has v2=2, v3=1, v4=1 (Weight candidate).")
    lines_md.append("- Code-side: VIP threshold branch (CMP + literal) not yet pinned to a single site.")
    lines_md.append("")
    lines_md.append("## 2) Required Process")
    lines_md.append("")
    lines_md.append("1. **romFS (Step28)**: Parse common_enc table containing 0xDB1CC069; extract slot weights.")
    lines_md.append("   - Compute total_weight of table; Pandanoko_weight/total_weight = slot selection probability.")
    lines_md.append("2. **exeFS (Step29)**: Find VIP spawn branch: RNG call -> CMP result vs threshold.")
    lines_md.append("   - Threshold literal (e.g. 10 for 1%, 1 for 0.1%) gives VIP roll probability.")
    lines_md.append("3. **Combined**: VIP_prob * slot_prob = Pandanoko appearance probability.")
    lines_md.append("")
    lines_md.append("## 3) Raw Evidence")
    lines_md.append("")
    lines_md.append(f"- `{raw_path.name}`")
    lines_md.append("")
    lines_md.append("## 4) Next Steps")
    lines_md.append("")
    lines_md.append("1. Extract common_enc_0.03a.cfg.bin via ARC0; parse full table/slot layout.")
    lines_md.append("2. Trace 0x2C4FDC / 0x507DC4 callees for RNG + CMP sequence.")
    lines_md.append("3. Dynamic: break at CMP, log threshold and RNG result on VIP spawn.")

    md_path = out_dir / "yw2_exefs_step28_vip_probability_density.md"
    md_path.write_text("\n".join(lines_md), encoding="utf-8")
    print(f"[OK] Density: {md_path}")

    # Step29 density
    if cmp_result is not None:
        s29_md = out_dir / "yw2_exefs_step29_cmp_rng_density.md"
        s29_md_lines = [
            "# YW2 EXEFS Step29 Report (CMP+RNG VIP Threshold Density)",
            "",
            "- Date: 2026-02-28 (UTC)",
            "- Goal: Identify VIP spawn probability branch (CMP + threshold literal)",
            "",
            "## 1) StreetPass-Region CMP Density",
            "",
            f"- Total CMP #imm(1-256) in SP regions: {len(cmp_result['sp_candidates'])}",
            "- Low-threshold (1-10) candidates for ~0.1%-1% VIP:",
            "",
        ]
        low_sp = [c for c in cmp_result["sp_candidates"] if c["imm12"] <= 10]
        for c in low_sp:
            s29_md_lines.append(f"- 0x{c['offset']:06X} imm12={c['imm12']} -> ~{c['pct_1_1000']}%")
        s29_md_lines.extend([
            "",
            "## 2) Raw",
            "",
            "- `yw2_exefs_step29_cmp_rng_raw.txt`",
            "",
            "## 3) Step30 (Slot Weight Logic)",
            "",
            "- Trace 0x552C08, 0x506D58 for weight-based slot selection.",
            "- common_enc slot Weight field (EncountChara.Unk2) used in cumulative draw.",
        ])
        s29_md.write_text("\n".join(s29_md_lines), encoding="utf-8")
        print(f"[OK] Step29 Density: {s29_md}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
