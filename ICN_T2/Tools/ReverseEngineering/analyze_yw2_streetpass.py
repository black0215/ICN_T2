#!/usr/bin/env python3
"""
YW2 StreetPass encounter baseline analyzer.

This script implements the plan artifacts for:
- code.bin CEC anchors and IPC constants
- romFS encounter cluster evidence
- save diff classification (A->B->C->D when available)
- reproducible JSON + Markdown report output
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import struct
import zlib
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple


CODE_BIN_NAME = "00040000001B2A00.code.bin"
ROMFS_NAME = "yw2_a.fa"
LANG_NAME = "yw2_lg_ko.fa"
SAVE_GAME1_NAME = "game1.yw"
SAVE_HEAD_NAME = "head.yw"

TARGET_PARAM_HASH = 0xDB1CC069


@dataclass(frozen=True)
class CecCallSite:
    address: int
    service_name: str
    ipc_header: Optional[int]
    xref_count: int
    notes: str


@dataclass(frozen=True)
class StreetPassEncounterPath:
    recv_func: str
    seed_func: str
    table_select_func: str
    vip_judge_func: str


@dataclass(frozen=True)
class EncounterEvidenceRow:
    source_file: str
    offset: int
    value_hex: str
    interpreted_role: str
    confidence: float


@dataclass(frozen=True)
class SaveStateCandidate:
    offset: int
    width: int
    before: str
    after: str
    class_tag: str


def crc32_shift_jis(text: str) -> int:
    return zlib.crc32(text.encode("shift_jis")) & 0xFFFFFFFF


def hex8(value: int) -> str:
    return f"0x{value:08X}"


def find_all(haystack: bytes, needle: bytes) -> List[int]:
    indexes: List[int] = []
    start = 0
    while True:
        idx = haystack.find(needle, start)
        if idx < 0:
            return indexes
        indexes.append(idx)
        start = idx + 1


def to_u32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def ascii_slice(data: bytes, start: int, end: int) -> str:
    out: List[str] = []
    for b in data[max(0, start): min(len(data), end)]:
        out.append(chr(b) if 32 <= b < 127 else ".")
    return "".join(out)


def score_target_hit(data: bytes, hit_offset: int) -> Tuple[str, float]:
    # Heuristic:
    # - Encounter slot-like blocks often have small level/weight-like ints nearby.
    # - We use a local window of 8 u32 values after the hit.
    values: List[int] = []
    for off in range(hit_offset + 4, hit_offset + 4 + (8 * 4), 4):
        if off + 4 <= len(data):
            values.append(to_u32(data, off))

    small_0_99 = sum(1 for v in values if 0 <= v <= 99)
    one_or_two = sum(1 for v in values if v in (1, 2))
    has_ten = any(v == 10 for v in values)
    score = min(1.0, 0.25 + (small_0_99 * 0.08) + (one_or_two * 0.05) + (0.1 if has_ten else 0.0))

    if score >= 0.65:
        role = "possible_encounter_slot_payload"
    elif score >= 0.45:
        role = "possible_struct_or_table_entry"
    else:
        role = "low_confidence_reference"

    return role, round(score, 3)


def detect_class_tag(width: int, before_bytes: bytes, after_bytes: bytes) -> str:
    if width == 1:
        return "tag"
    if width <= 4:
        b = int.from_bytes(before_bytes.ljust(4, b"\x00"), "little")
        a = int.from_bytes(after_bytes.ljust(4, b"\x00"), "little")
        delta = abs(a - b)
        if delta <= 16:
            return "counter"
        return "seed/hash"
    if width in (8, 12, 16):
        return "seed/hash"
    if width >= 32:
        return "blob"
    return "counter"


def diff_ranges(a: bytes, b: bytes) -> List[Tuple[int, int]]:
    n = min(len(a), len(b))
    ranges: List[Tuple[int, int]] = []
    i = 0
    while i < n:
        if a[i] != b[i]:
            start = i
            while i < n and a[i] != b[i]:
                i += 1
            ranges.append((start, i))
        else:
            i += 1
    if len(a) != len(b):
        ranges.append((n, max(len(a), len(b))))
    return ranges


def summarize_diff_pair(
    before_path: Path,
    after_path: Path,
    max_candidates: int = 128,
) -> Dict[str, object]:
    before = before_path.read_bytes()
    after = after_path.read_bytes()

    pairs = diff_ranges(before, after)
    candidates: List[SaveStateCandidate] = []
    for start, end in pairs[:max_candidates]:
        width = end - start
        bb = before[start:end]
        ab = after[start:end]
        candidates.append(
            SaveStateCandidate(
                offset=start,
                width=width,
                before=bb.hex(),
                after=ab.hex(),
                class_tag=detect_class_tag(width, bb, ab),
            )
        )

    return {
        "before": str(before_path),
        "after": str(after_path),
        "before_size": len(before),
        "after_size": len(after),
        "diff_range_count": len(pairs),
        "candidates": [asdict(c) for c in candidates],
    }


def scan_code_bin(code_path: Path) -> Dict[str, object]:
    data = code_path.read_bytes()
    anchors = {
        "cecd:u": b"cecd:u",
        "cecd:s": b"cecd:s",
        "common_enc_path": b"data/res/battle/common_enc_0.03a.cfg.bin",
        "yokaispot_common_path": b"data/res/map/yokaispot_common_0.03c.cfg.bin",
        "yokaispot_common_menu_path": b"data/res/map/yokaispot_common_menu_0.03c.cfg.bin",
    }
    ipc_headers = [0x000D0082, 0x00100042, 0x00110104, 0x000F0000]

    anchor_offsets: Dict[str, List[int]] = {
        name: find_all(data, token) for name, token in anchors.items()
    }

    ipc_offsets: Dict[str, List[int]] = {}
    for header in ipc_headers:
        ipc_offsets[hex8(header)] = find_all(data, struct.pack("<I", header))

    svc32_arm = find_all(data, bytes([0x32, 0x00, 0x00, 0xEF]))
    svc32_thumb = find_all(data, bytes([0x32, 0xDF]))

    callsites: List[CecCallSite] = []
    for off in anchor_offsets["cecd:u"]:
        callsites.append(
            CecCallSite(
                address=off,
                service_name="cecd:u",
                ipc_header=None,
                xref_count=-1,
                notes="string anchor; resolve xrefs in Ghidra",
            )
        )
    for off in anchor_offsets["cecd:s"]:
        callsites.append(
            CecCallSite(
                address=off,
                service_name="cecd:s",
                ipc_header=None,
                xref_count=-1,
                notes="string anchor; resolve xrefs in Ghidra",
            )
        )

    evidence_rows: List[EncounterEvidenceRow] = []
    for key, offsets in anchor_offsets.items():
        for off in offsets:
            evidence_rows.append(
                EncounterEvidenceRow(
                    source_file=code_path.name,
                    offset=off,
                    value_hex=hex8(off),
                    interpreted_role=f"code_anchor:{key}",
                    confidence=0.98,
                )
            )

    for header_hex, offsets in ipc_offsets.items():
        for off in offsets[:32]:
            evidence_rows.append(
                EncounterEvidenceRow(
                    source_file=code_path.name,
                    offset=off,
                    value_hex=header_hex,
                    interpreted_role="ipc_header_constant",
                    confidence=0.84,
                )
            )

    encounter_path = StreetPassEncounterPath(
        recv_func="UNRESOLVED(use cecd:u string xrefs in Ghidra)",
        seed_func="UNRESOLVED(trace RNG state update after CEC payload parse)",
        table_select_func="UNRESOLVED(trace common_enc/yokaispot path references)",
        vip_judge_func="UNRESOLVED(trace branch selecting VIP encounter state)",
    )

    return {
        "file": str(code_path),
        "anchors": {k: [hex8(v) for v in vals] for k, vals in anchor_offsets.items()},
        "ipc_header_hits": {k: [hex8(v) for v in vals] for k, vals in ipc_offsets.items()},
        "svc32_arm_count": len(svc32_arm),
        "svc32_arm_sample": [hex8(v) for v in svc32_arm[:24]],
        "svc32_thumb_count": len(svc32_thumb),
        "svc32_thumb_sample": [hex8(v) for v in svc32_thumb[:24]],
        "cec_call_sites": [asdict(c) for c in callsites],
        "streetpass_path": asdict(encounter_path),
        "evidence_rows": [asdict(r) for r in evidence_rows],
    }


def scan_romfs(romfs_path: Path) -> Dict[str, object]:
    data = romfs_path.read_bytes()
    markers = {
        "ENCOUNT_TABLE_BEGIN": b"ENCOUNT_TABLE_BEGIN",
        "ENCOUNT_CHARA_BEGIN": b"ENCOUNT_CHARA_BEGIN",
        "YS_YOKAI_BEGIN": b"YS_YOKAI_BEGIN",
        "YS_YOKAI": b"YS_YOKAI",
        "YS_YOKAI_END": b"YS_YOKAI_END",
    }
    marker_offsets = {k: find_all(data, v) for k, v in markers.items()}

    target_hits = find_all(data, struct.pack("<I", TARGET_PARAM_HASH))
    prioritized = 0x37F33238
    ranked_hits = sorted(target_hits, key=lambda x: abs(x - prioritized))

    hit_details: List[Dict[str, object]] = []
    evidence_rows: List[EncounterEvidenceRow] = []
    for hit in ranked_hits:
        role, confidence = score_target_hit(data, hit)
        block_preview = []
        for off in range(hit - 0x20, hit + 0x30, 4):
            if 0 <= off <= len(data) - 4:
                block_preview.append({"offset": hex8(off), "u32": hex8(to_u32(data, off))})

        detail = {
            "offset": hex8(hit),
            "role": role,
            "confidence": confidence,
            "ascii_window": ascii_slice(data, hit - 96, hit + 128),
            "u32_preview": block_preview,
        }
        hit_details.append(detail)
        evidence_rows.append(
            EncounterEvidenceRow(
                source_file=romfs_path.name,
                offset=hit,
                value_hex=hex8(TARGET_PARAM_HASH),
                interpreted_role=role,
                confidence=confidence,
            )
        )

    crc_targets = {
        "common_enc": crc32_shift_jis("common_enc"),
        "t103g00": crc32_shift_jis("t103g00"),
        "t103i69": crc32_shift_jis("t103i69"),
        "yokaispot": crc32_shift_jis("yokaispot"),
        "ys_yokai": crc32_shift_jis("ys_yokai"),
    }

    crc_counts = {}
    for key, value in crc_targets.items():
        crc_counts[key] = len(find_all(data, struct.pack("<I", value)))

    return {
        "file": str(romfs_path),
        "marker_offsets": {k: [hex8(v) for v in vals[:256]] for k, vals in marker_offsets.items()},
        "target_param_hash": hex8(TARGET_PARAM_HASH),
        "target_param_hits": [hex8(v) for v in target_hits],
        "target_param_hit_details": hit_details,
        "crc32_shift_jis": {k: hex8(v) for k, v in crc_targets.items()},
        "crc32_hit_count_in_romfs_u32": crc_counts,
        "evidence_rows": [asdict(r) for r in evidence_rows],
    }


def build_pseudocode() -> str:
    return "\n".join(
        [
            "fn ResolveStreetPassEncounter(cec_payload, runtime_state, save_state) -> EncounterResult {",
            "    recv = ParseCecMessage(cec_payload);",
            "    key = NormalizePeerIdentity(recv);",
            "    seed = UpdateEncounterSeed(runtime_state.seed, key, recv.timestamp);",
            "    table = SelectEncounterTable(seed, \"common_enc\", \"yokaispot_common\");",
            "    slot = SelectEncounterSlot(table, seed);",
            "    vip = JudgeVipRoomSpawn(seed, save_state.streetpass_flags);",
            "    if vip {",
            "        slot = SelectVipSlot(table, seed);",
            "    }",
            "    save_state.streetpass_flags = UpdateStreetPassFlags(save_state.streetpass_flags, vip);",
            "    return EncounterResult(table, slot, slot.param_hash, vip);",
            "}",
        ]
    )


def write_markdown_report(report: Dict[str, object], output_md: Path) -> None:
    code = report["code_scan"]
    rom = report["romfs_scan"]
    save = report["save_scan"]

    lines: List[str] = []
    lines.append("# YW2 StreetPass Reverse Engineering Baseline Report")
    lines.append("")
    lines.append(f"- Generated (UTC): `{report['generated_utc']}`")
    lines.append(f"- Asset root: `{report['asset_root']}`")
    lines.append("")
    lines.append("## Hash Checks")
    lines.append("")
    for key, value in report["hash_checks"].items():
        lines.append(f"- `{key}` = `{value}`")
    lines.append("")
    lines.append("## code.bin Anchors")
    lines.append("")
    for key, vals in code["anchors"].items():
        lines.append(f"- `{key}`: {', '.join(vals) if vals else '(none)'}")
    lines.append("")
    lines.append("## code.bin IPC Header Hits")
    lines.append("")
    for key, vals in code["ipc_header_hits"].items():
        sample = ", ".join(vals[:10]) if vals else "(none)"
        lines.append(f"- `{key}` count={len(vals)} sample={sample}")
    lines.append("")
    lines.append("## romFS Target Hash Hits")
    lines.append("")
    lines.append(f"- target `{rom['target_param_hash']}` at: {', '.join(rom['target_param_hits'])}")
    lines.append("")
    lines.append("## Save Diff Status")
    lines.append("")
    if save["pairs"]:
        for pair in save["pairs"]:
            lines.append(
                f"- `{Path(pair['before']).name}` -> `{Path(pair['after']).name}`: "
                f"{pair['diff_range_count']} diff ranges"
            )
    else:
        lines.append("- No diff pairs were provided (A/B/C/D not complete yet).")
    lines.append("")
    lines.append("## StreetPass Pseudocode")
    lines.append("")
    lines.append("```text")
    lines.append(report["pseudocode"])
    lines.append("```")
    lines.append("")
    lines.append("## Notes")
    lines.append("")
    lines.append("- `game2.yw` is intentionally excluded from baseline comparison.")
    lines.append("- Use Ghidra XREF on anchors to replace unresolved function placeholders.")
    lines.append("")

    output_md.parent.mkdir(parents=True, exist_ok=True)
    output_md.write_text("\n".join(lines), encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Analyze YW2 StreetPass encounter anchors.")
    parser.add_argument(
        "--asset-root",
        default=r"C:\Users\home\Desktop\ICN_T2\Yw2Asset",
        help="Path containing code.bin, romFS, and save files.",
    )
    parser.add_argument(
        "--output-dir",
        default=r"md/04_Tech_Task/reports",
        help="Output directory for JSON/MD reports.",
    )
    parser.add_argument("--save-b", default="", help="Optional B snapshot game1.yw path (after 1 StreetPass).")
    parser.add_argument("--save-c", default="", help="Optional C snapshot game1.yw path (after VIP entry).")
    parser.add_argument("--save-d", default="", help="Optional D snapshot game1.yw path (after VIP encounter consumed).")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    asset_root = Path(args.asset_root)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    code_path = asset_root / CODE_BIN_NAME
    romfs_path = asset_root / ROMFS_NAME
    lang_path = asset_root / LANG_NAME
    save_a = asset_root / SAVE_GAME1_NAME
    head_a = asset_root / SAVE_HEAD_NAME

    required = [code_path, romfs_path, lang_path, save_a, head_a]
    missing = [str(p) for p in required if not p.exists()]
    if missing:
        raise FileNotFoundError(f"Missing required asset files: {missing}")

    hash_checks = {
        'CRC32("common_enc")': hex8(crc32_shift_jis("common_enc")),
        'CRC32("t103g00")': hex8(crc32_shift_jis("t103g00")),
        'CRC32("t103i69")': hex8(crc32_shift_jis("t103i69")),
    }

    code_scan = scan_code_bin(code_path)
    romfs_scan = scan_romfs(romfs_path)

    diff_pairs: List[Dict[str, object]] = []
    # Save baseline A is always present. Pair analysis requires optional snapshots.
    optional = [("B", args.save_b), ("C", args.save_c), ("D", args.save_d)]
    pair_chain: List[Tuple[Path, Path]] = []
    prev = save_a
    for label, path_str in optional:
        if not path_str:
            continue
        curr = Path(path_str)
        if not curr.exists():
            raise FileNotFoundError(f"{label} snapshot not found: {curr}")
        pair_chain.append((prev, curr))
        prev = curr

    for before, after in pair_chain:
        diff_pairs.append(summarize_diff_pair(before, after))

    save_scan = {
        "baseline_game1": str(save_a),
        "baseline_head": str(head_a),
        "pairs": diff_pairs,
    }

    report = {
        "generated_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "asset_root": str(asset_root),
        "hash_checks": hash_checks,
        "code_scan": code_scan,
        "romfs_scan": romfs_scan,
        "save_scan": save_scan,
        "pseudocode": build_pseudocode(),
        "assumptions": [
            "0xDB1CC069 is treated as target ParamHash.",
            "common_enc hash is handled as runtime reference value.",
            "game2.yw is excluded from baseline save diff.",
        ],
    }

    output_json = output_dir / "yw2_streetpass_baseline.json"
    output_md = output_dir / "yw2_streetpass_baseline.md"
    output_json.write_text(json.dumps(report, indent=2), encoding="utf-8")
    write_markdown_report(report, output_md)

    print(f"[OK] JSON report: {output_json}")
    print(f"[OK] MD report  : {output_md}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
