#!/usr/bin/env python3
"""
YW2 Step39B: 세이브 파일 diff 분석
  - 엇갈림 통신 전/후 .yw 파일 비교
  - 변경된 오프셋, 값 분류 (카운터, 해시, 플래그)
  - StreetPass 처리 흔적 탐색

사용법:
  python probe_step39b_save_diff.py <before.yw> <after.yw>
"""
from __future__ import annotations

import sys, struct
from pathlib import Path
from typing import List, Tuple

def u32(data: bytes, off: int) -> int:
    return struct.unpack_from("<I", data, off)[0] if off + 4 <= len(data) else 0

def u16(data: bytes, off: int) -> int:
    return struct.unpack_from("<H", data, off)[0] if off + 2 <= len(data) else 0

def i32(data: bytes, off: int) -> int:
    return struct.unpack_from("<i", data, off)[0] if off + 4 <= len(data) else 0

def diff_saves(a: bytes, b: bytes) -> List[Tuple[int, int, int]]:
    """바이트 단위 diff. (offset, val_a, val_b) 반환"""
    length = min(len(a), len(b))
    diffs = []
    i = 0
    while i < length:
        if a[i] != b[i]:
            diffs.append((i, a[i], b[i]))
        i += 1
    return diffs


def main():
    # 인자 처리
    if len(sys.argv) >= 3:
        path_a = Path(sys.argv[1])
        path_b = Path(sys.argv[2])
    else:
        # 자동으로 세이브 파일 찾기
        base = Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset")
        saves = list(base.rglob("*.yw"))
        if len(saves) < 2:
            print("세이브 파일 2개 이상 필요. 사용법: script.py <before.yw> <after.yw>")
            return
        print("발견된 세이브 파일:")
        for i, s in enumerate(saves):
            print(f"  [{i}] {s}")
        print("before/after 파일 경로를 인자로 지정하세요.")
        return

    if not path_a.exists() or not path_b.exists():
        print(f"파일 없음: {path_a} or {path_b}")
        return

    a = path_a.read_bytes()
    b = path_b.read_bytes()
    print(f"Before: {path_a.name} ({len(a)} bytes)")
    print(f"After:  {path_b.name} ({len(b)} bytes)")

    diffs = diff_saves(a, b)
    print(f"\n변경된 바이트: {len(diffs)}개")

    if not diffs:
        print("차이 없음")
        return

    # 오프셋 군집 분석
    offsets = [d[0] for d in diffs]
    groups: List[List[Tuple[int,int,int]]] = []
    cur: List[Tuple[int,int,int]] = [diffs[0]]
    for d in diffs[1:]:
        if d[0] - cur[-1][0] <= 8:
            cur.append(d)
        else:
            groups.append(cur)
            cur = [d]
    groups.append(cur)

    print(f"변경 군집: {len(groups)}개\n")
    for gi, g in enumerate(groups):
        start_off = g[0][0] & ~3
        end_off   = (g[-1][0] | 3) + 1
        print(f"[그룹 {gi}] 오프셋 0x{g[0][0]:X}~0x{g[-1][0]:X} ({len(g)} bytes)")

        # u32 단위로 표시
        for off in range(start_off, end_off, 4):
            va = u32(a, off)
            vb = u32(b, off)
            if va != vb:
                # 변화 분류
                delta = vb - va
                hint = ""
                if abs(delta) == 1: hint = "(카운터 +1)"
                elif abs(delta) < 100: hint = f"(작은 변화 Δ={delta:+d})"
                elif vb == 0: hint = "(값 소거)"
                elif va == 0: hint = "(새 값 설정)"
                else: hint = "(해시/플래그 변경)"
                print(f"  off=0x{off:05X}: 0x{va:08X} → 0x{vb:08X}  {hint}")

        # 의미 있는 해시 검색
        for off in range(start_off, end_off, 4):
            vb_hash = u32(b, off)
            if vb_hash in (0x713FE778, 0xDB1CC069, 0xEDFE7305):
                print(f"  *** ENCOUNTER HASH @ 0x{off:05X}: 0x{vb_hash:08X}")
        print()

    # StreetPass 관련 카운터 탐색 (888/8A8 오프셋)
    print("=== StreetPass 카운터 후보 (ctx+0x888 등) ===")
    for off in range(0, min(len(a), len(b))-4, 2):
        va = u32(a, off)
        vb = u32(b, off)
        if va != vb and 0 < vb - va <= 10:
            print(f"  off=0x{off:05X}: {va} → {vb} (카운터 +{vb-va})")


if __name__ == "__main__":
    main()
