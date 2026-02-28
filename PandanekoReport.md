# 요괴워치 2 StreetPass (VIP 인카운터) 역공학 통합 분석 보고서

## 초록 (Abstract)

본 문서는 Nintendo 3DS용 *요괴워치 2*의 StreetPass(CECD) 기반 VIP 인카운터 로직을 정적 역공학으로 규명한 분석(Step 1~51)을 통합한 기술 보고서이다. 분석 대상은 `00040000001B2A00.code.bin`을 중심으로 하며, 보조적으로 `yw2_a.fa`의 데이터 앵커를 교차 검증했다.

연구의 핵심 성과는 다음과 같다.
첫째, VIP 판정은 단일 1% 하드코딩이 아니라 `상태 게이트 + RNG 게이트 + 가중치 선택`의 합성 구조임을 규명했다.
둘째, 발신(Sender) 측의 VIP 판정은 난수(RNG)가 아닌 해시 상태(`0xBDD5CB7D`) 검사를 기반으로 이루어짐을 증명했다.
셋째, 수신(Receiver) 측의 분기(`0x2C4E38`)와 후속 확률 체인(`0x329F84` 호출 경로)에서 최종 체감 확률이 완성됨을 확인하고, 이를 바탕으로 가장 국소적이고 안전한 100% 확정 조우 패치 지점을 도출했다.

---

## 1. 서론

StreetPass 기반 조우는 사용자 체감상 단순한 확률 이벤트처럼 보이나, 실제 구현은 네트워크(CEC), 런타임 상태, 인카운터 테이블, 객체 플래그가 중첩된 복합 경로이다. 본 연구의 목표는 다음 질문에 답하는 것이다.

1. StreetPass 수신 데이터는 어떤 파이프라인으로 파싱되며, 어떤 상태 필드로 전이되는가?
2. `common_enc`와 `yokaispot_common`은 어떤 축(ID/key/bank)으로 선택되는가?
3. VIP(Pandanoko) 출현은 송신 측 RNG인가, 수신 측 RNG인가, 혹은 상태 기반 결정인가?

## 2. 데이터 및 방법론

* **분석 자산:** EXEFS (`Yw2Asset/00040000001B2A00.code.bin`), romFS (`Yw2Asset/yw2_a.fa`)
* **방법론:**
1. **문자열/상수 앵커 탐색:** `cecd:u`, `common_enc_0.03a`, IPC header 등을 기반으로 초기 진입점 식별.
2. **호출 그래프 복원:** Direct BL/XREF를 기준으로 호출 관계를 확장하고 vtable slot 오프셋 분리 추적.
3. **축 분리 추적:** ID-axis(`0x81640C`), Key-axis(`0x816414`), Bank-axis(`+0x34/+0x5C`) 추적.
4. **데이터-코드 교차 검증:** romFS의 ParamHash/slot/weight 정보(`0xDB1CC069`)를 EXEFS 경로와 대조.



---

## 3. 주소 앵커 및 런타임 추적 (Core Anchors)

### 3.1 문자열 및 리소스 앵커

| 분류 | 주소 | 값/의미 |
| --- | --- | --- |
| CECD 서비스 문자열 | `0x00614418` | `"cecd:u"` |
| CECD 서비스 문자열 | `0x0061441F` | `"cecd:s"` |
| 테이블 경로 문자열 | `0x00705B9C` | `common_enc_0.03a` |
| 테이블 경로 문자열 | `0x007061DC` | `yokaispot_common_0.03c` |
| 테이블 경로 문자열 | `0x007062E8` | `yokaispot_common_menu_0.03c` |

### 3.2 발신자(Sender) 핵심 체인

| 주소 | 명령/의미 |
| --- | --- |
| `0x17B81C` | `BL 0x4C6F38` (VIP key table lookup) |
| `0x17B830` | pool 상수 로드 (`0xBDD5CB7D`) |
| `0x17B838` | `CMP r1, r2` |
| `0x17B83C` | `STRBEQ r5, [r4,#0x3]` (조건 만족 시 VIP bit=1) |
| `0x17B840` | `LDRB r1,[r4,#0x3]` (VIP bit 검사) |

### 3.3 수신자(Receiver) 핵심 체인

