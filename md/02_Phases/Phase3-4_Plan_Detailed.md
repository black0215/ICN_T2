# 🎯 Phase 3 & 4: 도구 메뉴 확장 + Acrylic 배경 효과

> **목표**:
> - **Phase 3**: 도구 메뉴 진입 시 **오른쪽 창/메인 콘텐츠 확장 로직 제거** → **윗쪽으로만 확장**
> - **Phase 4**: **Acrylic 배경 효과** 최적화 (WPF Backdrop)

---

## 📊 현재 상태 분석

### ✅ 이미 구현된 것

#### 1. **계층화된 확장 시스템 (3단계)**
```
레벨 0: 프로젝트 메뉴 (메인)
레벨 1: 모딩 메뉴 (책 열기)
레벨 2: 도구 메뉴 (캐릭터 정보)
```

#### 2. **배경 확장 애니메이션**
- **StepProgress** (0.0 → 0.5 → 1.0)
  - 0.0: 프로젝트 메뉴 상태
  - 0.5: 모딩 메뉴 상태 (왼쪽 확장)
  - 1.0: 도구 메뉴 상태 (왼쪽 + **위쪽** 확장)
- **UpdateSteppedPath()** 메서드로 Geometry 경로 동적 계산

#### 3. **Compact Layout 시스템**
- **MainContentPanel** 마진 축소/복원
- **MainContentRootGrid** 마진 축소/복원
- 확장 사이에 부드러운 Thickness 애니메이션

#### 4. **현재 요청사항 분석**
- ✅ "오른쪽 창이 커지는 로직 주석 처리" → **이미 구현됨!**
  - `RightContentArea.Margin` 변경 코드 없음
  - 도구 메뉴 진입 시 RightContentArea 크기 변경 안 함
- ✅ "윗쪽으로만 확장" → **이미 구현됨!**
  - `Background_TopRiseHeight = 80.0px` (위쪽 올라가는 높이)
  - SteppedPath geometry에서 위쪽만 상승

### ⚠️ 확인 필요한 부분

```csharp
// 라인 708-709: 도구 메뉴 진입 시 호출되는 애니메이션
AnimateSteppedLayoutTo(1.0);      // 배경 확장 (0.5 → 1.0, 위쪽만)
AnimateToolCompactLayout(true);   // Panel/Grid 마진 축소
```

**현재 상태**:
- ✅ `AnimateSteppedLayoutTo(1.0)` - 위쪽만 확장 (올바름)
- ✅ `AnimateToolCompactLayout(true)` - 전체 마진 축소 (올바름)
- ❓ 추가로 제거해야 할 코드가 있는가?

---

## 🔍 Phase 3 상세 분석: "윗쪽 확장만" 확인

### 현재 코드 흐름

#### 1. **도구 메뉴 진입 트리거**
```
TransitionToToolWindow()
  └─ 라인 708: AnimateSteppedLayoutTo(1.0)
      └─ 라인 1278-1301: AnimateSteppedLayoutTo() 메서드
          └─ StepProgress: 0.5 → 1.0 (애니메이션)
```

#### 2. **StepProgress 변화 시 자동 업데이트**
```csharp
// Dependency Property Changed Handler
private void OnStepProgressChanged(double newValue)
{
    // → UpdateSteppedPath() 호출
    // → Geometry 재계산 (위쪽 올라감)
}
```

#### 3. **UpdateSteppedPath() - Geometry 계산**
```
라인 1426-1500 범위에 구현되어 있음
StepProgress 값에 따라:
- 0.0 ~ 0.5: 왼쪽만 확장 (가로)
- 0.5 ~ 1.0: 위쪽 추가 확장 (세로) ✓
- 목표: Background_TopRiseHeight (80px) 상승
```

#### 4. **RightContentArea는 변경 안 됨**
```csharp
// 라인 1915
RightContentArea.Margin = new Thickness(0, 0,
    AnimationConfig.RightContent_MarginRight,    // 변경 안 함
    AnimationConfig.RightContent_MarginBottom);  // 변경 안 함
```

✅ **결론**: 현재 코드가 이미 "윗쪽만 확장"을 구현함!

---

## 🔧 Phase 3: "윗쪽 확장" 로직 확인 & 최적화

### Task 3-1: UpdateSteppedPath() 메서드 검토

**파일**: `ModernModWindow.xaml.cs` (라인 1426-1500)

