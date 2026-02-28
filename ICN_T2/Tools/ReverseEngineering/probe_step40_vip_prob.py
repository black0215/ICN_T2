#!/usr/bin/env python3
"""
Step 40: VIP 확률 분석 - StreetPass 직렬화 전(前) 단계에서
EncountConfigHash가 어떻게 결정되는지 추적.

1. 0x238390(직렬화) 호출자 → 그 앞에서 EncountConfigHash 세팅 추적
2. 0x4C8500 (테이블 선택자) 내부 분석
3. VIP 조건을 결정하는 함수/분기 탐색
4. 0x351074 VIP 체커의 obj+0x226, +0x1F0, +0x224 필드 추적
"""
import struct
from pathlib import Path

CODE = Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset\00040000001B2A00.code.bin")
code = CODE.read_bytes()
BASE = 0

def u32(off): return struct.unpack_from("<I", code, off)[0]
def s32(off): return struct.unpack_from("<i", code, off)[0]

def disasm_range(start_off, n=40, label=""):
    if label: print(f"\n[{label}]")
    for i in range(n):
        off = start_off + i*4
        if off + 4 > len(code): break
        instr = u32(off)
        addr = BASE + off
        desc = decode_instr(instr, addr)
        print(f"  {addr:08X}: {instr:08X}  {desc}")

def decode_instr(instr, addr):
    cond = (instr >> 28) & 0xF
    cond_str = ["EQ","NE","CS","CC","MI","PL","VS","VC","HI","LS","GE","LT","GT","LE","","NV"][cond]
    # BL/B
    if (instr & 0x0E000000) == 0x0A000000:
        imm24 = instr & 0xFFFFFF
        if imm24 & 0x800000: imm24 |= 0xFF000000
        offset = (imm24 << 2)
        target = (addr + 8 + offset) & 0xFFFFFFFF
        link = "L" if (instr >> 24) & 1 else ""
        return f"B{link}{cond_str} 0x{target:08X}"
    # STRB/LDRB/STR/LDR - 단순 표시
    if (instr & 0x0C000000) == 0x04000000:
        L = (instr >> 20) & 1
        B = (instr >> 22) & 1
        Rn = (instr >> 16) & 0xF
        Rd = (instr >> 12) & 0xF
        imm = instr & 0xFFF
        op = ("LDR" if L else "STR") + ("B" if B else "")
        U = (instr >> 23) & 1
        sign = "+" if U else "-"
        return f"{op} r{Rd},[r{Rn},#{sign}0x{imm:X}]"
    # PUSH/POP (STMFD/LDMFD sp)
    if (instr & 0xFFFF0000) == 0xE92D0000:
        regs = [f"r{i}" for i in range(16) if (instr >> i) & 1]
        return f"PUSH {{{','.join(regs)}}}"
    if (instr & 0xFFFF0000) == 0xE8BD0000:
        regs = [f"r{i}" for i in range(16) if (instr >> i) & 1]
        return f"POP {{{','.join(regs)}}}"
    # MOV/MVN
    if (instr & 0x0FEF0000) == 0x03A00000:
        Rd = (instr >> 12) & 0xF
        imm8 = instr & 0xFF
        rot = ((instr >> 8) & 0xF) * 2
        val = (imm8 >> rot) | (imm8 << (32-rot)) if rot else imm8
        val &= 0xFFFFFFFF
        return f"MOV r{Rd},#0x{val:X}"
    # CMP
    if (instr & 0x0FF0F000) == 0x03500000:
        Rn = (instr >> 16) & 0xF
        imm8 = instr & 0xFF
        rot = ((instr >> 8) & 0xF) * 2
        val = (imm8 >> rot) | (imm8 << (32-rot)) if rot else imm8
        val &= 0xFFFFFFFF
        return f"CMP r{Rn},#0x{val:X}"
    # ADD/SUB
    if (instr & 0x0FE00000) == 0x02800000:
        Rd = (instr >> 12) & 0xF
        Rn = (instr >> 16) & 0xF
        imm = instr & 0xFFF
        return f"ADD r{Rd},r{Rn},#0x{imm:X}"
    if (instr & 0x0FE00000) == 0x02400000:
        Rd = (instr >> 12) & 0xF
        Rn = (instr >> 16) & 0xF
        imm = instr & 0xFFF
        return f"SUB r{Rd},r{Rn},#0x{imm:X}"
    return f"[raw:{instr:08X}]"

def find_bl_callers(target_addr, label=""):
    """target_addr을 BL로 호출하는 모든 위치 탐색"""
    callers = []
    for off in range(0, len(code)-4, 4):
        instr = u32(off)
        if (instr & 0xFF000000) != 0xEB000000: continue
        imm24 = instr & 0xFFFFFF
        if imm24 & 0x800000: imm24 |= 0xFF000000
        target = (BASE + off + 8 + (imm24 << 2)) & 0xFFFFFFFF
        if target == target_addr:
            callers.append(BASE + off)
    if label:
        print(f"\n[BL 호출자: {label} @ 0x{target_addr:08X}]  총 {len(callers)}개")
        for c in callers[:20]:
            print(f"  호출 위치: 0x{c:08X}")
    return callers

def find_str_imm(target_off_hex, ldr=False):
    """STR Rd,[Rn,#imm] 또는 LDR Rd,[Rn,#imm] 중 imm == target_off_hex 인 것 찾기"""
    results = []
    for off in range(0, len(code)-4, 4):
        instr = u32(off)
        if (instr & 0x0C000000) != 0x04000000: continue
        L = (instr >> 20) & 1
        if ldr and not L: continue
        if not ldr and L: continue
        imm = instr & 0xFFF
        if imm == target_off_hex:
            results.append(BASE + off)
    return results

