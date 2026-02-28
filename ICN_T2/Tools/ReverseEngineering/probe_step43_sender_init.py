#!/usr/bin/env python3
"""
Step 43: 발신자 VIP 결정 원점 추적 (Pandanoko 슬롯 초기화 체인)

목표:
  # 1  BANK_5C (0x5384E0) 덤프 -> singleton offset 및 VIP bool 반환 경로 확정
  # 2  0x17B768 full body (0x17B768~0x17B900) -> MOV r6,#3 / VIP STRB / 상수 풀 hash
  # 3  0x17B768 호출자 전체 탐색 -> 호출 컨텍스트 8인스트럭션
  # 4  0x4C6F38 (hash lookup) 호출자 분석 -> r1=singleton+0x397F8 패턴 분리
  # 5  0x21BDA4 영역 덤프 (0x21BDA4~0x21BE20) -> VIP bypass 경로 확인
  # 6  StreetPass 준비 루프 (0x2C5ECC~0x2C6500) -> #3 즉치 / BL 0x17B768 스캔
"""
import struct
from pathlib import Path

CODE = Path(r"C:\Users\home\Desktop\ICN_T2\Yw2Asset\00040000001B2A00.code.bin")
code = CODE.read_bytes()
BASE = 0

def u32(off):
    if off + 4 > len(code):
        return 0
    return struct.unpack_from("<I", code, off)[0]

def arm_imm(imm12):
    """ARM immediate (rotate + 8-bit value)"""
    rot = ((imm12 >> 8) & 0xF) * 2
    imm8 = imm12 & 0xFF
    if rot == 0:
        return imm8
    return ((imm8 >> rot) | (imm8 << (32 - rot))) & 0xFFFFFFFF

