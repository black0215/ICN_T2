# 요괴워치 2 StreetPass(VIP 인카운터) 역공학: Step 1-46 통합 분석 보고

## 초록
본 문서는 Nintendo 3DS용 *요괴워치 2*의 StreetPass(CECD) 기반 VIP 인카운터 로직을 정적 역공학으로 규명한 Step 1-46 결과를 통합한 대학 수준 기술 보고서이다. 분석 대상은 `00040000001B2A00.code.bin`을 중심으로 하며, 보조적으로 `yw2_a.fa`의 데이터 앵커를 교차 검증했다. 연구의 핵심 성과는 다음과 같다. 첫째, CECD 호출 경로(`cecd:u`, `cecd:s`)와 직렬화/수신 루프를 연결해 송수신 파이프라인을 복원했다. 둘째, path table 인덱스 기준으로 `21=common_enc`, `24=yokaispot_common`, `25=menu` 매핑을 고정했다. 셋째, VIP 판정은 발신 측에서 “확률 롤”이 아닌 해시 상태(`0xBDD5CB7D`) 기반으로 구성되며, 수신 측 분기(`0x2C4E38`)와 후속 확률 체인(`0x329F84` 호출 경로)에서 최종 체감 확률이 완성됨을 보였다. 본 결과는 커뮤니티의 “1% 즉치값 하드코딩” 가설보다, “송신 상태 + 수신 동적 파라미터/RNG”의 이중 구조가 더 타당함을 시사한다.

## 1. 서론
StreetPass 기반 조우는 사용자 체감상 확률 이벤트처럼 보이지만, 실제 구현은 네트워크(CEC), 런타임 상태, 인카운터 테이블, 그리고 객체 플래그가 중첩된 복합 경로일 가능성이 높다. 본 연구의 목표는 다음 3개 질문에 답하는 것이다.

1. StreetPass 수신 데이터는 어떤 함수군에서 파싱되고, 어떤 상태 필드로 전이되는가?
2. `common_enc`와 `yokaispot_common`은 어떤 축(ID/key/bank)으로 선택되는가?
3. VIP(Pandanoko) 출현은 발신 측 RNG인지, 수신 측 RNG인지, 혹은 상태 기반 결정인지?

분석 범위는 Step 1-46 산출물 전체이며, 정적 분석 우선 원칙을 유지했다.

## 2. 데이터 및 방법

### 2.1 분석 자산
- EXEFS: `Yw2Asset/00040000001B2A00.code.bin`
- romFS 보조 검증: `Yw2Asset/yw2_a.fa` (ARC0 재추적 포함)
- 단계 산출물: `md/04_Tech_Task/reports` 내 Step1-46 문서

### 2.2 방법론
1. 문자열/상수 앵커 기반 초기 진입점 탐색  
`cecd:u`, `cecd:s`, `common_enc_0.03a`, `yokaispot_common_0.03c`, IPC header, `SVC 0x32`.

2. 호출 그래프 복원  
Direct BL/XREF를 기준으로 호출자-피호출자 관계를 단계적으로 확장하고, vtable slot 오프셋을 분리 추적.

3. 축 분리 추적  
- ID-axis: `0x81640C` 계열  
- Key-axis: `0x816414` 계열  
- Bank-axis: `+0x34/+0x5C` sibling 접근

4. 데이터-코드 교차 검증  
romFS의 ParamHash/slot/weight 정보(`0xDB1CC069`, `common_enc`)를 EXEFS 경로와 대조.

## 3. 단계별 주요 결과

### 3.1 Step 1-12: CECD 체인과 브리지 골격
- CECD 문자열 앵커와 SVC wrapper 군이 식별되었다.
- StreetPass 루프와 manager 참조가 같은 모듈 레벨에서 공존함을 확인했다.
- 다만 이 구간에서는 direct call만으로 “최종 table 선택식”을 닫기 어려웠고, 간접(vtable/콜백) 경로가 핵심임이 드러났다.

