#!/usr/bin/env python3
"""
YW2 Step37B: Pandanoko 슬롯 raw Variables 덤프 (CfgBin 파서 수준).
common_enc_0.03a.cfg.bin에서 ENCOUNT_CHARA_BEGIN의 첫 자식 엔트리의 Variables를 그대로 출력.
"""
from __future__ import annotations

import argparse
import struct
from pathlib import Path
from typing import List, Optional, Tuple

TARGET_HASH = 0xDB1CC069

def to_u32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0] if offset + 4 <= len(data) else 0

def to_u16(data: bytes, offset: int) -> int:
    return struct.unpack_from("<H", data, offset)[0] if offset + 2 <= len(data) else 0

def to_u8(data: bytes, offset: int) -> int:
    return data[offset] if offset < len(data) else 0


class SimpleEntry:
    def __init__(self, name_hash: int, var_count: int, vars: list, children: list):
        self.name_hash = name_hash
        self.var_count = var_count
        self.vars = vars  # list of (type, value)
        self.children = children  # list of SimpleEntry


def parse_cfgbin(data: bytes) -> Tuple[Optional[List[SimpleEntry]], Optional[List[str]]]:
    """Very minimal CfgBin parser for type-0 (flat) format."""
    if len(data) < 8:
        return None, ["Too short"]
    magic = data[:4]
    ver = to_u32(data, 4)
    
    # Use simple scanning to find ENCOUNT_CHARA_BEGIN and child entries
    # rather than full CfgBin parse
    return None, ["Use flat scan"]


def find_pandanoko_raw(romfs_path: Path, common_enc_rel: str) -> List[str]:
    """Scan yw2_a.fa for common_enc, find Pandanoko entry, dump raw u32 context."""
    out: List[str] = []
    data = romfs_path.read_bytes()
    needle = struct.pack("<I", TARGET_HASH)
    idx = 0
    hits: List[int] = []
    while True:
        pos = data.find(needle, idx)
        if pos < 0:
            break
        hits.append(pos)
        idx = pos + 1

    out.append(f"# common_enc Pandanoko(0x{TARGET_HASH:08X}) raw context")
    out.append(f"Hits: {len(hits)}")
    for hit in hits[:5]:
        out.append(f"\n## Hit @ 0x{hit:X}")
        # Dump -0x50 ~ +0x80 as u32 sequence
        start = max(0, hit - 0x50)
        end = min(len(data), hit + 0x80)
        for off in range(start, end, 4):
            u = to_u32(data, off)
            marker = " <-- PANDANOKO" if off == hit else ""
            out.append(f"  0x{off:X}: 0x{u:08X} ({u:10d}){marker}")
    return out


def find_pandanoko_in_common_enc_file(data: bytes) -> List[str]:
    """Find Pandanoko hash in raw common_enc bytes and extract variable sequence."""
    out: List[str] = []
    needle = struct.pack("<I", TARGET_HASH)
    idx = data.find(needle)
    if idx < 0:
        out.append("Pandanoko hash not found in file.")
        return out

    out.append(f"Found @ file offset 0x{idx:X}")
    out.append("")

    # Scan backward to find entry start (look for small var_count byte)
    # CfgBin entries: typically 1-byte var_count before variables
    # Try: read 7 u32 values starting from idx (entry row)
    out.append("## u32 row starting at hit (7 values = 1 slot):")
    for i in range(8):
        u = to_u32(data, idx + i * 4)
        out.append(f"  v[{i}] = 0x{u:08X} = {u}")
    out.append("")

    # Also check -4 offset
    out.append("## u32 row starting at hit-4 (if hash is v[1]):")
    for i in range(8):
        u = to_u32(data, idx - 4 + i * 4)
        out.append(f"  v[{i}] = 0x{u:08X} = {u}")

    return out


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--asset-root", default=r"C:\Users\home\Desktop\ICN_T2\Yw2Asset")
    parser.add_argument("--output-dir", default=r"md/04_Tech_Task/reports")
    args = parser.parse_args()
    out_dir = Path(args.output_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    # yw2_a.fa context scan
    romfs = Path(args.asset_root) / "yw2_a.fa"
    lines: List[str] = []
    if romfs.exists():
        lines = find_pandanoko_raw(romfs, "data/res/battle/common_enc_0.03a.cfg.bin")
    else:
        lines = ["yw2_a.fa not found."]

    out_path = out_dir / "yw2_exefs_step37b_slot_vars_raw.txt"
    out_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"[OK] {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
