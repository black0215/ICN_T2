# 🎯 Phase 2: 전역 마우스 추적 + Edge Glow 효과 & 버튼 진입 애니메이션

> **목표**: CharacterInfoV3의 모든 카드에 iOS 제어센터 스타일 Edge Glow 효과 + 도구 메뉴 진입 시 staggered 애니메이션 구현

---

## 📊 현재 상태 분석

### ✅ 이미 구현된 것
1. **EdgeGlowBehavior.cs** - 매우 잘 구현된 Attached Behavior
   - 각 Border의 테두리에 마우스 위치 기반 반사광 적용
   - GlowIntensity, GlowWidth 설정 가능
   - 엣지 근처(25% 범위)에서만 활성화

2. **CharacterInfoV3.xaml** - 모든 카드에 EdgeGlowBehavior 적용됨
   - Identity Card: `EdgeGlowBehavior.IsEnabled="True"` ✓
   - Medal Info Card: `EdgeGlowBehavior.IsEnabled="True"` ✓
   - Food Preferences Card: `EdgeGlowBehavior.IsEnabled="True"` ✓
   - Description Card: `EdgeGlowBehavior.IsEnabled="True"` ✓
   - Search Panel (Left): `EdgeGlowBehavior.IsEnabled="True"` ✓

3. **UIAnimationsRx.cs** - Rx 기반 애니메이션 시스템
   - Fade, Pop, Scale, Translate 등 다양한 애니메이션 메서드 있음
   - DispatcherScheduler로 안전한 UI 스레드 관리

---

## 🔧 Phase 2 작업 상세 분석

### **1️⃣ Task: Edge Glow 효과 최적화**

#### 현재 상황
- EdgeGlowBehavior는 **각 Border가 독립적으로 Window.MouseMove 이벤트 감지**
- 각 Border 내에서 마우스 좌표를 계산하고 반사광 위치 업데이트

#### 요청사항
- "창 전체 어디서든 마우스 움직임이 모든 버튼에 반영"
- 마우스가 버튼을 벗어나도 shine이 계속 업데이트

#### 현재 방식이 이미 충족하는가?
✅ **YES!** 현재 코드를 분석하면:
```csharp
private static void RegisterMouseTracking(Border border)
{
    Window window = Window.GetWindow(border);
    if (window != null)
    {
        window.MouseMove += (s, e) => OnWindowMouseMove(border, e);
    }
}
```
- Window 레벨에서 MouseMove 이벤트 감지
- 각 Border가 e.GetPosition(border)로 상대 좌표 계산
- 즉, 버튼 외부에서도 마우스 움직임 반영됨

#### 결론
✅ **현재 구현이 이미 요청사항을 만족**
- 변경 불필요, 그대로 유지
- 다만, CharacterInfoV3의 카드들이 올바르게 동작하는지 **테스트 확인 필요**

---

### **2️⃣ Task: 버튼 진입 애니메이션 (Staggered Delay)**

#### 요청 사양
```
spring, based on time
- time = 0.8s
- bounce = 0.4 (탄력성)
- delay = 0.1s (초기 딜레이)
- stagger = 0.04s (버튼 간 간격)
```

#### 구현 위치
**ModernModWindow.xaml.cs** - `TransitionToToolWindow()` 메서드에 추가

#### 구현 전략

**방식: UIAnimationsRx에 Spring 애니메이션 메서드 추가**

```csharp
// 1. UIAnimationsRx.cs에 Spring 애니메이션 메서드 추가
public static IObservable<Unit> SpringScale(
    FrameworkElement element,
    double targetScale = 1.0,
    double durationMs = 800,
    double bounce = 0.4)
{
    // Rx 기반 Spring 애니메이션
    // EasingMode: ElasticEase with Bounce 파라미터
    // KeyFrame 기반으로 스프링 효과 표현
}

public static IObservable<Unit> SpringFadeAndScale(
    FrameworkElement element,
    double fromOpacity = 0,
    double toOpacity = 1,
    double fromScale = 0.8,
    double toScale = 1.0,
    double durationMs = 800,
    double bounce = 0.4)
{
    // Fade + Scale 동시 진행
    // 스프링 탄력 효과 포함
}
```