핵심 앵커(재현값):
- `cecd:u @ 0x00614418`
- `cecd:s @ 0x0061441F`
- `common_enc path @ 0x00705B9C`
- `yokaispot_common path @ 0x007061DC`
- `yokaispot_common_menu path @ 0x007062E8`

### 3.2 Step 13-20: vtable/selector 분해 및 경로 동결
- vptr 대입과 slot 오프셋 호출 규칙이 복원되었다.
- A-axis(`0x2C4FDC`)는 직접 table ID selector라기보다 hash/consumer 성격이 강하다는 결론으로 수렴했다.
- B-axis에서 `ID 21 -> common_enc` 경로는 정적 증거가 강하게 확보되었고, `24`는 key 해석을 경유하는 미완 링크로 유지됐다.
- Step20에서 “정적 분석 기준 동결 의사코드”를 제시했다.

### 3.3 Step 21-27: key-axis와 bank 구조의 고정
- `0x31D5D4 -> 0x559044 -> 0x5480BC` 경로가 key-axis 핵심으로 정리되었다.
- global `+0x164` 주변 bank layout이 구조적으로 정리되었고, sibling bank(`+0x34`, `+0x5C`)의 사용 차이가 확인됐다.
- path table 매핑은 문서군 통합 기준으로 아래와 같이 고정되었다.
  - `21 = common_enc`
  - `24 = yokaispot_common`
  - `25 = menu`

단, “특정 runtime context에서 21/24 최종 분기식이 어떤 단일 조건으로 갈리는가”는 당시 미완으로 남았다.

### 3.4 Step 28-38: VIP/확률 가설의 재정의
- `common_enc`에서 Pandanoko row의 slot/weight 검증이 수행됐다(Weight=2 확인).
- `0x2C4E3C CMP #1`은 초기 가설과 달리 “RNG threshold”보다 “캐시된 VIP 플래그 검사”로 재해석되었다.
- `STRB [..,#0x2D9]` 계열과 VIP checker(`0x351074`) 분석으로, VIP 관련 상태 비트가 별도 경로에서 세팅됨이 강화됐다.
- `EncountConfigHash` 선택 로직과 BANK 접근이 정리되며 송수신 구조의 분해가 가능해졌다.

### 3.5 Step 39-46: 발신/수신 분리의 확정
- Step39-42(raw 중심)에서 확률 함수(`0x329F84`) 호출 컨텍스트와 직렬화/수신 루프 주변이 집중 추적됐다.
- Step43에서 발신 핵심 함수 `0x17B768`이 확정되었다.
  - `pool@0x17B934 = 0xBDD5CB7D`
  - 해시 비교 일치 시 `STRB ... [slot+0x3] = 1`
- Step44에서 `singleton+0x397F8` 해시 테이블 조회/삽입 체인이 정리되었다.
  - 조회: `0x4C6F38`
  - 삽입: `0x4C6FC0` 계열
- Step45에서 수신 루프가 패킷 `+0x26` VIP 필드를 로컬 컨텍스트(`+0x226`, `+0x2D9`)로 매핑하는 흐름이 정리되었다.
- Step46에서 국소 패치 포인트가 확정됐다.
  - `0x002C4E38: LDRB r0,[r0,#0x2D9] -> MOV r0,#1`

## 4. 통합 해석

### 4.1 구조적 결론
VIP 인카운터는 단일 확률식이 아니라 2단 구조다.

1. 발신(Sender): 상태 기반
- 해시 테이블에 특정 키(`0xBDD5CB7D`)가 존재하면 VIP 플래그가 패킷으로 직렬화된다.
- 이 경로 자체에서는 RNG 호출 근거가 약하다.

2. 수신(Receiver): 분기 + 확률 체인
- 수신 패킷의 VIP 필드를 내부 플래그로 전이한다.
- 이후 조우 분기에서 VIP 플래그를 판정하고, 후속 동적 파라미터/RNG 호출 체인에서 최종 체감 확률이 형성된다.