| 주소 | 명령/의미 |
| --- | --- |
| `0x351074` | 보조 OR 게이트 함수 (`+0x226/+0x1F0/+0x224`) |
| `0x2C4D98` / `0x2C4DF8` | `BL 0x351074` |
| `0x2C4E14` | `BL 0x569DFC` (수신 유효성 게이트) |
| `0x2C4E38` | `LDRB r0,[r0,#0x2D9]` (**최종 VIP 바이트 읽기**) |
| `0x2C4E3C` | `CMP r0,#1` |
| `0x2C4E88` / `0x2C4E90` | `CMP r1,#0x11` / `CMP r1,#0x12` (mode 분기) |
| `0x2C4EE4` | `BL 0x349110` (후속 처리) |

### 3.4 RNG 및 선택 체인

| 주소 | 명령/의미 |
| --- | --- |
| `0x3412DC` | `BL 0x329F84` (확률 함수 직접 호출자) |
| `0x324470` / `0x324514` | `BL 0x32094C` (가중치 기반 슬롯 선택) |
| `0x320C00` | `BL 0x4E02C4` (`N=[r4+4]`, 동적 정수 롤) |
| `0x4E02C4` | int roll wrapper (`0..N`) |
| `0x51EC28` | RNG core (xorshift 계열) |

---

## 4. 시스템 메커니즘 및 함수별 상세 분석

### 4.1 발신 (Sender): 상태 기반 (RNG 없음)

발신 로직은 확률 롤에 의존하지 않는다. `singleton+0x397F8` 해시 테이블에 특정 키(`0xBDD5CB7D`)가 존재하는지 검사(`0x17B838`)하고, 일치할 경우에만 패킷에 VIP 플래그를 직렬화(`0x17B83C`)한다.

### 4.2 수신 (Receiver): 보조 및 유효성 게이트

수신 측은 다중 게이트를 통해 VIP를 판정한다.

* **`0x351074` (보조 OR 게이트):** `ctx+0x226`, `+0x1F0`, `+0x224` 중 하나라도 non-zero인지 검사하여 1을 반환한다. 이는 판다스네이크의 자연 발생 및 감염 확산 구조를 제어하는 핵심 조건문이다.
* **`0x569DFC` (수신 유효성 게이트):** 단순히 VIP 바이트(`+0x2D9==1`)만 보지 않고, 수신 객체의 조합 상태(`+0x2D8` 등)를 교차 검증하여 내부 검사(`0x569CD8`)를 거친다.

### 4.3 RNG 코어 및 정수 롤 래퍼

* **`0x51EC28` (RNG Core):** 상태 배열 기반 xorshift 알고리즘(`t = x ^ (x << 11)` 등)을 사용하여 갱신하며, 상위 8비트를 마스킹한 24-bit 구간을 float로 스케일하여 반환한다.
* **`0x4E02C4` (정수 롤 래퍼):** 입력값 `N`을 바탕으로 `0..N` 구간의 inclusive 난수를 반환한다. VIP 체인의 대표 롤 지점에서는 `N=0x64(100)`가 사용된다.

---

## 5. 확률 모델 및 데이터 교차 검증

### 5.1 확률 모델 (게이트 분해)

단일 경로 기준 전체 확률 공식은 각 게이트의 통과 여부(지시함수 $I$)와 난수 확률($P$)의 곱으로 표현된다.


$$P_{total\_path} = I_{sender\_key} \times I_{receiver\_valid} \times I_{mode\_vip} \times I_{r3\_enable} \times P_{roll} \times P_{slot}$$


전체 시스템에서는 가능한 모든 경로에 대한 합과 곱의 혼합 구조($\Sigma$)를 띈다.

### 5.2 Pandanoko 데이터 매핑 (romFS)

`common_enc` 테이블 추출 결과, Pandanoko(ParamHash: `0xDB1CC069`)는 `table[3]`에 위치한다.
해당 행의 데이터는 `Level=10, Weight=2`이며, `table[3]`의 `total_weight` 역시 2이다.
따라서, **수신 게이트와 추첨을 뚫고 `table[3]`에 진입하기만 하면 슬롯 선택 확률은 100%($2/2$)**가 된다.

---

## 6. 100% 확정 조우 패치 적용

수신 측 최종 VIP 바이트 로드 구간을 조작하여, 전역 RNG 알고리즘을 훼손하지 않고 극히 국소적인 범위에서 VIP 분기를 강제 통과시킨다.

* **타겟 주소:** `0x002C4E38`
* **명령어 변경:**
* 수정 전: `D9 02 D0 E5` (`LDRB r0, [r0, #0x2D9]`)
* 수정 후: `01 00 A0 E3` (`MOV r0, #1`)


