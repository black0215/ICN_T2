#!/usr/bin/env python3
"""
Step 42: 발신 측 EncountConfigHash / VIP bool 결정 로직 추적.
- 0x17B708 (EncountSlot 초기화 함수) 분석
- 직렬화 전 EncountConfigHash 세팅 경로
- 테이블 선택 → VIP bool 연계 규명
"""
import struct
from pathlib import Path

CODE = Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset\00040000001B2A00.code.bin")
code = CODE.read_bytes()
BASE = 0

def u32(off): return struct.unpack_from("<I", code, off)[0]

def arm_imm(imm12):
    rot = ((imm12 >> 8) & 0xF) * 2
    imm8 = imm12 & 0xFF
    if rot == 0: return imm8
    return ((imm8 >> rot) | (imm8 << (32 - rot))) & 0xFFFFFFFF

def decode_instr(instr, addr):
    cond = (instr >> 28) & 0xF
    cs = ["EQ","NE","CS","CC","MI","PL","VS","VC","HI","LS","GE","LT","GT","LE","","NV"][cond]
    if (instr & 0x0E000000) == 0x0A000000:
        imm24 = instr & 0xFFFFFF
        if imm24 & 0x800000: imm24 |= 0xFF000000
        t = (addr + 8 + (imm24 << 2)) & 0xFFFFFFFF
        return f"B{'L' if (instr>>24)&1 else ''}{cs} 0x{t:08X}"
    if (instr & 0x0C000000) == 0x04000000:
        L=(instr>>20)&1; B=(instr>>22)&1
        Rn=(instr>>16)&0xF; Rd=(instr>>12)&0xF
        imm=instr&0xFFF; sign="+" if (instr>>23)&1 else "-"
        return f"{'LDR' if L else 'STR'}{'B' if B else ''} r{Rd},[r{Rn},{sign}#0x{imm:X}]"
    if (instr&0xFFFF0000)==0xE92D0000:
        return f"PUSH {{{','.join([f'r{i}' for i in range(16) if (instr>>i)&1])}}}"
    if (instr&0xFFFF0000)==0xE8BD0000:
        return f"POP {{{','.join([f'r{i}' for i in range(16) if (instr>>i)&1])}}}"
    if (instr&0x0FEF0000)==0x03A00000:
        return f"MOV r{(instr>>12)&0xF},#0x{arm_imm(instr&0xFFF):X}"
    if (instr&0x0FF0F000)==0x03500000:
        return f"CMP r{(instr>>16)&0xF},#0x{arm_imm(instr&0xFFF):X}"
    if (instr&0x0FE00000)==0x02800000:
        v=arm_imm(instr&0xFFF)
        return f"ADD r{(instr>>12)&0xF},r{(instr>>16)&0xF},#0x{v:X}"
    if (instr&0x0FE00000)==0x02400000:
        v=arm_imm(instr&0xFFF)
        return f"SUB r{(instr>>12)&0xF},r{(instr>>16)&0xF},#0x{v:X}"
    return f"[{instr:08X}]"

def dump(start_addr, n=48, label=""):
    lines = []
    if label: lines.append(f"\n[{label}]")
    for i in range(n):
        off = start_addr + i*4
        if off + 4 > len(code): break
        instr = u32(off)
        lines.append(f"  {off:08X}: {instr:08X}  {decode_instr(instr, off)}")
    return "\n".join(lines)

def find_bl(target):
    res = []
    for off in range(0, len(code)-4, 4):
        instr = u32(off)
        if (instr&0xFF000000)!=0xEB000000: continue
        imm24 = instr&0xFFFFFF
        if imm24&0x800000: imm24|=0xFF000000
        if (off+8+(imm24<<2))&0xFFFFFFFF == target:
            res.append(off)
    return res

out = []
out.append("="*70)
out.append("Step 42: 발신 VIP/EncountConfigHash 결정 추적")
out.append("="*70)

# ─── 1. 0x17B708 (EncountSlot 초기화) 분석 ────────────────────────────
out.append("\n## 1. 0x17B708 (EncountSlot 초기화, 10502C에서 6회 호출)")
out.append(dump(0x17B708, 80, "0x17B708"))

callers_17b = find_bl(0x17B708)
out.append(f"\n[호출자: {len(callers_17b)}개 (앞 10개)]")
for c in callers_17b[:10]:
    out.append(f"  0x{c:08X}")