# ─────────────────────────────────────────────────────────────────
print("="*70)
print("Step 40: VIP 확률 정적 분석")
print("="*70)

# ─── 1. 0x238390 직렬화 호출자 분석 ──────────────────────────────
print("\n" + "="*60)
print("1. 0x238390 (StreetPass 직렬화) 호출자 → 전후 컨텍스트")
print("="*60)

SERIALIZER = 0x238390
callers_ser = find_bl_callers(SERIALIZER, "StreetPass Serializer 0x238390")

# 각 호출자 주변 30인스트럭션 덤프 (직렬화 직전에 EncountConfigHash가 설정됨)
for caller_addr in callers_ser[:5]:
    off = caller_addr - BASE
    start = max(0, off - 20*4)
    disasm_range(start, 50, f"Caller @ 0x{caller_addr:08X} 전후")

# ─── 2. 0x4C8500 테이블 선택자 내부 분석 ─────────────────────────
print("\n" + "="*60)
print("2. 0x4C8500 (테이블 선택자) 내부 분석")
print("="*60)
TABLE_SEL = 0x4C8500
off_4c8500 = TABLE_SEL - BASE
# 함수 진입점부터 80인스트럭션
disasm_range(off_4c8500, 80, "0x4C8500 full")
# 0x4C8500 호출자
find_bl_callers(TABLE_SEL, "TableSelector 0x4C8500")

# ─── 3. 0x351074 VIP 체커: obj+0x226, +0x1F0, +0x224 관련 ────────
print("\n" + "="*60)
print("3. VIP 체커 0x351074 재분석 (obj+0x226, +0x1F0, +0x224 추적)")
print("="*60)
VIP_CHECKER = 0x351074
disasm_range(VIP_CHECKER - BASE, 60, "VIP Checker 0x351074")

# ─── 4. obj+0x226, +0x1F0, +0x224에 쓰는 STRB/STR 탐색 ──────────
print("\n" + "="*60)
print("4. STRB/STR [Rn,#0x226], [Rn,#0x1F0], [Rn,#0x224] 탐색")
print("="*60)

for target_imm, label in [(0x226, "+0x226"), (0x1F0, "+0x1F0"), (0x224, "+0x224")]:
    hits = find_str_imm(target_imm, ldr=False)
    lhits = find_str_imm(target_imm, ldr=True)
    print(f"\n  STR*[Rn,#0x{target_imm:X}] ({label}) - 쓰기: {len(hits)}개")
    for h in hits[:10]:
        instr = u32(h - BASE)
        Rd = (instr >> 12) & 0xF
        Rn = (instr >> 16) & 0xF
        B = (instr >> 22) & 1
        op = "STRB" if B else "STR"
        print(f"    0x{h:08X}: {op} r{Rd},[r{Rn},#0x{target_imm:X}]")
        # 직전 10개 인스트럭션 일부 보기
        disasm_range((h - BASE) - 8*4, 12)
    print(f"  LDR*[Rn,#0x{target_imm:X}] ({label}) - 읽기: {len(lhits)}개")
    for h in lhits[:5]:
        instr = u32(h - BASE)
        Rd = (instr >> 12) & 0xF
        Rn = (instr >> 16) & 0xF
        B = (instr >> 22) & 1
        op = "LDRB" if B else "LDR"
        print(f"    0x{h:08X}: {op} r{Rd},[r{Rn},#0x{target_imm:X}]")

# ─── 5. 0x329EC4 (확률 함수 진입점) 재분석 - VIP 분기 이전 코드 ─────
print("\n" + "="*60)
print("5. 0x329EC4 (확률 함수 진입점) → VIP 판정 경로 전체")
print("="*60)
PROB_ENTRY = 0x329EC4
disasm_range(PROB_ENTRY - BASE, 100, "ProbFunc 0x329EC4")

# ─── 6. VIP bool(+0x5C) 쓰기 탐색 ───────────────────────────────
print("\n" + "="*60)
print("6. STR [Rn,#0x5C] 쓰기 (VIP bool 세팅 후보) 탐색")
print("="*60)
vip_stores = find_str_imm(0x5C, ldr=False)
print(f"  총 {len(vip_stores)}개 (앞 10개만 표시)")
for h in vip_stores[:10]:
    instr = u32(h - BASE)
    Rd = (instr >> 12) & 0xF
    Rn = (instr >> 16) & 0xF
    B = (instr >> 22) & 1
    op = "STRB" if B else "STR"
    print(f"  0x{h:08X}: {op} r{Rd},[r{Rn},#0x5C]")

# ─── 7. EncountConfigHash(+0x34) 쓰기 탐색 ─────────────────────
print("\n" + "="*60)
print("7. STR [Rn,#0x34] 쓰기 (EncountConfigHash 세팅 후보)")
print("="*60)
enc_stores = find_str_imm(0x34, ldr=False)
print(f"  총 {len(enc_stores)}개 (앞 20개만 표시)")
for h in enc_stores[:20]:
    instr = u32(h - BASE)
    Rd = (instr >> 12) & 0xF
    Rn = (instr >> 16) & 0xF
    B = (instr >> 22) & 1
    op = "STRB" if B else "STR"
    print(f"  0x{h:08X}: {op} r{Rd},[r{Rn},#0x34]")

print("\n[완료]")