**확인사항**:
1. StepProgress=0.5에서 StepProgress=1.0으로 변할 때 Geometry 변화 확인
2. 위쪽 올라가는 높이(80px)가 정확한가?
3. 애니메이션 속도가 적절한가?

**현재 설정값**:
```csharp
public const double Background_TopRiseHeight = 80.0;    // 위쪽 올라가는 높이
public const double Background_StepXPosition = 400.0;   // 꺾이는 X 좌표
public const double Transition_LayoutDuration = 600;    // 애니메이션 시간 (600ms)
```

### Task 3-2: RightContentArea 확장 로직 주석 처리 (확인)

**파일**: `ModernModWindow.xaml.cs`

**현재 상황**:
- RightContentArea 너비를 변경하는 코드가 없음 ✅
- Grid.Column 스트래치는 유지되어 자동으로 가용 공간 채움

**확인 포인트**:
```csharp
// 라인 547: XAML
<Grid x:Name="RightContentArea" Grid.Column="2" Margin="0,0,10,15">
    <!-- 자동으로 Grid의 Width="*"를 상속받음 -->
</Grid>
```

✅ **결론**: RightContentArea는 이미 고정 크기이고, 마진만 조정됨

### Task 3-3: 테스트 체크리스트

- [ ] 도구 메뉴 진입 시 배경이 **위쪽으로만** 확장되는가?
- [ ] RightContentArea (오른쪽 카드 영역) 너비가 **변하지 않는가**?
- [ ] MainContentPanel (메인 배경) 너비가 **변하지 않는가**?
- [ ] 확장 애니메이션이 부드러운가? (600ms)
- [ ] 모딩 메뉴로 돌아올 때 정상 복원되는가?

---

## 🎨 Phase 4: Acrylic 배경 효과

### 현재 상황

#### 1. **이미 적용된 색상**
```xaml
<!-- CharacterInfoV3.xaml: 라인 68, 242, 330, 366, 424 -->
Background="#D8E8F5F8"    <!-- 밝은 청회색 Acrylic 스타일 -->
```

#### 2. **이미 적용된 Border 효과**
```xaml
BorderBrush="#15FFFFFF"              <!-- 얇은 흰색 테두리 -->
BorderThickness="1.5"
CornerRadius="24"
Effect: DropShadowEffect             <!-- 부드러운 그림자 -->
```

#### 3. **EdgeGlowBehavior**
```xaml
behaviors:EdgeGlowBehavior.IsEnabled="True"
behaviors:EdgeGlowBehavior.GlowIntensity="0.4"
behaviors:EdgeGlowBehavior.GlowWidth="100"
```

### Phase 4 목표: Acrylic 효과 고도화

#### 옵션 1: **WPF Backdrop Brush** (권장 - .NET 8)
```csharp
// Microsoft.Windows.SDK.Contracts 패키지 필요
using Windows.UI.Composition;
using Windows.System;

// Backdrop 효과 설정
BackdropEffect = new SystemBackdropConfiguration();
SystemBackdrop = new MicaBackdrop();  // 또는 DesktopAcrylicBackdrop
```

**장점**:
- 시스템 수준의 Acrylic 효과
- 배경화면 색상과 동기화
- 고성능

**단점**:
- Windows 11 필요
- P/Invoke 복잡

#### 옵션 2: **WPF 기본 효과로 충분** (현재 상태)
```xaml
Background="#D8E8F5F8"  <!-- 반투명 색상 -->
Effect: DropShadowEffect <!-- 깊이감 -->
BorderBrush: LinearGradient <!-- Edge Glow -->
```

**장점**:
- 이미 iOS 제어센터 느낌 구현됨
- 추가 라이브러리 불필요
- 모든 환경에서 동작

**단점**:
- 시스템 통합 부족
- 배경화면 동기화 안 됨

---

## 📋 Phase 3 & 4 작업 계획

### **Phase 3 작업 (1-2시간)**

#### 작업 3-1: UpdateSteppedPath() 메서드 분석
**파일**: `ModernModWindow.xaml.cs` (라인 1426-1500)