**방식: 도구 메뉴 진입 시 버튼 Stagger 적용**

```csharp
// 2. ModernModWindow.xaml.cs - TransitionToToolWindow() 내에서

// 도구 메뉴 콘텐츠가 보이기 시작
ModdingMenuContent.Visibility = Visibility.Visible;

// 각 버튼에 Staggered 애니메이션 적용
int buttonCount = ModdingMenuContent.Items.Count;
double initialDelay = 100; // 0.1s
double staggerDelay = 40;  // 0.04s
double totalDuration = 800; // 0.8s
double bounce = 0.4;

for (int i = 0; i < buttonCount; i++)
{
    var buttonUI = ModdingMenuContent.ItemContainerGenerator.ContainerFromIndex(i) as UIElement;
    if (buttonUI == null) continue;

    double delayMs = initialDelay + (i * staggerDelay);

    // 딜레이 후 Spring 애니메이션 시작
    Observable.Timer(TimeSpan.FromMilliseconds(delayMs))
        .SelectMany(_ => UIAnimationsRx.SpringFadeAndScale(
            buttonUI,
            fromOpacity: 0,
            toOpacity: 1,
            fromScale: 0.6,
            toScale: 1.0,
            durationMs: totalDuration,
            bounce: bounce
        ))
        .Subscribe();
}
```

---

## 📋 실제 구현 작업 계획

### **작업 1: UIAnimationsRx.cs에 Spring 메서드 추가**

**파일**: `C:\Users\home\Desktop\ICN_T2\ICN_T2\UI\WPF\Animations\UIAnimationsRx.cs`

**추가할 메서드**:

```csharp
// 1. SpringScale 메서드
public static IObservable<Unit> SpringScale(
    FrameworkElement element,
    double targetScale = 1.0,
    double durationMs = 800,
    double bounce = 0.4)
{
    // 구현:
    // - EnsureMutableTransformGroup() 호출
    // - ElasticEase 사용
    // - bounce 파라미터로 탄력성 제어
    // - Storyboard로 ScaleX, ScaleY 동시 애니메이션
}

// 2. SpringFadeAndScale 메서드
public static IObservable<Unit> SpringFadeAndScale(
    FrameworkElement element,
    double fromOpacity = 0,
    double toOpacity = 1,
    double fromScale = 0.8,
    double toScale = 1.0,
    double durationMs = 800,
    double bounce = 0.4)
{
    // 구현:
    // - Opacity 애니메이션 (선형 또는 EaseOut)
    // - Scale 애니메이션 (ElasticEase with bounce)
    // - 두 애니메이션을 Storyboard로 동시 진행
    // - Observable.Merge로 완료 신호 처리
}
```

---

### **작업 2: ModernModWindow.xaml.cs 수정**

**파일**: `C:\Users\home\Desktop\ICN_T2\ICN_T2\UI\WPF\ModernModWindow.xaml.cs`

**수정 위치**: `TransitionToToolWindow()` 메서드 내, 도구 메뉴 콘텐츠 표시 부분

**추가할 코드**:

```csharp
private async Task AnimateModdingToolsEntrance()
{
    // 도구 메뉴 콘텐츠가 이미 Visibility=Visible로 설정됨

    // 설정값
    const double INITIAL_DELAY_MS = 100;  // 0.1s
    const double STAGGER_DELAY_MS = 40;   // 0.04s
    const double TOTAL_DURATION_MS = 800; // 0.8s
    const double BOUNCE = 0.4;

    // ModdingMenuContent의 모든 버튼에 Staggered 애니메이션 적용
    int itemCount = ModdingMenuContent.Items.Count;
    var animationTasks = new List<Task>();

    for (int i = 0; i < itemCount; i++)
    {
        var container = ModdingMenuContent.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
        if (container == null) continue;

        // Button 찾기
        var button = FindVisualChild<Button>(container);
        if (button == null) continue;

        double delayMs = INITIAL_DELAY_MS + (i * STAGGER_DELAY_MS);

        // 딜레이 후 애니메이션 시작
        var animTask = Task.Delay((int)delayMs).ContinueWith(_ =>
        {
            button.Opacity = 0;
            button.Visibility = Visibility.Visible;

            _animationService.SpringFadeAndScale(
                button,
                fromOpacity: 0,
                toOpacity: 1,
                fromScale: 0.6,
                toScale: 1.0,
                durationMs: (int)TOTAL_DURATION_MS,
                bounce: BOUNCE
            ).Subscribe();
        });

        animationTasks.Add(animTask);
    }

    // 모든 애니메이션이 시작될 때까지 대기
    if (animationTasks.Count > 0)
    {
        await Task.WhenAll(animationTasks);
    }
}

// 헬퍼 메서드: VisualTree에서 자식 요소 찾기
private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
{
    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
    {
        var child = VisualTreeHelper.GetChild(obj, i);
        if (child is T result)
            return result;

        var childOfChild = FindVisualChild<T>(child);
        if (childOfChild != null)
            return childOfChild;
    }
    return null;
}
```

---

### **작업 3: AnimationConfig.cs에 Spring 애니메이션 설정 추가** (선택)

**파일**: `C:\Users\home\Desktop\ICN_T2\ICN_T2\UI\WPF\Animations\AnimationConfig.cs`

**추가할 상수**:

```csharp
// 버튼 진입 애니메이션 (Spring)
public const double Button_SpringDuration = 0.8;      // 0.8초
public const double Button_SpringBounce = 0.4;        // 탄력성
public const double Button_InitialDelay = 0.1;        // 0.1초
public const double Button_StaggerDelay = 0.04;       // 0.04초
public const double Button_FromScale = 0.6;           // 초기 스케일
public const double Button_ToScale = 1.0;             // 최종 스케일
public const double Button_FromOpacity = 0;           // 투명
public const double Button_ToOpacity = 1;             // 불투명
```

---

## 🎨 Spring 애니메이션 구현 상세 가이드

### ElasticEase와 Bounce 파라미터 관계

```csharp
// ElasticEase의 Oscillations과 bounce 파라미터 매핑
double oscillations = 3.0;  // 기본값
double springiness = bounce * 2;  // bounce=0.4 → springiness=0.8

var easing = new ElasticEase
{
    EasingMode = EasingMode.EaseOut,
    Oscillations = oscillations,
    Springiness = springiness
};
```

### Storyboard 예시

```csharp
var sb = new Storyboard();

// Fade In 애니메이션 (선형)
var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs))
{
    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
};
Storyboard.SetTarget(fadeAnim, element);
Storyboard.SetTargetProperty(fadeAnim, new PropertyPath(UIElement.OpacityProperty));
sb.Children.Add(fadeAnim);

// Scale 애니메이션 (Spring)
var scaleAnim = new DoubleAnimation(fromScale, toScale, TimeSpan.FromMilliseconds(durationMs))
{
    EasingFunction = new ElasticEase
    {
        EasingMode = EasingMode.EaseOut,
        Oscillations = 3,
        Springiness = bounce * 2
    }
};
Storyboard.SetTarget(scaleAnim, scaleTransform);
Storyboard.SetTargetProperty(scaleAnim, new PropertyPath(ScaleTransform.ScaleXProperty));
sb.Children.Add(scaleAnim);

// ScaleY도 동일하게
var scaleAnimY = new DoubleAnimation(fromScale, toScale, TimeSpan.FromMilliseconds(durationMs))
{
    EasingFunction = scaleAnim.EasingFunction as ElasticEase
};
Storyboard.SetTarget(scaleAnimY, scaleTransform);
Storyboard.SetTargetProperty(scaleAnimY, new PropertyPath(ScaleTransform.ScaleYProperty));
sb.Children.Add(scaleAnimY);

sb.Begin();
```