def decode_instr(instr, addr):
    cond = (instr >> 28) & 0xF
    cs = ["EQ","NE","CS","CC","MI","PL","VS","VC","HI","LS","GE","LT","GT","LE","","NV"][cond]

    # BL / B
    if (instr & 0x0E000000) == 0x0A000000:
        imm24 = instr & 0xFFFFFF
        if imm24 & 0x800000:
            imm24 |= 0xFF000000
        t = (addr + 8 + (imm24 << 2)) & 0xFFFFFFFF
        lnk = "L" if (instr >> 24) & 1 else ""
        return f"B{lnk}{cs} 0x{t:08X}"

    # LDR/STR/LDRB/STRB (immediate offset)
    if (instr & 0x0C000000) == 0x04000000:
        L = (instr >> 20) & 1
        B = (instr >> 22) & 1
        Rn = (instr >> 16) & 0xF
        Rd = (instr >> 12) & 0xF
        imm = instr & 0xFFF
        sign = "+" if (instr >> 23) & 1 else "-"
        op = ("LDR" if L else "STR") + ("B" if B else "")
        Rn_name = "r15(PC)" if Rn == 15 else f"r{Rn}"
        if Rn == 15:
            pool_addr = (addr + 8 + imm) if (instr >> 23) & 1 else (addr + 8 - imm)
            pool_val = u32(pool_addr)
            return f"{op} r{Rd},[{Rn_name},{sign}#0x{imm:X}]  ; pool@0x{pool_addr:08X}=0x{pool_val:08X}"
        return f"{op} r{Rd},[{Rn_name},{sign}#0x{imm:X}]"

    # PUSH
    if (instr & 0xFFFF0000) == 0xE92D0000:
        regs = ",".join(f"r{i}" for i in range(16) if (instr >> i) & 1)
        return f"PUSH {{{regs}}}"

    # POP
    if (instr & 0xFFFF0000) == 0xE8BD0000:
        regs = ",".join(f"r{i}" for i in range(16) if (instr >> i) & 1)
        return f"POP {{{regs}}}"

    # MOV (imm)
    if (instr & 0x0FEF0000) == 0x03A00000:
        Rd = (instr >> 12) & 0xF
        val = arm_imm(instr & 0xFFF)
        return f"MOV r{Rd},#0x{val:X}"

    # MOVT / MOVW (ARMv6T2+)
    if (instr & 0x0FF00000) == 0x03000000:
        Rd = (instr >> 12) & 0xF
        imm16 = ((instr >> 4) & 0xF000) | (instr & 0xFFF)
        return f"MOVW r{Rd},#0x{imm16:X}"
    if (instr & 0x0FF00000) == 0x03400000:
        Rd = (instr >> 12) & 0xF
        imm16 = ((instr >> 4) & 0xF000) | (instr & 0xFFF)
        return f"MOVT r{Rd},#0x{imm16:X}"

    # CMP (imm)
    if (instr & 0x0FF0F000) == 0x03500000:
        Rn = (instr >> 16) & 0xF
        val = arm_imm(instr & 0xFFF)
        return f"CMP r{Rn},#0x{val:X}"

    # ADD (imm)
    if (instr & 0x0FE00000) == 0x02800000:
        Rd = (instr >> 12) & 0xF
        Rn = (instr >> 16) & 0xF
        val = arm_imm(instr & 0xFFF)
        return f"ADD r{Rd},r{Rn},#0x{val:X}"

    # SUB (imm)
    if (instr & 0x0FE00000) == 0x02400000:
        Rd = (instr >> 12) & 0xF
        Rn = (instr >> 16) & 0xF
        val = arm_imm(instr & 0xFFF)
        return f"SUB r{Rd},r{Rn},#0x{val:X}"

    # AND (imm)
    if (instr & 0x0FE00000) == 0x02000000:
        Rd = (instr >> 12) & 0xF
        Rn = (instr >> 16) & 0xF
        val = arm_imm(instr & 0xFFF)
        return f"AND r{Rd},r{Rn},#0x{val:X}"

    # BIC (imm)
    if (instr & 0x0FE00000) == 0x03C00000:
        Rd = (instr >> 12) & 0xF
        Rn = (instr >> 16) & 0xF
        val = arm_imm(instr & 0xFFF)
        return f"BIC r{Rd},r{Rn},#0x{val:X}"

    # ORR (imm)
    if (instr & 0x0FE00000) == 0x03800000:
        Rd = (instr >> 12) & 0xF
        Rn = (instr >> 16) & 0xF
        val = arm_imm(instr & 0xFFF)
        return f"ORR r{Rd},r{Rn},#0x{val:X}"

    # TST (imm)
    if (instr & 0x0FF0F000) == 0x03100000:
        Rn = (instr >> 16) & 0xF
        val = arm_imm(instr & 0xFFF)
        return f"TST r{Rn},#0x{val:X}"

    # MOV (reg)
    if (instr & 0x0FEF0FF0) == 0x01A00000:
        Rd = (instr >> 12) & 0xF
        Rm = instr & 0xF
        return f"MOV r{Rd},r{Rm}"

    # BX
    if (instr & 0x0FFFFFFF) == 0x012FFF1E:
        return "BX r14 (ret)"
    if (instr & 0x0FFFFFF0) == 0x012FFF10:
        Rm = instr & 0xF
        return f"BX r{Rm}"

    return f"[{instr:08X}]"

def dump(start_addr, n=48, label=""):
    lines = []
    if label:
        lines.append(f"\n[{label}]")
    for i in range(n):
        off = start_addr + i * 4
        if off + 4 > len(code):
            break
        instr = u32(off)
        lines.append(f"  {off:08X}: {instr:08X}  {decode_instr(instr, off)}")
    return "\n".join(lines)

def find_bl(target_addr):
    """전체 바이너리에서 BL target_addr 패턴 탐색"""
    results = []
    for off in range(0, len(code) - 4, 4):
        instr = u32(off)
        if (instr & 0xFF000000) != 0xEB000000:
            continue
        imm24 = instr & 0xFFFFFF
        if imm24 & 0x800000:
            imm24 |= 0xFF000000
        tgt = (off + 8 + (imm24 << 2)) & 0xFFFFFFFF
        if tgt == target_addr:
            results.append(off)
    return results