# ─── 2. 0x22C84C의 BL 0x2626BC → 직렬화 전 enc hash 세팅 확인 ────────
out.append("\n## 2. 0x0022C84C (BL 0x2626BC) - 직렬화 전 enc hash 세팅")
out.append(dump(0x2626BC, 60, "0x2626BC (직렬화 helper?)"))

# ─── 3. 0x2581BC 분석 (직렬화 직전 자주 호출) ──────────────────────────
out.append("\n## 3. 0x2581BC 분석 (직렬화 직전 호출)")
out.append(dump(0x2581BC, 60, "0x2581BC"))

# ─── 4. 0x22C874 → BL 0x538524 (BANK getter?) ────────────────────────
out.append("\n## 4. 0x538524 분석 (BANK 0x38 getter?)")
out.append(dump(0x538524, 40, "0x538524"))

# ─── 5. 0x2C1F80 (StreetPass 수신 처리) 전체 다시 분석 ──────────────────
out.append("\n## 5. 0x2C1F80 (StreetPass 수신) 150인스트럭션")
out.append(dump(0x2C1F80, 150, "0x2C1F80"))

# ─── 6. 수신 패킷에서 EncountConfigHash가 저장되는 위치 탐색 ─────────────
# 직렬화 시 r4+0x1C에 씀 (0x238390 내부에서)
# 수신 시 이것을 읽는 코드가 있을 것
# EncountConfigHash (0x34 offset) 로딩 후 비교
out.append("\n## 6. 수신 패킷에서 EncountConfigHash 로딩 - [r*,+0x1C] 읽기 탐색")
hits_1c = []
for off in range(0, len(code)-4, 4):
    instr = u32(off)
    if (instr&0x0FE00000)!=0x05900000: continue  # LDR r*,[r*,+*]
    if (instr&0xFFF)!=0x01C: continue
    L=(instr>>20)&1
    if not L: continue
    hits_1c.append(off)
out.append(f"  LDR [Rn,+0x1C] 총 {len(hits_1c)}개")
# 그 중 0x22Cxxx 범위 (직렬화 근처)
nearby = [h for h in hits_1c if 0x220000 <= h <= 0x240000]
out.append(f"  0x22xxxx 범위: {len(nearby)}개")
for h in nearby[:10]:
    instr = u32(h)
    Rn=(instr>>16)&0xF; Rd=(instr>>12)&0xF
    out.append(f"  0x{h:08X}: LDR r{Rd},[r{Rn},+0x1C]")
    out.append(dump(h-8*4, 20))

# ─── 7. 0x2C4E3C 재분석 (VIP CMP RNG 함수로 알려진 곳) ─────────────────
out.append("\n## 7. 0x2C4E3C (VIP/RNG 관련) 재분석")
out.append(dump(0x2C4D80, 100, "0x2C4D80~0x2C4EFC"))

# ─── 8. ctx+0x1A5D4 gate byte setter 탐색 ─────────────────────────────
# 0x1A5D4 = 0x1A500 + 0xD4 / ADD r8,r4,#0x10000; ADD r8,r8,#0xA500 pattern
# gate를 1로 세팅하는 STRB/STR 코드 탐색
# 직접 찾기 어려우므로 gate offset 0xD4를 addr+0x1A500 이후에서 탐색
# 단순히 STRB 후 0xD4 immediate 검색
out.append("\n## 8. STRB [Rn,+0xD4] 탐색 (gate byte setter)")
gate_setters = []
for off in range(0, len(code)-4, 4):
    instr = u32(off)
    if (instr&0x0FE00FFF)==0x05C000D4:  # STRB r*,[r*,+#0xD4]
        Rd=(instr>>12)&0xF; Rn=(instr>>16)&0xF
        L=(instr>>20)&1
        if not L:
            gate_setters.append(off)
out.append(f"  STRB [Rn,+0xD4] 총 {len(gate_setters)}개")
for h in gate_setters[:15]:
    instr = u32(h)
    Rd=(instr>>12)&0xF; Rn=(instr>>16)&0xF
    out.append(f"  0x{h:08X}: STRB r{Rd},[r{Rn},+0xD4]")
    out.append(dump(h-6*4, 16))

Path("md/04_Tech_Task/reports/step42_sender_vip_raw.txt").write_text("\n".join(out), encoding="utf-8")
print("\n".join(out))
