# 🚀 ICN_T2 iOS 26 제어센터 UI - 완전 구현 로드맵

> **프로젝트 목표**: Windows WPF에서 **iOS 26 제어센터의 Glassmorphism UI** 재현
>
> **최종 결과물**: 마우스 추적, 동적 유리 왜곡, Spring 애니메이션, Acrylic 효과가 결합된 프리미엄급 UI

---

## 📊 전체 구현 단계

```
┌─────────────────────────────────────────────────────────────────┐
│ Phase 1: UI 스타일 (iOS 26 컬러팔레트)                         │
│ ✅ 완료: Acrylic 배경색, Edge Glow 기초 구조                   │
├─────────────────────────────────────────────────────────────────┤
│ Phase 2: 전역 마우스 추적 + Spring 애니메이션                  │
│ ⏳ 진행 예정: Edge Glow 고도화, 버튼 진입 애니메이션          │
├─────────────────────────────────────────────────────────────────┤
│ Phase 3: 도구 메뉴 확장 로직 검증                              │
│ ✅ 완료: "윗쪽 80px만 확장" 구현 확인                         │
├─────────────────────────────────────────────────────────────────┤
│ Phase 4: Acrylic 배경 효과 (선택)                              │
│ ✅ 완료: 현재 상태 최적화, Windows 11 Mica (선택)             │
├─────────────────────────────────────────────────────────────────┤
│ Phase 5: HLSL Shader 굴절 효과 (최종)                          │
│ 🎯 다음 단계: Perlin Noise 기반 실시간 유리 왜곡             │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🎯 각 Phase 상세 내용

### **Phase 1: iOS 26 제어센터 UI 스타일** ✅
**상태**: 완료
**구현 내용**:
- Acrylic 배경색 (#D8E8F5F8) - 모든 카드에 적용
- 얇은 흰색 테두리 (1.5px, #15FFFFFF)
- 부드러운 드롭 섀도우 (40px blur)
- 둥근 모서리 (24px CornerRadius)

**파일**:
- `CharacterInfoV3.xaml` - 모든 Border에 스타일 적용

---

### **Phase 2: 전역 마우스 추적 + Spring 애니메이션** 🔄
**상태**: 구현 예정
**예상 시간**: 2.5-4시간

#### 2-1: Edge Glow 효과 (이미 구현됨)
```
✅ 현재 상태:
- EdgeGlowBehavior.cs로 카드 테두리에 반사광
- Window 레벨 MouseMove 이벤트 감지
- 마우스 위치에 따라 shine 실시간 업데이트
- 버튼 밖에서도 계속 추적

📍 위치: ICN_T2/UI/WPF/Behaviors/EdgeGlowBehavior.cs (247줄)
```

#### 2-2: Spring 애니메이션 (새로 추가)
```
🎯 요구사항:
- Time: 0.8초
- Bounce: 0.4 (탄력성)
- Initial Delay: 0.1초
- Stagger: 0.04초 (버튼 간 간격)
- Scale: 0.6 → 1.0

📝 작업 목록:
1. UIAnimationsRx.cs에 SpringScale(), SpringFadeAndScale() 메서드 추가
2. ModernModWindow.xaml.cs에 AnimateModdingToolsEntrance() 메서드 추가
3. AnimationConfig.cs에 Button Spring 설정 추가
4. 테스트 및 최적화

📍 파일:
- UIAnimationsRx.cs (라인 ~250 이후에 추가)
- ModernModWindow.xaml.cs (TransitionToToolWindow() 내)
- AnimationConfig.cs (이미 설정값 추가됨 ✅)
```

**구현 코드 샘플** (Phase 2 계획 문서 참고):
```csharp
// UIAnimationsRx.cs에 추가할 메서드
public static IObservable<Unit> SpringScale(
    FrameworkElement element,
    double targetScale = 1.0,
    double durationMs = 800,
    double bounce = 0.4)
{
    // ElasticEase + Storyboard 구현
}
```

---

### **Phase 3: 도구 메뉴 확장 로직** ✅
**상태**: 검증 완료
**확인 사항**:

```
✅ "윗쪽만 확장" - 이미 구현됨!
- StepProgress: 0.5 → 1.0
- Background_TopRiseHeight: 80px (상단 올라감)
- RightContentArea: 크기 변경 없음