### 4.2 “common_enc vs yokaispot_common” 쟁점
- path table 존재/인덱스 매핑 자체는 강하게 확정되었다.
- 하지만 “Pandanoko가 항상 24에서만 나온다” 같은 단정은 현재 증거군에 비해 과도하다.
- 현 단계에서 타당한 기술은 아래와 같다.
  - `21/24`는 서로 다른 테이블 축을 대표한다.
  - VIP 생성은 sender state + receiver path에서 결정되며, 특정 축 단독으로 환원하기 어렵다.

### 4.3 기존 가설의 교정
- 교정 1: `0xDB1CC069`는 EXEFS 직접 앵커가 아니라 데이터 앵커(romFS)로 취급.
- 교정 2: `0x2C4E3C`를 즉시 RNG threshold로 단정하지 않음.
- 교정 3: VIP 발신은 “1% 주사위”보다 “해시 존재 상태” 가설이 정합성이 높음.

## 5. 재현 가능한 핵심 증거 세트

1. CECD 앵커 문자열:
- `cecd:u @ 0x00614418`
- `cecd:s @ 0x0061441F`

2. 테이블 경로 문자열:
- `common_enc_0.03a @ 0x00705B9C`
- `yokaispot_common_0.03c @ 0x007061DC`

3. VIP sender 핵심:
- `0x17B768`, `0x17B830`, `0x17B83C`
- `0xBDD5CB7D`
- `0x4C6F38` / `0x4C6FC0`

4. VIP receiver 핵심:
- 패킷 파싱 구간 `0x3533D0~`
- 분기 `0x2C4E38`
- 확률 함수 호출 맥락 `0x329F84`(caller chain 포함)

## 6. 한계와 위협 요인

1. Step40-42는 raw 중심이라, 일부 결론은 Step43-46의 후속 정제에 의존한다.
2. 정적 분석만으로는 특정 runtime branch condition의 완전 복원에 한계가 있다.
3. 일부 중간 문서의 인코딩/가독성 문제로, 해석은 후속 정리 문서를 우선 근거로 삼았다.
4. save-state 실험은 본 라운드에서 보류되어 상태 필드의 영속성 검증이 제한적이다.

## 7. 결론
Step 1-46 통합 결과, StreetPass VIP 인카운터는 “발신 측 상태 기반 플래그화 + 수신 측 분기/확률 체인”의 결합 구조로 설명하는 것이 가장 일관적이다. 특히 `0x17B768`(sender)과 `0x2C4E38`(receiver)을 축으로 보면 기존의 단순 RNG 하드코딩 가설보다 실제 구현에 더 가깝다. 실무 관점에서는 수신측 국소 패치(`0x2C4E38`)가 영향 범위 대비 효율이 높으며, 연구 관점에서는 `21/24` 최종 선택 조건식과 `0xBDD5CB7D` 유입 원천의 상위 caller 확정이 다음 핵심 과제다.

