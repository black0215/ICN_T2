#!/usr/bin/env python3
"""
Step 41: VIP bool 결정 원점 추적
- 0x10502C (RoomEntry 초기화) 전체 분석 → STRB r4,[r3,#0x2D9] 직전 r4 값 추적
- 0x329FC8의 gate byte [ctx+0x1A5D4] 세팅 코드 탐색
- VIP 조건이 진짜 무작위 확률인지, 아니면 상태 기반인지 확인
"""
import struct
from pathlib import Path

CODE = Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset\00040000001B2A00.code.bin")
code = CODE.read_bytes()
BASE = 0

def u32(off): return struct.unpack_from("<I", code, off)[0]

# ARM 즉값 디코더 (회전 포함)
def arm_imm(imm12):
    rot = ((imm12 >> 8) & 0xF) * 2
    imm8 = imm12 & 0xFF
    if rot == 0:
        return imm8
    return ((imm8 >> rot) | (imm8 << (32 - rot))) & 0xFFFFFFFF

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
    # STRB/LDRB
    if (instr & 0x0C000000) == 0x04000000:
        L = (instr >> 20) & 1
        B = (instr >> 22) & 1
        Rn = (instr >> 16) & 0xF
        Rd = (instr >> 12) & 0xF
        imm = instr & 0xFFF
        U = (instr >> 23) & 1
        sign = "+" if U else "-"
        op = ("LDR" if L else "STR") + ("B" if B else "")
        return f"{op} r{Rd},[r{Rn},{sign}#0x{imm:X}]"
    # PUSH/POP
    if (instr & 0xFFFF0000) == 0xE92D0000:
        regs = [f"r{i}" for i in range(16) if (instr >> i) & 1]
        return f"PUSH {{{','.join(regs)}}}"
    if (instr & 0xFFFF0000) == 0xE8BD0000:
        regs = [f"r{i}" for i in range(16) if (instr >> i) & 1]
        return f"POP {{{','.join(regs)}}}"
    # MOV
    if (instr & 0x0FEF0000) == 0x03A00000:
        Rd = (instr >> 12) & 0xF
        imm12 = instr & 0xFFF
        val = arm_imm(imm12)
        return f"MOV r{Rd},#0x{val:X}"
    # CMP (imm)
    if (instr & 0x0FF0F000) == 0x03500000:
        Rn = (instr >> 16) & 0xF
        imm12 = instr & 0xFFF
        val = arm_imm(imm12)
        return f"CMP r{Rn},#0x{val:X}"
    # ADD (imm)
    if (instr & 0x0FE00000) == 0x02800000:
        Rd = (instr >> 12) & 0xF
        Rn = (instr >> 16) & 0xF
        imm12 = instr & 0xFFF
        val = arm_imm(imm12)
        return f"ADD r{Rd},r{Rn},#0x{val:X}  [=0x{val:08X}]"
    # SUB (imm)
    if (instr & 0x0FE00000) == 0x02400000:
        Rd = (instr >> 12) & 0xF
        Rn = (instr >> 16) & 0xF
        imm12 = instr & 0xFFF
        val = arm_imm(imm12)
        return f"SUB r{Rd},r{Rn},#0x{val:X}  [=0x{val:08X}]"
    return f"[{instr:08X}]"

def disasm_range(start_off, n=40, label=""):
    lines = []
    if label: lines.append(f"\n[{label}]")
    for i in range(n):
        off = start_off + i*4
        if off + 4 > len(code): break
        instr = u32(off)
        addr = BASE + off
        desc = decode_instr(instr, addr)
        lines.append(f"  {addr:08X}: {instr:08X}  {desc}")
    return "\n".join(lines)

def find_bl_callers(target_addr):
    callers = []
    for off in range(0, len(code)-4, 4):
        instr = u32(off)
        if (instr & 0xFF000000) != 0xEB000000: continue
        imm24 = instr & 0xFFFFFF
        if imm24 & 0x800000: imm24 |= 0xFF000000
        target = (BASE + off + 8 + (imm24 << 2)) & 0xFFFFFFFF
        if target == target_addr:
            callers.append(BASE + off)
    return callers

out = []
out.append("="*70)
out.append("Step 41: VIP bool 결정 원점 추적")
out.append("="*70)

# ─── 1. 0x10502C 함수 전체 (VIP 플래그 초기화 함수) ─────────────────
out.append("\n" + "="*60)
out.append("1. 0x10502C 전체 (STRB r4,[r3,#0x2D9] 포함)")
out.append("="*60)
out.append(disasm_range(0x10502C, 120, "0x10502C full"))

# 0x10502C 칼러 목록
callers_105 = find_bl_callers(0x10502C)
out.append(f"\n[0x10502C 호출자: {len(callers_105)}개]")
for c in callers_105[:15]:
    out.append(f"  0x{c:08X}")