---

## 📝 테스트 체크리스트

### ✅ Edge Glow 테스트
- [ ] CharacterInfoV3의 Identity Card에 마우스 이동 시 테두리 shine 활성화
- [ ] 마우스가 카드 25% 범위 내에 있을 때만 shine 표시
- [ ] 마우스가 카드 밖으로 나가도 shine이 계속 업데이트 (창 내 다른 위치에서)
- [ ] 모든 카드(Medal, Food, Description, Search Panel)에서 동일하게 작동
- [ ] Edge Glow 강도(GlowIntensity=0.4)가 적절한가?

### ✅ Spring 애니메이션 테스트
- [ ] 도구 메뉴 진입 시 버튼이 동시에 나타남 (동시 등장)
- [ ] 각 버튼 간 0.04초 Stagger 간격 확인
- [ ] 첫 번째 버튼은 0.1초 후 시작
- [ ] 스프링 탄력 효과(bounce=0.4)가 느껴지는가?
- [ ] 총 애니메이션 시간이 0.8초인가?
- [ ] 버튼이 0.6 스케일에서 1.0으로 확대되는가?

### ✅ 성능 테스트
- [ ] 모든 버튼 애니메이션이 동시에 진행될 때 FPS 드롭 확인
- [ ] EdgeGlowBehavior가 마우스 움직임에 충분히 빠르게 반응하는가?
- [ ] 메모리 누수 확인 (장시간 실행 후)

---

## 🔗 파일 구조 요약

```
ICN_T2/
├── UI/WPF/
│   ├── Animations/
│   │   ├── UIAnimationsRx.cs          ← SpringScale, SpringFadeAndScale 추가
│   │   ├── AnimationConfig.cs         ← Button Spring 설정 추가 (선택)
│   │   └── ...
│   ├── Behaviors/
│   │   ├── EdgeGlowBehavior.cs        ← ✅ 이미 구현됨, 수정 불필요
│   │   └── ...
│   ├── Views/
│   │   ├── CharacterInfoV3.xaml       ← ✅ 이미 EdgeGlowBehavior 적용됨
│   │   └── ...
│   └── ModernModWindow.xaml.cs        ← AnimateModdingToolsEntrance() 추가
```

---

## ⏰ 예상 작업 시간

| 작업 | 예상 시간 | 난이도 |
|-----|---------|--------|
| UIAnimationsRx에 Spring 메서드 추가 | 1-2시간 | ⭐⭐⭐ |
| ModernModWindow에 Stagger 로직 추가 | 30-45분 | ⭐⭐ |
| AnimationConfig에 설정 추가 | 10분 | ⭐ |
| 테스트 및 디버깅 | 30분-1시간 | ⭐⭐ |
| **총합** | **2.5-4시간** | |

---

## 🎯 최종 결과물

### 도구 메뉴 진입 시 사용자 경험 (UX)
1. 사용자가 도구 아이콘 클릭
2. 0.1초 후 첫 번째 버튼이 스프링 애니메이션으로 출현
3. 각 버튼이 0.04초 간격으로 순차 등장 (stagger)
4. 각 버튼은 0.6배 스케일에서 1.0으로 탄력 있게 확대
5. 총 애니메이션 시간 0.8초

### Edge Glow 효과
1. 사용자가 마우스를 카드 근처로 이동
2. 카드 테두리 가장자리에 미세한 흰색 반사광 출현
3. 마우스 움직임에 따라 shine이 실시간 추적
4. 버튼을 벗어나도 창 내 다른 위치에서 shine 업데이트됨

---

## 🚀 다음 단계

Phase 2 완료 후:
- **Phase 3**: 도구 메뉴 확장 로직 수정 (윗쪽만 확장)
- **Phase 4**: Acrylic 배경 효과 (WPF Backdrop) - 선택
- **Phase 5**: HLSL Shader 기반 굴절 효과 (나중에)