def find_b(target_addr):
    """전체 바이너리에서 B (branch, not link) target_addr 탐색 (조건부 포함)"""
    results = []
    for off in range(0, len(code) - 4, 4):
        instr = u32(off)
        if (instr & 0x0E000000) != 0x0A000000:
            continue
        if (instr >> 24) & 1:
            continue  # BL 제외
        imm24 = instr & 0xFFFFFF
        if imm24 & 0x800000:
            imm24 |= 0xFF000000
        tgt = (off + 8 + (imm24 << 2)) & 0xFFFFFFFF
        if tgt == target_addr:
            results.append(off)
    return results

out = []
out.append("=" * 70)
out.append("Step 43: 발신자 VIP 결정 원점 추적 -- Pandanoko 슬롯 초기화 체인")
out.append("=" * 70)
out.append(f"  code.bin size: {len(code):,} bytes  (BASE=0x{BASE:08X})")

# ============================================================
# # 1  BANK_5C (0x5384E0) 덤프
# ============================================================
out.append("\n" + "=" * 60)
out.append("S1  BANK_5C (0x5384E0) - singleton offset / VIP bool 반환 경로")
out.append("=" * 60)
out.append(dump(0x5384E0, 80, "0x5384E0 BANK_5C"))

callers_bank5c = find_bl(0x5384E0)
out.append(f"\n[BANK_5C 호출자: {len(callers_bank5c)}개]")
for c in callers_bank5c[:15]:
    out.append(f"  0x{c:08X}  {decode_instr(u32(c), c)}")

# ============================================================
# # 2  0x17B768 full body (0x17B768 ~ 0x17B940)
#     : MOV r6,#3 / hash lookup / STRB r5,[r4,+#3] VIP set / 상수 풀
# ============================================================
out.append("\n" + "=" * 60)
out.append("# 2  0x17B768 full body (Pandanoko 슬롯 초기화)")
out.append("    -- MOV r6,#3 / BL 0x4C6F38 / CMP hash / STRB VIP=1 / 상수 풀")
out.append("=" * 60)

# 전체 덤프: 0x17B768 ~ 0x17B940 (0xD8 bytes = 54 instr, 여유있게 80개)
out.append(dump(0x17B768, 100, "0x17B768 (full body)"))

# 상수 풀 위치 직접 읽기
# 0x17B830: E59F20FC  LDR r2,[r15(PC),+#0xFC] -> pool@0x17B830+8+0xFC=0x17B934
pool_addr = 0x17B934
pool_val  = u32(pool_addr)
out.append(f"\n  >> 상수 풀 @ 0x{pool_addr:08X} = 0x{pool_val:08X}")
if pool_val == 0x713FE778:
    out.append("  >> [*] Pandanoko hash (0x713FE778) 일치 확인!")
else:
    out.append(f"  >> [!] 예상 Pandanoko hash 0x713FE778 와 불일치 -> 실제값 0x{pool_val:08X}")

# 추가로 0x17B938 ~ 0x17B950 (상수 풀 영역)
out.append("\n  [상수 풀 영역 0x17B930~0x17B960]")
for a in range(0x17B930, 0x17B960, 4):
    v = u32(a)
    out.append(f"    {a:08X}: {v:08X}")

# ============================================================
# # 3  0x17B768 호출자 전체 탐색 + 컨텍스트 8인스트럭션
# ============================================================
out.append("\n" + "=" * 60)
out.append("# 3  BL 0x17B768 호출자 탐색 (전체 바이너리 스캔)")
out.append("=" * 60)
callers_17b768 = find_bl(0x17B768)
out.append(f"  총 {len(callers_17b768)}개 호출자 발견")
for c in callers_17b768:
    out.append(f"\n  -- 호출 @ 0x{c:08X} --")
    start = max(0, c - 8 * 4)
    end_n = 18  # 8 before + call + 9 after
    for i in range(end_n):
        off = start + i * 4
        if off + 4 > len(code):
            break
        instr = u32(off)
        mark = " <<<" if off == c else ""
        out.append(f"    {off:08X}: {instr:08X}  {decode_instr(instr, off)}{mark}")