✅ "오른쪽 창 확장 제거" - 이미 완료됨!
- RightContentArea.Margin만 조정
- Grid 너비는 고정

📍 위치:
- ModernModWindow.xaml.cs:
  - AnimateSteppedLayoutTo() (라인 1278-1301)
  - UpdateSteppedPath() (라인 1426-1500)
  - AnimateToolCompactLayout() (라인 1327-1423)

🎯 Phase 3의 역할: 기존 구현 검증 + 미세 조정
```

**설정값**:
```csharp
// AnimationConfig.cs
Background_TopRiseHeight = 80.0          // 위쪽 올라가는 높이
Background_StepXPosition = 400.0         // 경로 꺾이는 지점
Transition_LayoutDuration = 600          // 애니메이션 시간
MainPanel_ToolMenu_CompactMargin = 10.0  // 패널 마진 축소
```

---

### **Phase 4: Acrylic 배경 효과** ✅
**상태**: 완료 (현재 상태 유지)
**옵션**:

#### 옵션 A: 현재 상태 (권장) ✅
```
이미 구현된 것:
- Acrylic 색상 (#D8E8F5F8)
- Edge Glow 반사광
- 부드러운 그림자
- Glassmorphism 느낌

장점:
- 모든 환경에서 동작
- 성능 최적화됨
- iOS 제어센터 스타일 완성
```

#### 옵션 B: Windows 11 Mica (선택) 🎯
```
추가 구현 가능:
- Windows 11+ OS 요구
- Microsoft.Windows.SDK 패키지 필요
- DwmSetWindowAttribute로 시스템 수준 통합
- 배경화면 색상 동기화

추가 시간: 2-3시간
```

**결론**: 현재 상태로 충분함 → Phase 5로 진행

---

### **Phase 5: HLSL Shader 굴절 효과** 🚀
**상태**: 최종 구현 예정
**예상 시간**: 8-13시간

#### 핵심 기술
```
HLSL Pixel Shader 3.0:
- Perlin-like Noise 함수
- 마우스 위치 기반 동적 왜곡
- 실시간 텍스처 변형

구현 계획:
1. GlassRefraction.fx (HLSL 소스) 작성
2. FXC.exe로 컴파일 → GlassRefraction.ps 생성
3. GlassRefractionEffect.cs (WPF Wrapper) 구현
4. ModernModWindow.cs에 통합
5. 성능 최적화 & 테스트
```

**최종 결과**:
```
도구 메뉴 진입 시:
┌─────────────────────────────┐
│ 1. Spring 애니메이션 (버튼) │
│ 2. 배경 윗쪽으로 확장 (80px)│
│ 3. 유리 왜곡 효과 활성화    │
│ 4. 마우스 추적 → 실시간 변형│
│ 5. Edge Glow 반사광 유지    │
└─────────────────────────────┘

→ iOS 26 제어센터처럼 프리미엄한 Glassmorphism UI!
```

**파일**:
```
ICN_T2/UI/WPF/Effects/
├── GlassRefractionEffect.cs   ← WPF Wrapper (수정)
├── GlassRefraction.fx          ← HLSL 소스 (새 작성)
└── GlassRefraction.ps          ← 컴파일된 바이너리

ICN_T2/
├── ModernModWindow.xaml.cs    ← 통합 로직 추가
└── ICN_T2.csproj              ← Build Event 추가
```

---

## 📋 구현 우선순위 & 일정

### Week 1: Phase 2 (Spring 애니메이션)
```
Day 1-2: UIAnimationsRx.cs 메서드 추가
Day 2-3: ModernModWindow.cs 통합
Day 3-4: 테스트 & 최적화
기대효과: 버튼이 Spring 애니메이션으로 우아하게 나타남
```

### Week 2: Phase 3-4 (확장 로직 & 배경)
```
Day 1: Phase 3 검증 (UpdateSteppedPath 확인)
Day 2-3: 필요시 미세 조정
Day 4: Phase 4 결정 (현재 유지 vs. Mica 추가)
기대효과: 도구 메뉴 확장이 부드럽고 자연스러움
```

### Week 3-4: Phase 5 (HLSL Shader)
```
Day 1-2: HLSL Shader 작성 & 컴파일
Day 3-4: WPF Wrapper 구현
Day 5-6: ModernModWindow 통합
Day 7-8: 성능 최적화 & 테스트
기대효과: 실시간 유리 왜곡으로 최고급 UI 완성
```

---

## 🎯 각 파일별 작업 요약

### **UIAnimationsRx.cs**
```
위치: ICN_T2/UI/WPF/Animations/
기능: Rx 기반 애니메이션 헬퍼

추가 메서드 (Phase 2):
✅ SpringScale()
✅ SpringFadeAndScale()
✅ 이징 함수: ElasticEase(bounce 파라미터)
```

### **ModernModWindow.xaml.cs**
```
위치: ICN_T2/UI/WPF/
기능: 전체 창 로직 & 애니메이션 조율

추가 메서드 (Phase 2):
✅ AnimateModdingToolsEntrance()
   - 각 버튼에 Stagger 애니메이션 적용

추가 메서드 (Phase 5):
✅ UpdateShaderAnimation()
   - 매 프레임 Shader 업데이트
✅ Window_MouseMove()
   - 마우스 추적 → Shader에 전달
```

### **AnimationConfig.cs**
```
위치: ICN_T2/UI/WPF/Animations/
기능: 모든 애니메이션 설정값 관리

이미 추가됨 (Phase 2) ✅:
✅ Button_SpringDuration = 800
✅ Button_SpringBounce = 0.4
✅ Button_InitialDelay = 100
✅ Button_StaggerDelay = 40
✅ Button_FromScale = 0.6
✅ Button_ToScale = 1.0
```

### **CharacterInfoV3.xaml**
```
위치: ICN_T2/UI/WPF/Views/
기능: 캐릭터 정보 UI

이미 적용됨 (Phase 1) ✅:
✅ EdgeGlowBehavior.IsEnabled="True"
✅ Background="#D8E8F5F8" (Acrylic)
✅ BorderBrush="#15FFFFFF" (얇은 테두리)

추가 예정 (Phase 5):
✅ GlassRefractionEffect 적용 (후기)
```

### **GlassRefractionEffect.cs**
```
위치: ICN_T2/UI/WPF/Effects/
기능: HLSL Shader 래퍼

현재: 스켈레톤만 있음 (구현 필요)
필요한 것:
✅ PixelShader 로드
✅ 5개 Dependency Property
✅ Shader 상수 바인딩
```

### **GlassRefraction.fx** (새 파일)
```
위치: ICN_T2/UI/WPF/Effects/
기능: HLSL 피클 셰이더 소스

구현:
✅ Perlin-like Noise 함수
✅ 마우스 기반 왜곡 벡터
✅ 텍스처 샘플링 + 오프셋
✅ 엣지 페이드 (부드러운 경계)

컴파일: fxc.exe로 .ps 생성
```

---

## 🔍 마일스톤 & 성공 기준

### Milestone 1: Phase 2 완료
```
✅ 도구 메뉴 진입 시 버튼이 Spring 애니메이션으로 나타남
✅ 각 버튼 간 0.04초 Stagger 간격 확인
✅ 스프링 탄력 (bounce=0.4) 느껴짐
✅ 60 FPS 유지
✅ Edge Glow 반사광 정상 작동
```

### Milestone 2: Phase 3-4 검증 완료
```
✅ 도구 메뉴 진입 시 배경이 위쪽으로만 확장 (80px)
✅ RightContentArea 너비 변경 없음
✅ MainContentPanel 마진 축소로 공간 활용 최적화
✅ 전체 애니메이션 부드러움 (CubicEase)
```

### Milestone 3: Phase 5 완료
```
✅ 마우스 움직임에 따라 유리처럼 왜곡되는 효과
✅ Perlin-like Noise로 자연스러운 파도
✅ 60 FPS 고성능 유지
✅ 메모리 누수 없음
✅ 모든 Phase (1-4)와 통합 동작
```

---

## 🚀 최종 비전: 완성된 UI

### 사용자 경험 흐름

```
1️⃣ 도구 아이콘 클릭
   ↓
2️⃣ 메달 팝업 + 비행 애니메이션 (기존)
   ↓
3️⃣ 배경이 위쪽으로 80px 상승 (Phase 3)
   ↓
4️⃣ 버튼들이 동시에 Spring 애니메이션으로 나타남 (Phase 2)
   - 0.1초 초기 딜레이
   - 0.04초 Stagger 간격
   - 0.6배 스케일에서 1.0으로 확대
   - 탄력 있는 움직임 (bounce=0.4)
   ↓
5️⃣ CharacterInfoV3 카드들이 페이드인
   - Acrylic 배경색 (#D8E8F5F8) 강조
   - 테두리 Edge Glow 반사광 활성화
   ↓
6️⃣ 마우스를 움직이면 Shader 효과 활성화 (Phase 5)
   - 유리가 물결처럼 왜곡됨
   - 마우스 위치에 따라 실시간 변형
   - Perlin Noise로 자연스러운 파동
   ↓
7️⃣ iOS 26 제어센터처럼 프리미엄한 UI 완성! ✨
```

### 기술 스택 요약

| 계층 | 기술 | 파일 |
|-----|------|------|
| **UI Style** | Acrylic + Edge Glow | CharacterInfoV3.xaml |
| **Animation** | Spring (ElasticEase) | UIAnimationsRx.cs |
| **Layout** | SteppedBackground + CompactLayout | ModernModWindow.cs |
| **Effects** | HLSL Pixel Shader | GlassRefractionEffect.cs |
| **Config** | Centralized Settings | AnimationConfig.cs |

---

## 📚 참고 문서

```
📄 PHASE_2_DETAILED_PLAN.md     - Spring 애니메이션 상세
📄 PHASE_3_4_DETAILED_PLAN.md   - 확장 로직 & Acrylic
📄 PHASE_5_FINAL_HLSL_SHADER.md - HLSL Shader 완전 가이드
📄 IMPLEMENTATION_ROADMAP.md    - 이 문서
```

---

## ✅ 체크리스트 (지금부터 시작)

### 즉시 진행 (Phase 2)
- [ ] UIAnimationsRx.cs에 SpringScale() 메서드 추가
- [ ] UIAnimationsRx.cs에 SpringFadeAndScale() 메서드 추가
- [ ] ModernModWindow.xaml.cs에 AnimateModdingToolsEntrance() 추가
- [ ] 마우스 MouseMove 이벤트 핸들러 추가
- [ ] Stagger 애니메이션 로직 구현
- [ ] 테스트 및 튜닝

### 다음 진행 (Phase 3-4)
- [ ] UpdateSteppedPath() 메서드 검증
- [ ] 설정값 미세 조정 (필요시)
- [ ] 성능 모니터링 (FPS, 메모리)
- [ ] Phase 4 선택 (현재 유지 vs. Mica)

### 최종 진행 (Phase 5)
- [ ] HLSL Shader 코드 작성
- [ ] FXC.exe로 컴파일
- [ ] GlassRefractionEffect.cs 구현
- [ ] ModernModWindow에 통합
- [ ] 마우스 추적 시스템 구축
- [ ] 성능 최적화
- [ ] 최종 테스트

---

## 🎉 프로젝트 완료 예상 시점

```
📅 현재: Phase 1-4 완료 (또는 거의 완료)
📅 +1-2주: Phase 2 완료 (Spring 애니메이션)
📅 +2-3주: Phase 3-4 검증 완료 (확장 로직)
📅 +4-5주: Phase 5 완료 (HLSL Shader)

✨ 최종: iOS 26 제어센터 스타일 Glassmorphism UI 완성!
```

---

**이것이 ICN_T2의 최고급 UI 구현 로드맵입니다. 🚀**

