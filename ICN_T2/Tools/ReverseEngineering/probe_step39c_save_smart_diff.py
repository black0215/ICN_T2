#!/usr/bin/env python3
"""
YW2 Step39C: 세이브 파일 스마트 diff
게임 데이터가 달라도 StreetPass 관련 변화만 추출.
전략:
  1. 엇갈림 통신 카운터 후보: 소폭 증가(+1~+100) u32 값
  2. 단독 변화 블록 (주변 변화 없는 독립 4바이트 변화)
  3. 연속 변화 블록 크기별 분류 (작은 블록 = 플래그/카운터, 큰 블록 = 데이터)
  4. 특정 오프셋 범위의 변화 (저장 포맷상 헤더/카운터 영역 등)
"""
from __future__ import annotations

import struct
from pathlib import Path
from typing import List, Tuple, Set

def u32(data: bytes, off: int) -> int:
    return struct.unpack_from("<I", data, off)[0] if off + 4 <= len(data) else 0

def u16(data: bytes, off: int) -> int:
    return struct.unpack_from("<H", data, off)[0] if off + 2 <= len(data) else 0


def main():
    before_path = Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset\gameBefor.yw")
    after_path  = Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset\gameafter.yw")
    out_dir = Path("md/04_Tech_Task/reports")
    out_dir.mkdir(parents=True, exist_ok=True)

    a = before_path.read_bytes()
    b = after_path.read_bytes()
    sz = min(len(a), len(b))

    print(f"Before: {len(a)} bytes | After: {len(b)} bytes")

    # ─── 1) 전체 diff 맵 ───────────────────────────────
    changed: List[int] = []
    for i in range(sz):
        if a[i] != b[i]:
            changed.append(i)

    print(f"변경 바이트: {len(changed):,}개")

    # ─── 2) 변화 군집 구성 ────────────────────────────
    groups: List[List[int]] = []
    if changed:
        cur = [changed[0]]
        for ci in changed[1:]:
            if ci - cur[-1] <= 4:
                cur.append(ci)
            else:
                groups.append(cur)
                cur = [ci]
        groups.append(cur)

    print(f"변화 군집: {len(groups):,}개")

    lines = [
        "# YW2 세이브 diff: gameBefor.yw vs gameafter.yw",
        f"# Before={len(a)}B / After={len(b)}B / 변경바이트={len(changed):,} / 군집={len(groups):,}",
        "",
    ]

    # ─── 3) 소폭 카운터 변화 (u32 +1~+500) ────────────
    lines.append("## [A] 소폭 카운터 변화 (+1 ~ +500, u32 기준)")
    counter_hits = []
    for off in range(0, sz - 4, 4):
        va = u32(a, off)
        vb = u32(b, off)
        if va != vb and 1 <= (vb - va) <= 500:
            counter_hits.append((off, va, vb, vb - va))
    for off, va, vb, d in counter_hits:
        lines.append(f"  0x{off:05X}: {va} → {vb}  (+{d})")
    lines.append(f"  총 {len(counter_hits)}개\n")

    # ─── 4) 독립 단일 블록 (4바이트, 주변 16바이트 변화 없음) ───
    lines.append("## [B] 독립 4바이트 변화 (주변 ±16B 변화 없는 것)")
    changed_set: Set[int] = set(changed)
    isolated = []
    for off in range(0, sz - 4, 4):
        va = u32(a, off)
        vb = u32(b, off)
        if va == vb:
            continue
        # 주변 ±16바이트에 다른 변화가 없는지
        neighbors = [i for i in range(off - 16, off + 20) if i != off and i in changed_set]
        if not neighbors:
            isolated.append((off, va, vb))
    for off, va, vb in isolated[:50]:
        hint = ""
        delta = vb - va
        if 1 <= delta <= 500: hint = f"(카운터 +{delta})"
        elif vb == 0: hint = "(소거)"
        elif va == 0: hint = "(신규)"
        else: hint = "(교체)"
        lines.append(f"  0x{off:05X}: 0x{va:08X} → 0x{vb:08X}  {hint}")
    lines.append(f"  총 {len(isolated)}개\n")

    # ─── 5) 군집 크기별 분류 ──────────────────────────
    small_groups = [(g, min(g), max(g)) for g in groups if (max(g)-min(g)) <= 12]
    med_groups   = [(g, min(g), max(g)) for g in groups if 12 < (max(g)-min(g)) <= 64]
    large_groups = [(g, min(g), max(g)) for g in groups if (max(g)-min(g)) > 64]

    lines.append(f"## [C] 군집 크기 분포")
    lines.append(f"  소형(≤12B): {len(small_groups)}개 | 중형(13~64B): {len(med_groups)}개 | 대형(>64B): {len(large_groups)}개\n")

    lines.append("## [D] 소형 군집 상세 (플래그/카운터 후보)")
    for g, gs, ge in small_groups[:80]:
        start4 = gs & ~3
        end4   = (ge | 3) + 1
        row = []
        for off in range(start4, end4, 4):
            va = u32(a, off)
            vb = u32(b, off)
            if va != vb:
                delta = vb - va
                hint = f"+{delta}" if 1 <= delta <= 500 else ("↑" if vb > va else "↓")
                row.append(f"0x{off:05X}:[{va:08X}→{vb:08X}({hint})]")
        lines.append(f"  [{gs:05X}~{ge:05X}] " + " | ".join(row))
    lines.append("")

    # ─── 6) 처음 0x200 바이트 헤더 영역 변화 ──────────
    lines.append("## [E] 헤더 영역 (0x000~0x200) 변화")
    for off in range(0, min(0x200, sz)):
        if a[off] != b[off]:
            lines.append(f"  0x{off:03X}: 0x{a[off]:02X} → 0x{b[off]:02X}")
    lines.append("")

    # ─── 7) 중형 군집 헥스 덤프 (StreetPass 방 데이터 후보) ──
    lines.append("## [F] 중형 군집 헥스 덤프 (StreetPass 방 데이터 후보)")
    for g, gs, ge in med_groups[:20]:
        start4 = gs & ~3
        end4   = (ge | 3) + 1
        lines.append(f"\n  [군집 0x{gs:05X}~0x{ge:05X}, {ge-gs+1}B]")
        for off in range(start4, end4, 4):
            va = u32(a, off)
            vb = u32(b, off)
            mark = " ←" if va != vb else ""
            lines.append(f"    0x{off:05X}: {va:08X} → {vb:08X}{mark}")
    lines.append("")

    # ─── 8) 특정 값 검색 ──────────────────────────────
    lines.append("## [G] 특정 해시 검색 (after에서만 나타나는 값)")
    KNOWN = {
        0x713FE778: "VIP EncountConfigHash",
        0xDB1CC069: "Pandanoko ParamHash",
        0xEDFE7305: "table[0] ConfigHash",
    }
    for val, label in KNOWN.items():
        needle = struct.pack("<I", val)
        pos_a = a.find(needle); pos_b = b.find(needle)
        lines.append(f"  {label} (0x{val:08X}):")
        lines.append(f"    before: {hex(pos_a) if pos_a>=0 else 'not found'}")
        lines.append(f"    after:  {hex(pos_b) if pos_b>=0 else 'not found'}")
    lines.append("")

    # ─── 9) 4바이트 정렬 완전 diff (first 40 groups) ──
    lines.append("## [H] 4바이트 정렬 군집 앞 40개 완전 덤프")
    for g, gs, ge in [(g, min(g), max(g)) for g in groups[:40]]:
        start4 = gs & ~3
        end4   = (ge | 3) + 1
        span = ge - gs + 1
        lines.append(f"\n  [0x{gs:05X}~0x{ge:05X}, span={span}B]")
        for off in range(start4, end4, 4):
            va = u32(a, off)
            vb = u32(b, off)
            mark = " ←" if va != vb else ""
            lines.append(f"    0x{off:05X}: {va:08X} → {vb:08X}{mark}")

    out = out_dir / "yw2_save_diff_smart.txt"
    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"[OK] {out}")
    # 콘솔에도 핵심 요약 출력
    print("\n=== 핵심 요약 ===")
    print(f"소폭 카운터 변화: {len(counter_hits)}개")
    print(f"독립 단일 블록: {len(isolated)}개")
    print(f"헤더(0x000~0x200) 변화 오프셋:")
    for off in range(0, min(0x200, sz)):
        if a[off] != b[off]:
            print(f"  0x{off:03X}: 0x{a[off]:02X}→0x{b[off]:02X}")


if __name__ == "__main__":
    main()