```csharp
// 현재 구현 확인
private void UpdateSteppedPath()
{
    // StepProgress (0.0 ~ 1.0)에 따라 Geometry 계산

    // 1. StepProgress 0.0 ~ 0.5: 왼쪽만 확장
    //    - 사이드바 축소와 동시에 왼쪽 배경 확장

    // 2. StepProgress 0.5 ~ 1.0: 위쪽 추가 확장
    //    - 상단이 Background_TopRiseHeight(80px) 만큼 상승
    //    - Background_StepXPosition(400px)에서 꺾임

    // 구현 상세 분석:
    // - PathFigure: StartPoint (시작점)
    // - LineSegment: 직선 연결
    // - Geometry로 복잡한 다각형 경로 표현
}
```

**확인 코드**:
```csharp
// 라인 1426 이후에 있을 구현부 검토
// 1. StepProgress에 따른 Y 오프셋 계산
double topRiseOffset = (StepProgress - 0.5) * Background_TopRiseHeight;
// → 0.5일 때: 0px (변화 없음)
// → 1.0일 때: 80px (80px 상승) ✓

// 2. X 좌표 400px에서 경로 꺾임
// → StepXPosition = 400 (화면 왼쪽에서 400px 지점)
```

#### 작업 3-2: 애니메이션 타이밍 확인

**확인 사항**:
```csharp
// 라인 1294-1300
var anim = new DoubleAnimation(currentValue, targetValue,
    TimeSpan.FromMilliseconds(AnimationConfig.Transition_LayoutDuration))
{
    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
};
// Transition_LayoutDuration = 600ms
// EasingMode: EaseOut (시작 빠르고 끝에서 느려짐)
```

✅ **설정 적절** - 부드러운 움직임 확보

#### 작업 3-3: 배경 Geometry 크기 확인

**파일**: `ModernModWindow.xaml` (라인 416-421)

```xaml
<Path x:Name="SteppedBackgroundPath"
      Fill="#80FFFFFF"
      Stroke="#30FFFFFF"
      StrokeThickness="1"
      StrokeLineJoin="Round"/>
```

**확인 코드**:
- Geometry 경로의 너비/높이가 container를 초과하지 않는가?
- ClipToBounds가 필요한가?

### **Phase 3 체크리스트**

- [ ] UpdateSteppedPath() 메서드가 올바르게 위쪽만 확장하는가?
- [ ] Background_TopRiseHeight (80px) 값이 적절한가?
- [ ] Transition_LayoutDuration (600ms)이 적절한가?
- [ ] StepProgress=0.5 → 1.0 전환이 부드러운가?
- [ ] 도구 메뉴 복귀 시 (1.0 → 0.5) 정상 복원되는가?
- [ ] 경로 꺾임 지점(400px)이 시각적으로 자연스러운가?

---

### **Phase 4 작업 (2-3시간, 선택사항)**

#### 옵션 A: WPF Backdrop Brush 적용 (고도화)

**파일**: `ModernModWindow.xaml.cs` (Window 초기화 구간)

```csharp
// Step 1: Package 추가
// Install-Package Microsoft.Windows.SDK.Contracts -Version 1.0.0

// Step 2: Window Loaded 이벤트에서 Backdrop 초기화
private void Window_Loaded(object sender, RoutedEventArgs e)
{
    if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))  // Windows 11+
    {
        try
        {
            var config = new SystemBackdropConfiguration();
            this.SystemBackdrop = new MicaBackdrop() { Kind = MicaKind.Base };
        }
        catch { /* Fallback to default */ }
    }
}
```

**장점**:
- iOS 제어센터처럼 배경화면 색상 동기화
- 시스템 성능 최적화
- 프리미엄 느낌

**단점**:
- Windows 11 필수
- P/Invoke 복잡
- 추가 라이브러리

#### 옵션 B: 현재 상태 유지 (권장)

