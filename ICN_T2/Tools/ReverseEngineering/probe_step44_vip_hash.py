#!/usr/bin/env python3
"""
Step 44: 해시 테이블 (singleton+0x397F8) 삽입 코드 및 0xBDD5CB7D 추적
"""
import struct
from pathlib import Path

CODE = Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset\00040000001B2A00.code.bin")
code = CODE.read_bytes()
def u32(off):
    if off + 4 > len(code): return 0
    return struct.unpack_from("<I", code, off)[0]

def arm_imm(imm12):
    rot = ((imm12 >> 8) & 0xF) * 2
    imm8 = imm12 & 0xFF
    if rot == 0: return imm8
    return ((imm8 >> rot) | (imm8 << (32 - rot))) & 0xFFFFFFFF

def decode_simple(instr, addr):
    if (instr & 0x0E000000) == 0x0A000000:
        imm24 = instr & 0xFFFFFF
        if imm24 & 0x800000: imm24 |= 0xFF000000
        t = (addr + 8 + (imm24 << 2)) & 0xFFFFFFFF
        lnk = "L" if (instr >> 24) & 1 else ""
        return f"B{lnk} 0x{t:08X}"
    if (instr & 0xFFFF0000) == 0xE92D0000:
        regs = ",".join(f"r{i}" for i in range(16) if (instr >> i) & 1)
        return f"PUSH {{{regs}}}"
    if (instr & 0xFFFF0000) == 0xE8BD0000:
        regs = ",".join(f"r{i}" for i in range(16) if (instr >> i) & 1)
        return f"POP {{{regs}}}"
    if (instr & 0x0FEF0000) == 0x03A00000:
        return f"MOV r{(instr>>12)&0xF},#0x{arm_imm(instr&0xFFF):X}"
    if (instr & 0x0FE00000) == 0x02800000:
        return f"ADD r{(instr>>12)&0xF},r{(instr>>16)&0xF},#0x{arm_imm(instr&0xFFF):X}"
    if (instr & 0x0C000000) == 0x04000000:
        L = (instr >> 20) & 1; B = (instr >> 22) & 1
        Rn = (instr >> 16) & 0xF; Rd = (instr >> 12) & 0xF
        imm = instr & 0xFFF; sign = "+" if (instr >> 23) & 1 else "-"
        op = ("LDR" if L else "STR") + ("B" if B else "")
        Rn_name = "PC" if Rn == 15 else f"r{Rn}"
        if Rn == 15:
            pa = (addr + 8 + imm) if (instr >> 23) & 1 else (addr + 8 - imm)
            return f"{op} r{Rd},[{Rn_name},#{sign}0x{imm:X}] pool@{pa:08X}={u32(pa):08X}"
        return f"{op} r{Rd},[{Rn_name},{sign}#0x{imm:X}]"
    return f"[{instr:08X}]"

out = []
out.append("=" * 60)
out.append("Step 44: singleton+0x397F8 해시 테이블 삽입 및 0xBDD5CB7D 탐색")
out.append("=" * 60)

# 1. 0xBDD5CB7D가 코드/상수풀 어디에 있는지 탐색
out.append("\n[1] 전체 코드에서 0xBDD5CB7D (Pandanoko VIP hash) 탐색")
hits_bdd = []
for off in range(0, len(code), 4):
    val = u32(off)
    if val == 0xBDD5CB7D:
        hits_bdd.append(off)

out.append(f"  발견: {len(hits_bdd)}개")
for h in hits_bdd:
    # 상수 풀인 경우, 이 주소를 LDR rX, [PC, #...]로 로드하는 명령어를 찾는다
    out.append(f"\n  >> 상수 풀 위치: 0x{h:08X}")
    loaders = []
    for off in range(max(0, h - 0x1000), min(len(code), h + 0x1000), 4):
        instr = u32(off)
        if (instr & 0x0E5F0000) == 0x041F0000: # LDR rX, [PC, #...]
            imm = instr & 0xFFF
            sign = 1 if (instr >> 23) & 1 else -1
            target = off + 8 + (imm * sign)
            if target == h:
                loaders.append(off)
    out.append(f"     해당 상수 로더: {len(loaders)}개")
    for l in loaders:
        out.append(f"       0x{l:08X}: {u32(l):08X}  {decode_simple(u32(l), l)}")

# 2. 0x4C6F38 (Hash Lookup) 쌍둥이 함수 (Hash Insert/Add) 탐색
# 0x4C6F38처럼 0x397F8 (또는 0x39400 + 0x3F8)을 넘기는 함수를 찾는다.
out.append("\n[2] Hash Add로 추정되는 함수 탐색 (singleton+0x397F8 패턴)")
# 이미 Step 43에서 0x39400을 더하는 패턴을 찾았으나, 이번엔 BL 0x4C6F38 이외의 BL 대상을 찾는다
add_39400_hits = []
for off in range(0, len(code)-4, 4):
    instr = u32(off)
    if (instr & 0x0FE0F000) == 0x02800000: # ADD rX, rY, imm
        val = arm_imm(instr & 0xFFF)
        if val == 0x39400:
            add_39400_hits.append(off)

hash_insert_candidates = {}
for off in add_39400_hits:
    # 뒤쪽으로 20 인스트럭션 내에서 BL 찾기
    for i in range(1, 21):
        target_off = off + i * 4
        if target_off >= len(code): break
        n_instr = u32(target_off)
        if (n_instr & 0xFF000000) == 0xEB000000: # BL
            imm24 = n_instr & 0xFFFFFF
            if imm24 & 0x800000: imm24 |= 0xFF000000
            tgt = (target_off + 8 + (imm24 << 2)) & 0xFFFFFFFF
            if tgt != 0x4C6F38: # Lookup이 아닌 다른 함수
                if tgt not in hash_insert_candidates:
                    hash_insert_candidates[tgt] = []
                hash_insert_candidates[tgt].append(target_off)

out.append(f"  ADD r*, #0x39400 주변 BL 호출 (0x4C6F38 제외): {len(hash_insert_candidates)}개 함수")
for tgt, callers in sorted(hash_insert_candidates.items(), key=lambda x: len(x[1]), reverse=True):
    out.append(f"  - 함수 0x{tgt:08X} (호출 {len(callers)}회):")
    for c in callers[:3]:
        out.append(f"      호출 위치: 0x{c:08X}")
    if len(callers) > 3: out.append("      ...")

# Hash Table Insert 일 가능성이 높은 함수 덤프 (0x4C70E4 등)
# 0x4C6F38 근처에 있는 함수 덤프
out.append("\n[3] 0x4C6F38 주변 함수 (Hash 관련 패키지) 분석")
out.append("  0x4C6E5C ~ 0x4C71F0 덤프")
for off in range(0x4C6E5C, 0x4C71F0, 4):
    instr = u32(off)
    out.append(f"  {off:08X}: {instr:08X}  {decode_simple(instr, off)}")

result = "\n".join(out)
Path("md/04_Tech_Task/reports/step44_raw.txt").write_text(result, encoding="utf-8")
print("Done. Saved to step44_raw.txt")