# ============================================================
# # 4  0x4C6F38 (hash lookup) 호출자 분석
#     -> r1 = singleton+0x397F8 패턴 분리
#     -> singleton+0x397F8 에 쓰는 STR 탐색
# ============================================================
out.append("\n" + "=" * 60)
out.append("# 4  BL 0x4C6F38 (hash lookup) 호출자 분석")
out.append("    -- r1=singleton+0x397F8 패턴 탐색")
out.append("=" * 60)
callers_4c6f38 = find_bl(0x4C6F38)
out.append(f"  0x4C6F38 총 호출자: {len(callers_4c6f38)}개")

# 각 호출자 앞에서 ADD r1,r*,#0x39400 / ADD r1,r1,#0x3F8 패턴 탐색
# -> singleton + 0x39400 + 0x3F8 = singleton + 0x397F8
pattern_hits = []
for c in callers_4c6f38:
    # 앞 20인스트럭션 검사
    context = []
    has_39400 = False
    has_3f8   = False
    for j in range(1, 21):
        off = c - j * 4
        if off < 0:
            break
        instr = u32(off)
        context.insert(0, (off, instr))
        # ADD r1,r*,#0x39400 -> arm_imm 계산으로 확인
        if (instr & 0x0FE0F000) == 0x02801000:  # ADD r1,r*,imm
            val = arm_imm(instr & 0xFFF)
            if val == 0x39400:
                has_39400 = True
        if (instr & 0x0FE0F000) == 0x02801000:
            val = arm_imm(instr & 0xFFF)
            if val == 0x3F8:
                has_3f8 = True
    if has_39400 or has_3f8:
        pattern_hits.append((c, context, has_39400, has_3f8))

out.append(f"  singleton+0x397F8 패턴 일치 호출자: {len(pattern_hits)}개")
for c, ctx, h1, h2 in pattern_hits:
    out.append(f"\n  -- 0x4C6F38 호출 @ 0x{c:08X}  [+0x39400:{h1} / +0x3F8:{h2}]")
    for (off, instr) in ctx:
        mark = " <<<BL" if off == c else ""
        out.append(f"    {off:08X}: {instr:08X}  {decode_instr(instr, off)}{mark}")

# 0x17B768 내부의 BL 0x4C6F38 (이미 알려진 것, 확인용)
out.append(f"\n  [참고] 0x17B81C 인근 BL 0x4C6F38 직접 덤프:")
out.append(dump(0x17B810, 12, "0x17B810 (BL 0x4C6F38 근방)"))

# singleton+0x397F8 에 STR하는 코드 탐색
# ADD r1,r0,#0x39400 후 ADD r1,r1,#0x3F8 -> STR
# 패턴: STR r*,[r1,+*] 형태로 r1이 singleton+0x397F8 포인터
# 대신 BL 0x4C6F38 전후에서 SET (write) 경로 추적
out.append("\n  [singleton+0x397F8 WRITE 탐색]")
out.append("  (singleton+0x397F8 해시 테이블에 엔트리를 넣는 STR 코드)")
# 0x4C6F38 함수 내부 덤프로 어떤 역할인지 확인
out.append(dump(0x4C6F38, 60, "0x4C6F38 (hash lookup) 함수 body"))

# ============================================================
# # 5  0x21BDA4 영역 덤프 (VIP 확정 bypass 경로)
# ============================================================
out.append("\n" + "=" * 60)
out.append("# 5  0x21BDA4 영역 (0x21BDA4~0x21BE20) -- VIP bypass 경로")
out.append("    -- 0x329F84 확률 함수의 bypass인지 확인")
out.append("=" * 60)
out.append(dump(0x21BDA4, 60, "0x21BDA4 (VIP bypass?)"))

callers_21bda4 = find_bl(0x21BDA4)
out.append(f"\n  0x21BDA4 BL 호출자: {len(callers_21bda4)}개")
for c in callers_21bda4[:10]:
    out.append(f"  0x{c:08X}  {decode_instr(u32(c), c)}")

# 0x21BDA4 로 B (branch) 하는 코드도 탐색
b_21bda4 = find_b(0x21BDA4)
out.append(f"\n  0x21BDA4 B (branch) 점프: {len(b_21bda4)}개")
for c in b_21bda4[:10]:
    out.append(f"  0x{c:08X}  {decode_instr(u32(c), c)}")