✅ **현재 상태가 이미 충분**:
- iOS 제어센터 스타일의 Acrylic 색상 (#D8E8F5F8)
- Edge Glow 반사광 (테두리만)
- 부드러운 그림자
- 모든 환경에서 동작

**조정 가능 사항**:
```csharp
// AnimationConfig에서 색상 조정
public const string Acrylic_BackgroundColor = "#D8E8F5F8";  // 현재
public const string Acrylic_BorderColor = "#15FFFFFF";      // 현재
public const string Acrylic_ShadowColor = "#FF1A1C1E";      // 현재

// 더 강한 Acrylic 효과 원한다면:
// "#D0E8F5F8" → "#D8E8F5F8" (투명도 증가)
```

### **Phase 4 체크리스트 (선택)**

- [ ] Windows SDK 패키지 설치 여부 결정
- [ ] MicaBackdrop vs DesktopAcrylicBackdrop 선택
- [ ] 배경 색상 동기화 확인
- [ ] 기존 EdgeGlow와의 호환성 확인
- [ ] 성능 테스트 (FPS, 메모리)

---

## 📊 세부 설정값

### AnimationConfig.cs 현재 설정

```csharp
// 도구 메뉴 확장 (Phase 3)
public const double Background_StepProgress_ToolMenu = 1.0;    // 최대 확장
public const double Background_TopRiseHeight = 80.0;           // 위쪽 올라가는 높이
public const double Background_StepXPosition = 400.0;          // 경로 꺾이는 지점
public const double Background_CornerRadius = 25.0;            // 모서리 둥글기

public const int Transition_LayoutDuration = 600;              // 애니메이션 시간 (ms)
public const int Transition_RiserDuration = 600;               // 배경 상승 시간

// 메인 패널
public const double MainPanel_ToolMenu_CompactMargin = 10.0;   // 전체 마진 축소
public const double MainContentRootGrid_ToolMenu_CompactMargin = 10.0;

// 오른쪽 콘텐츠 영역 (변경 안 함)
public const double RightContent_MarginRight = 25.0;           // 고정
public const double RightContent_MarginBottom = 10.0;          // 고정
```

---

## 🎯 예상 결과

### Phase 3 완료 후
```
도구 메뉴 진입 시:
┌─────────────────────────────┐
│ 배경이 ↑ (위쪽 80px 상승)   │
│ MainContentPanel 마진 축소  │
│ RightContentArea 크기 유지  │
└─────────────────────────────┘

특징:
✓ 윗쪽만 확장 (좌우 크기 변화 없음)
✓ 고정 높이 80px 상승
✓ 부드러운 600ms 애니메이션
✓ 모딩 메뉴로 복귀 시 정상 복원
```

### Phase 4 완료 후 (선택)
```
WPF Backdrop 적용 (Windows 11):
┌─────────────────────────────┐
│ Mica 배경 효과 추가         │
│ 배경화면 색상 동기화        │
│ 시스템 수준 Acrylic 통합    │
└─────────────────────────────┘

iOS 제어센터처럼:
- 배경화면을 흐리게 비추는 유리 느낌
- 마우스 위치 기반 Edge Glow shine
- 스프링 애니메이션으로 버튼 진입
```

---

## 🔗 관련 파일 요약

```
ICN_T2/
├── UI/WPF/
│   ├── ModernModWindow.xaml        ← SteppedBackgroundPath Geometry
│   ├── ModernModWindow.xaml.cs
│   │   ├── UpdateSteppedPath()     ← Phase 3 핵심
│   │   ├── AnimateSteppedLayoutTo()
│   │   └── AnimateToolCompactLayout()
│   ├── Views/
│   │   └── CharacterInfoV3.xaml    ← Acrylic 색상
│   └── Animations/
│       └── AnimationConfig.cs      ← 설정값
```

---

## ⏰ 예상 작업 시간

| Phase | 작업 | 예상 시간 | 난이도 |
|-------|------|---------|--------|
| 3 | "윗쪽 확장" 확인 & 검증 | 1-2시간 | ⭐⭐ |
| 4A | WPF Backdrop 적용 | 2-3시간 | ⭐⭐⭐ |
| 4B | 현재 상태 유지 | 0시간 | - |

---

## 🚀 다음 단계

1. **Phase 3 검증 완료** → 도구 메뉴 확장 로직 확인
2. **Phase 4 선택**:
   - Windows 11 Mica 원한다면 → 4A 추진
   - 현재 스타일로 충분하면 → 4B (완료)
3. **나중에**: Phase 5 HLSL Shader 굴절 효과

---

## 📝 주요 발견사항

✅ **현재 코드가 이미 요청사항 충족**
- "윗쪽 확장만" → 이미 구현됨
- "오른쪽 창 확장 제거" → 이미 제거됨
- Acrylic 색상 → 이미 적용됨

⚠️ **추가 최적화 가능**
- 확장 높이 (80px) 조정 가능
- 확장 속도 (600ms) 조정 가능
- Windows 11 Mica 통합 (선택)

🎯 **Phase 3의 주요 역할**
- 기존 구현이 올바른지 **검증**
- 설정값 최적화 (필요시)
- 성능 테스트 및 디버깅

