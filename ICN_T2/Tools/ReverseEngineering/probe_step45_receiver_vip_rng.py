#!/usr/bin/env python3
"""
Step 45: 수신 측 (Receiver) VIP 확률 롤(RNG) 추적
- 수신된 패킷에서 VIP bool 값을 읽어 확률(1%)을 굴리는 지점 탐색
- 0x329F84 (확률 함수) 주변 문맥 정밀 분석
- VIP 플래그가 1일 때만 1/100 (또는 특정 확률)을 전달하는 로직 탐색
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
    if (instr & 0x0FF00000) == 0x03000000: # MOVW
        imm16 = ((instr >> 4) & 0xF000) | (instr & 0xFFF)
        return f"MOVW r{(instr>>12)&0xF},#0x{imm16:X}"
    if (instr & 0x0FE00000) == 0x02800000:
        return f"ADD r{(instr>>12)&0xF},r{(instr>>16)&0xF},#0x{arm_imm(instr&0xFFF):X}"
    if (instr & 0x0FF0F000) == 0x03500000:
        return f"CMP r{(instr>>16)&0xF},#0x{arm_imm(instr&0xFFF):X}"
    if (instr & 0x0C000000) == 0x04000000:
        L = (instr >> 20) & 1; B = (instr >> 22) & 1
        Rn = (instr >> 16) & 0xF; Rd = (instr >> 12) & 0xF
        imm = instr & 0xFFF; sign = "+" if (instr >> 23) & 1 else "-"
        op = ("LDR" if L else "STR") + ("B" if B else "")
        if Rn == 15:
            pa = (addr + 8 + imm) if (instr >> 23) & 1 else (addr + 8 - imm)
            return f"{op} r{Rd},[PC,#{sign}0x{imm:X}] pool@{pa:08X}={u32(pa):08X}"
        return f"{op} r{Rd},[r{Rn},{sign}#{imm:X}]"
    return f"[{instr:08X}]"

out = []
out.append("=" * 60)
out.append("Step 45: 수신 측 VIP 확률 롤(0x329F84) 정밀 추적")
out.append("=" * 60)

# 1. 0x329F84 (RNG) 함수 호출자 재탐색 (전체)
out.append("\n[1] 0x329F84 (확률 판정 함수) 호출자 분석")
callers = []
for off in range(0, len(code)-4, 4):
    instr = u32(off)
    if (instr & 0xFF000000) == 0xEB000000:
        imm24 = instr & 0xFFFFFF
        if imm24 & 0x800000: imm24 |= 0xFF000000
        tgt = (off + 8 + (imm24 << 2)) & 0xFFFFFFFF
        if tgt == 0x329F84:
            callers.append(off)

out.append(f"  발견된 호출자: {len(callers)}개")

# 2. 각 호출자의 인자(r1: 분모, 확률 설정) 확인
out.append("\n[2] 호출자 컨텍스트에서 확률(인자) 추적")
for c in callers:
    out.append(f"\n  --- 호출자 0x{c:08X} 주변 ---")
    for i in range(-12, 4):
        addr = c + i * 4
        if addr < 0 or addr >= len(code): continue
        instr = u32(addr)
        dec = decode_simple(instr, addr)
        mark = " <<<" if addr == c else ""
        out.append(f"    0x{addr:08X}: {instr:08X}  {dec}{mark}")
        
# 3. 100(0x64)을 세팅하는 코드 스캔 (1% 확률 롤)
out.append("\n[3] 'MOV rX, #0x64' 주변에서 0x329F84 호출 찾기 (1% 판정 후보)")
candidates_1percent = []
for c in callers:
    found_100 = False
    for i in range(-20, 0):
        addr = c + i * 4
        if addr < 0: continue
        instr = u32(addr)
        # MOV r*, #0x64 (또는 MOVW)
        if (instr & 0x0FEF0FFF) == 0x03A00064:
            found_100 = True
        if (instr & 0x0FF00FFF) == 0x03000064:
            found_100 = True
    if found_100:
        candidates_1percent.append(c)

out.append(f"  1% 확률(0x64) 관련 0x329F84 호출: {len(candidates_1percent)}개")
for c in candidates_1percent:
    out.append(f"    0x{c:08X}")

# 4. 수신 패킷 파싱 루틴 (0x3533D0 부근)과 확률 함수의 연결점 탐색
out.append("\n[4] 수신 패킷 루프 (0x3533D0~0x353464) 분석")
# 수신 루프에서 VIP 플래그(패킷 +0x26)를 어떻게 읽고 처리하는지 재확인
for off in range(0x3533D0, 0x353470, 4):
    instr = u32(off)
    out.append(f"  0x{off:08X}: {instr:08X}  {decode_simple(instr, off)}")

result = "\n".join(out)
Path("md/04_Tech_Task/reports/step45_raw.txt").write_text(result, encoding="utf-8")
print("Done. Saved to step45_raw.txt")