# 0x329F84 (확률 함수) 덤프 -- bypass와 비교
out.append(dump(0x329F84, 30, "0x329F84 (확률 함수) 앞부분"))

# ============================================================
# # 6  StreetPass 준비 루프 (0x2C5ECC ~ 0x2C6500) 스캔
#     -> #3 즉치 / BL 0x17B768 / encounter table 선택 분기
# ============================================================
out.append("\n" + "=" * 60)
out.append("# 6  StreetPass 준비 루프 (0x2C5ECC ~ 0x2C6500) 분석")
out.append("    -- #0x3 즉치 / BL 0x17B768 / CMP r*,#3 분기 탐색")
out.append("=" * 60)

SCAN_START = 0x2C5ECC
SCAN_END   = 0x2C6500

# 전체 구간 덤프
n_instr = (SCAN_END - SCAN_START) // 4
out.append(dump(SCAN_START, n_instr, f"0x{SCAN_START:08X}~0x{SCAN_END:08X} 전체"))

# BL 0x17B768 존재 여부
bl_hits_in_loop = [c for c in callers_17b768 if SCAN_START <= c < SCAN_END]
out.append(f"\n  구간 내 BL 0x17B768: {len(bl_hits_in_loop)}개")
for c in bl_hits_in_loop:
    out.append(f"  0x{c:08X}")

# #0x3 즉치 (MOV r*,#3 또는 CMP r*,#3)
hits_imm3 = []
for off in range(SCAN_START, SCAN_END, 4):
    instr = u32(off)
    # MOV r*,#3
    if (instr & 0x0FEF0FFF) == 0x03A00003:
        hits_imm3.append((off, "MOV", instr))
    # CMP r*,#3
    elif (instr & 0x0FF0FFFF) == 0x03500003:
        hits_imm3.append((off, "CMP", instr))
out.append(f"\n  구간 내 #0x3 즉치 (MOV/CMP): {len(hits_imm3)}개")
for off, typ, instr in hits_imm3:
    out.append(f"  0x{off:08X}: {instr:08X}  {decode_instr(instr, off)}")

# 0x2C5ECC 직전 구간도 확인 (StreetPass 준비 진입점)
out.append("\n  [StreetPass 준비 진입점 0x2C5E80~0x2C5ECC]")
out.append(dump(0x2C5E80, 20, "0x2C5E80 (직전 구간)"))

# 0x2C6430 ~ 0x2C6500 (2번째 진입점 근처)
out.append(dump(0x2C6430, 30, "0x2C6430 (2번째 진입점 근처)"))

# ============================================================
# # 부록  결과 해석 테이블
# ============================================================
out.append("\n" + "=" * 60)
out.append("# 결과 해석")
out.append("=" * 60)

pool_val2 = u32(0x17B934)
out.append(f"\n  상수 풀 0x17B934 = 0x{pool_val2:08X}")
if pool_val2 == 0x713FE778:
    out.append("  -> [*] Pandanoko hash 일치 -> VIP=1 조건 = Pandanoko 테이블 로드 여부")
else:
    out.append(f"  -> 다른 hash 값 (0x{pool_val2:08X}), Pandanoko hash 재확인 필요")

n_callers = len(callers_17b768)
out.append(f"\n  0x17B768 호출자 수: {n_callers}개")
if n_callers == 1:
    c0 = callers_17b768[0]
    out.append(f"  -> 단일 진입점 @ 0x{c0:08X}: Pandanoko 선택 = 해당 경로 진입 조건이 확률 결정")
elif n_callers == 0:
    out.append("  -> 직접 BL 없음 (테이블 포인터 또는 vtable 경유 가능성)")
else:
    out.append(f"  -> 복수 호출 ({n_callers}개): 여러 맵/존 분기에서 호출 가능")

out.append("\n[완료]")

result = "\n".join(out)
out_path = Path("md/04_Tech_Task/reports/step43_raw.txt")
out_path.write_text(result, encoding="utf-8")
print(result)
print(f"\n>> 저장: {out_path}")