# ─── 2. 0x329FC0 gate byte 계산 (ARM 즉값 올바르게 해석) ──────────────
out.append("\n" + "="*60)
out.append("2. 0x329FC0 gate byte 계산 (ARM imm 올바른 해석)")
out.append("="*60)
# E2848801: ADD r8,r4, imm12=0x801 → rotate=16, imm8=1 → 0x10000
imm_a = arm_imm(0x801)
imm_b = arm_imm(0xCA5)
gate_off = imm_a + imm_b + 0xD4
out.append(f"  ADD r8,r4,#0x801  → 실제값 0x{imm_a:08X}")
out.append(f"  ADD r8,r8,#0xCA5  → 실제값 0x{imm_b:08X}")
out.append(f"  LDRB [r8,+0xD4]   → ctx offset = 0x{gate_off:X}")
out.append(f"  ADD r6,r4,#0xB69  → 실제값 0x{arm_imm(0xB69):08X}  (ctx+0x{arm_imm(0xB69):X})")

# ─── 3. gate byte [ctx+0x157A] 쓰기 탐색 ─────────────────────────────
# 위에서 계산한 gate_off 오프셋을 쓰는 코드를 찾아야 하지만
# 컴파일러가 복잡한 방식으로 접근할 수 있으므로 
# 대신 0x329F84 함수의 callers와 더 넓은 문맥 분석
out.append("\n" + "="*60)
out.append(f"3. 0x329F84 (확률함수) 호출자 분석")
out.append("="*60)
callers_329 = find_bl_callers(0x329F84)
out.append(f"[0x329F84 호출자: {len(callers_329)}개]")
for c in callers_329[:10]:
    out.append(f"\n  호출위치: 0x{c:08X}")
    # 각 호출자 앞뒤 20인스트럭션
    start = max(0, c - BASE - 16*4)
    for j in range(40):
        off = start + j*4
        if off + 4 > len(code): break
        instr = u32(off)
        addr = BASE + off
        mark = " <<<" if addr == c else ""
        out.append(f"    {addr:08X}: {instr:08X}  {decode_instr(instr, addr)}{mark}")

# ─── 4. 0x32A4F8 (VIP path target) 컨텍스트 ─────────────────────────
out.append("\n" + "="*60)
out.append("4. 0x32A4F8 (VIP 확정 경로) 분석")
out.append("="*60)
out.append(disasm_range(0x32A4F8, 60, "0x32A4F8 VIP path"))

# ─── 5. 0x00353428~0x00353460 루프 (StreetPass 수신 처리) ─────────────
out.append("\n" + "="*60)
out.append("5. StreetPass 수신 루프 [0x003533D0~0x00353464] 분석")
out.append("   (VIP bool → +0x226 플래그 세팅 루프)")
out.append("="*60)
out.append(disasm_range(0x003533D0, 40, "RecvLoop@0x3533D0"))
out.append(disasm_range(0x00353410, 40, "RecvLoop@0x353410"))

# ─── 6. 0x0017B9xx (common_enc 로딩 구역) VIP 관련 ─────────────────────
out.append("\n" + "="*60)
out.append("6. 0x0017B9xx 구역 (0x226 세팅) 분석 → 테이블 로딩과의 관계")
out.append("="*60)
# 0x0017B924: STRB r10,[r5,+0x226] 기준으로 주변 80개
out.append(disasm_range(0x0017B880, 80, "0x0017B880 (0x226 세팅 포함)"))

# ─── 7. StreetPass 패킷 역직렬화 - +0x26 (VIP bool) 읽기 ──────────────
out.append("\n" + "="*60)
out.append("7. 수신 패킷 +0x26 (VIP bool) 읽기 코드 탐색")
out.append("   (LDRB r*,[r*,+0x26] 형태)")
out.append("="*60)
hits_026 = []
for off in range(0, len(code)-4, 4):
    instr = u32(off)
    if (instr & 0x0FE00FFF) == 0x05D00026:  # LDRB r*,[r*,+0x26]
        Rn = (instr >> 16) & 0xF
        Rd = (instr >> 12) & 0xF
        hits_026.append(BASE + off)
out.append(f"  LDRB [Rn,+0x26] 총 {len(hits_026)}개")
for h in hits_026[:20]:
    instr = u32(h - BASE)
    Rn = (instr >> 16) & 0xF
    Rd = (instr >> 12) & 0xF
    out.append(f"  0x{h:08X}: LDRB r{Rd},[r{Rn},+0x26]")

# 0x353440 전후 (이미 알고 있는 VIP bool 읽기 위치) 재확인
out.append(disasm_range(0x353410, 30, "0x353410 VIP bool 읽기 문맥"))

out.append("\n[완료]")

result = "\n".join(out)
Path("md/04_Tech_Task/reports/step41_vip_origin_raw.txt").write_text(result, encoding="utf-8")
print(result)