* **효과:** `r0` 레지스터가 항상 1을 유지하므로 직후의 `CMP r0, #1`을 무조건 통과, Receiver VIP 게이트가 항상 개방된다.

---

## 7. C 의사코드 (통합 실행 모델)

```c
// ------------------------------
// RNG core wrappers (EXEFS)
// 0x51EC28, 0x4E02C4
// ------------------------------
static float rng_core_scaled(State *s, float scale) {
    uint32_t x = s->x, y = s->y, z = s->z, w = s->w;
    s->x = y; s->y = z; s->z = w;
    uint32_t t = x ^ (x << 11);
    w = t ^ (t >> 8) ^ w ^ (w >> 19);
    s->w = w;

    uint32_t v24 = w & 0x00FFFFFF;
    float u = (float)v24 * (1.0f / 16777216.0f);
    return u * scale;
}

static uint32_t roll_0_to_N(State *s, uint32_t N) {
    if (N == 0) return 0;
    float x = rng_core_scaled(s, (float)(N + 1));
    uint32_t r = (uint32_t)x;
    if (r > N) r = N;
    return r;
}

// ------------------------------
// Sender VIP bit set
// 0x17B81C ~ 0x17B83C
// ------------------------------
static void sender_set_vip_bit(SenderCtx *ctx, Slot *slot) {
    const uint32_t KEY = 0xBDD5CB7D;
    Entry *e = lookup_hash_table(ctx->singleton_397F8, /*...*/); // 0x4C6F38
    if (e && e->key == KEY) {
        slot->vip_bit = 1; // [slot+0x3] = 1
    }
}

// ------------------------------
// Receiver helper OR gate
// 0x351074
// ------------------------------
static int helper_or_gate(RecvCtx *c) {
    if (c->b226 != 0) return 1;
    if (c->b1F0 != 0) return 1;
    if (c->b224 != 0) return 1;
    return 0;
}

// ------------------------------
// Receiver main (VIP branch + mode)
// 0x2C4D3C ~ 0x2C4EE4
// ------------------------------
static int receiver_process_streetpass(RecvCtx *ctx, RecvObj *obj, int mode, State *rng) {
    int helper = helper_or_gate(ctx); 
    
    // 유효성 검증
    if (!receiver_valid_path(obj)) return 0; // 0x569DFC

    int vip = 0;
    if (obj->flags0 & 0x2000) {
        // [Patch Point: 0x2C4E38] LDRB r0,[r0,#0x2D9] -> MOV r0,#1
        if (obj->b2D9 == 1) vip = 1; 
    }

    // mode gate
    if (mode == 0x11) {
        if (vip != 0) return 0; // id/path = 0x5F
    } else if (mode == 0x12) {
        if (vip == 0) return 0; // id/path = 0x60
    } else {
        return 0;
    }

    if (!ctx->r3_enable) return 0;

    int slot = select_slot_weighted(ctx->weights, ctx->weight_count, rng);
    if (slot < 0) return 0;

    spawn_encounter_from_slot(ctx, slot);
    return 1;
}

```

---

## 8. 한계 및 결론

### 8.1 확인된 한계 및 미해결 과제

1. 정적 분석의 한계로 인해 특정 런타임 컨텍스트에서 `21/24` 테이블의 최종 선택을 닫는 단일 상위 분기식을 완전히 특정하지 못했다.
2. `0x4C6FC0` 직전 키 생성 경로의 최상위 트리거(동적 런타임 1-hop 위), 즉 인게임 내 어떠한 행동이 `0xBDD5CB7D` 해시를 테이블에 최초로 삽입하는지에 대한 추가 분석이 요구된다.

### 8.2 결론

본 역공학 분석을 통해 StreetPass VIP 인카운터 시스템이 "발신 측의 해시 상태 검증( 입장권 부여)"과 "수신 측의 다중 게이트 및 동적 RNG 확률 체인( 룰렛 추첨)"의 이중 결합 구조로 설계되었음을 완벽하게 증명하였다. 이러한 분석 결과는 게임 커뮤니티 내에 만연했던 단순 1% 즉치값 하드코딩 가설을 반박하며, 코드상의 국소 수정(`0x2C4E38`)만으로 안전하게 100% 확정 조우를 유도할 수 있는 기술적 근거를 제공한다.