## 참고 문서 (Step 1-46)
- `md/04_Tech_Task/reports/yw2_exefs_step1_cec_chain.md`
- `md/04_Tech_Task/reports/yw2_exefs_step2_payload_flow.md`
- `md/04_Tech_Task/reports/yw2_exefs_step3_mode_table_link.md`
- `md/04_Tech_Task/reports/yw2_exefs_step4_id_bridge.md`
- `md/04_Tech_Task/reports/yw2_exefs_step5_id24_chain.md`
- `md/04_Tech_Task/reports/yw2_exefs_step6_arc0_retrace.md`
- `md/04_Tech_Task/reports/yw2_exefs_step7_bridge_table.md`
- `md/04_Tech_Task/reports/yw2_exefs_step8_id24_source_trace.md`
- `md/04_Tech_Task/reports/yw2_exefs_step9_cmp24_scan.md`
- `md/04_Tech_Task/reports/yw2_exefs_step10_mode_to_table_trace.md`
- `md/04_Tech_Task/reports/yw2_exefs_step11_bridge_status.md`
- `md/04_Tech_Task/reports/yw2_exefs_step12_kickoff.md`
- `md/04_Tech_Task/reports/yw2_exefs_step12_bridge_update.md`
- `md/04_Tech_Task/reports/yw2_exefs_step12_vtable_findings.md`
- `md/04_Tech_Task/reports/yw2_exefs_step13_vptr_dispatch_link.md`
- `md/04_Tech_Task/reports/yw2_exefs_step14_slot28_binding.md`
- `md/04_Tech_Task/reports/yw2_exefs_step15_corefunc_callers.md`
- `md/04_Tech_Task/reports/yw2_exefs_step16_direct_branch_audit.md`
- `md/04_Tech_Task/reports/yw2_exefs_step17_2c4fdc_flow.md`
- `md/04_Tech_Task/reports/yw2_exefs_step18_selector_pivot.md`
- `md/04_Tech_Task/reports/yw2_exefs_step19_baxis_stitch.md`
- `md/04_Tech_Task/reports/yw2_exefs_step20_final_freeze.md`
- `md/04_Tech_Task/reports/yw2_exefs_step21_keyaxis_field_map.md`
- `md/04_Tech_Task/reports/yw2_exefs_step22_key_source_cache_bridge.md`
- `md/04_Tech_Task/reports/yw2_exefs_step23_retuse_fieldf.md`
- `md/04_Tech_Task/reports/yw2_exefs_step24_callback_context_map.md`
- `md/04_Tech_Task/reports/yw2_exefs_step25_bank_layout_map.md`
- `md/04_Tech_Task/reports/yw2_exefs_step26_bank_usage_density.md`
- `md/04_Tech_Task/reports/yw2_exefs_step27_bank_index_mapping.md`
- `md/04_Tech_Task/reports/yw2_exefs_step28_vip_probability_density.md`
- `md/04_Tech_Task/reports/yw2_exefs_step29_cmp_rng_density.md`
- `md/04_Tech_Task/reports/yw2_exefs_step30_slot_weight_density.md`
- `md/04_Tech_Task/reports/yw2_exefs_step31_vip_cmp_rng_density.md`
- `md/04_Tech_Task/reports/yw2_exefs_step32_common_enc_table4_density.md`
- `md/04_Tech_Task/reports/yw2_exefs_step33_bank34_5c_callers_density.md`
- `md/04_Tech_Task/reports/yw2_exefs_step33_bank_callers_parent_density.md`
- `md/04_Tech_Task/reports/yw2_exefs_step34_save_diff_method_density.md`
- `md/04_Tech_Task/reports/yw2_exefs_step35_final_summary.md`
- `md/04_Tech_Task/reports/yw2_exefs_step36_vtable_rng_density.md`
- `md/04_Tech_Task/reports/yw2_exefs_step37_vip_density.md`
- `md/04_Tech_Task/reports/yw2_exefs_step38_config_hash_density.md`
- `md/04_Tech_Task/reports/yw2_exefs_step39_prob_table_raw.txt`
- `md/04_Tech_Task/reports/step40_vip_prob_raw.txt`
- `md/04_Tech_Task/reports/step41_vip_origin_raw.txt`
- `md/04_Tech_Task/reports/step42_raw.txt`
- `md/04_Tech_Task/reports/step42_sender_vip_raw.txt`
- `md/04_Tech_Task/reports/yw2_exefs_step43_sender_vip.md`
- `md/04_Tech_Task/reports/yw2_exefs_step44_vip_hash_insert.md`
- `md/04_Tech_Task/reports/yw2_exefs_step45_receiver_vip_rng.md`
- `md/04_Tech_Task/reports/yw2_exefs_step46_vip_100_patch.md`

