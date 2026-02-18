# 🎨 CharacterScaleView & YokaiStatsView 재설계 계획
## iOS 26 제어센터 스타일 통일 + "잠깐 보이고 사라지는" 버그 수정

> **목표**: CharacterInfoV3와 동일한 프리미엄 iOS 26 Glassmorphism 스타일로 통일하면서
> 안정적인 UI 렌더링과 상태 관리 보장

---

## 📊 현재 상태 분석

### CharacterInfoV3 (기준 모델) ✅
```
✨ 스타일:
- Acrylic 배경: #D8E8F5F8 (밝은 청회색)
- 테두리: #15FFFFFF (매우 얇은 흰색, 1.5px)
- 모서리: 24px CornerRadius (부드러움)
- 그림자: DropShadowEffect (40px blur, 0.08 opacity)
- Edge Glow: EdgeGlowBehavior.IsEnabled="True"

🎭 레이아웃:
- 왼쪽 패널: 32% (SearchPanel)
- 오른쪽 패널: 68% (DetailPanel)
- 스크롤 가능한 카드 레이아웃
- 부드러운 페이드인 애니메이션
```

### CharacterScaleView (현재) ❌
```
문제점 1: 스타일 불일치
- 배경: #50FFFFFF (다른 색상, 투명도 다름)
- 테두리: #40FFFFFF (더 밝음)
- 모서리: 15px (더 각짐)
- 그림자: 10px blur (약한 그림자)
- Edge Glow: 없음 ⚠️

문제점 2: 레이아웃 다름
- 고정 너비 패널 (고정 크기 비율)
- 다른 스타일의 입력 필드
- 부드러운 애니메이션 없음

⚠️ "잠깐 보이고 사라지는" 버그 원인 분석:
- Visibility 바인딩이 너무 빠르게 변함
- Opacity 애니메이션이 없어서 갑작스러움
- 콘텐츠 로딩 타이밍 문제
- 렌더링 레이아웃 지연
```

### YokaiStatsView (현재) ❌
```
문제점 1: 부분적으로 다른 스타일
- 더 나은 색상 사용 (#50FFFFFF, #40FFFFFF)
- 하지만 여전히 CharacterInfoV3와 다름
- 테두리/모서리 불일치

문제점 2: 버그 존재
- "잠깐 보이고 사라지는" 현상
- 탭 전환 시 깜빡임
- 렌더링 성능 문제
```

---

## 🎯 재설계 계획

### **Goal 1: 스타일 완전 통일**

#### CharacterInfoV3 스타일 기준
```xaml
<!-- 모든 카드/패널에 적용할 기본 스타일 -->

<!-- 배경 색상 -->
Background="#D8E8F5F8"  (밝은 청회색 Acrylic)

<!-- 테두리 -->
BorderBrush="#15FFFFFF"  (매우 얇은 흰색)
BorderThickness="1.5"

<!-- 모서리 -->
CornerRadius="24"  (부드러움)

<!-- 그림자 -->
Effect: DropShadowEffect
  Color="#FF1A1C1E"
  Opacity="0.08"
  BlurRadius="40"
  ShadowDepth="8"

<!-- Edge Glow 반사광 -->
behaviors:EdgeGlowBehavior.IsEnabled="True"
behaviors:EdgeGlowBehavior.GlowIntensity="0.4"
behaviors:EdgeGlowBehavior.GlowWidth="100"

<!-- 입력 필드 -->
TextBox Background="#30000000"
TextBox BorderThickness="0"
TextBox CornerRadius="6"
```

#### SharedResourceDictionary 생성 (권장)
```xaml
<!-- 파일: Themes/CharacterViewsTheme.xaml -->
<ResourceDictionary>
    <!-- Shared Color Palette -->
    <SolidColorBrush x:Key="AcrylicBackground">#D8E8F5F8</SolidColorBrush>
    <SolidColorBrush x:Key="BorderBrush">#15FFFFFF</SolidColorBrush>

    <!-- Shared Card Style -->
    <Style x:Key="AcrylicCardStyle" TargetType="Border">
        <Setter Property="Background" Value="{StaticResource AcrylicBackground}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1.5"/>
        <Setter Property="CornerRadius" Value="24"/>
        <Setter Property="Effect">
            <Setter.Value>
                <DropShadowEffect Color="#FF1A1C1E"
                                  Opacity="0.08"
                                  BlurRadius="40"
                                  ShadowDepth="8"/>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Shared InputField Style -->
    <Style x:Key="AcrylicInputStyle" TargetType="TextBox">
        <Setter Property="Background" Value="#30000000"/>
        <Setter Property="Foreground" Value="#1A1C1E"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Padding" Value="12,8"/>
        <Setter Property="CornerRadius" Value="6"/>
        <Setter Property="FontFamily" Value="Malgun Gothic, Segoe UI"/>
    </Style>
</ResourceDictionary>
```

---

### **Goal 2: "잠깐 보이고 사라지는" 버그 수정**

#### 근본 원인 분석

```csharp
// ❌ 문제 코드 (현재):
// CharacterScaleContent.Visibility = Visibility.Visible;  // 즉시 보임
// CharacterScaleContent.Opacity = 0;                      // 투명하게 시작
// await AnimateFadeIn();                                  // 애니메이션 시작

// 문제: Visibility=Visible이지만 Opacity=0이면
// 부분적으로 렌더링되거나 레이아웃 계산에 지연 발생
```

#### 해결 방안

**방법 1: Visibility + Opacity 타이밍 조정** ✅ (권장)

```csharp
// ModernModWindow.xaml.cs

private async Task ShowCharacterScaleContent()
{
    if (CharacterScaleContent == null) return;

    // STEP 1: 초기 상태 설정
    CharacterScaleContent.Visibility = Visibility.Visible;
    CharacterScaleContent.Opacity = 0;  // 투명으로 시작

    // STEP 2: UI 스레드에서 레이아웃 강제 계산
    CharacterScaleContent.InvalidateMeasure();
    CharacterScaleContent.InvalidateArrange();
    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

    // STEP 3: 페이드인 애니메이션
    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
    {
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
    };
    CharacterScaleContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);

    System.Diagnostics.Debug.WriteLine("[ModWindow] CharacterScaleContent 페이드인 완료");
}

private async Task HideCharacterScaleContent()
{
    if (CharacterScaleContent == null) return;

    // STEP 1: 페이드아웃 애니메이션
    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
    {
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
    };
    fadeOut.Completed += (s, e) =>
    {
        // STEP 2: 애니메이션 완료 후 숨김
        CharacterScaleContent.Visibility = Visibility.Collapsed;
    };
    CharacterScaleContent.BeginAnimation(UIElement.OpacityProperty, fadeOut);

    System.Diagnostics.Debug.WriteLine("[ModWindow] CharacterScaleContent 페이드아웃 완료");
}
```

**방법 2: PreviewMouseLeftButtonDown 이벤트 등록**

```xaml
<!-- CharacterScaleView.xaml -->
<ListBox ...
         PreviewMouseLeftButtonDown="ListBoxItem_PreviewMouseLeftButtonDown">
</ListBox>
```

```csharp
// CharacterScaleView.xaml.cs

private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    // 선택 즉시 처리
    var listBox = (ListBox)sender;
    listBox.Focus();

    // UI 업데이트 강제
    this.InvalidateMeasure();
    this.InvalidateArrange();

    System.Diagnostics.Debug.WriteLine("[CharacterScaleView] 항목 선택됨");
}
```

**방법 3: XAML Visibility 바인딩 최적화**

```xaml
<!-- ❌ 문제: 직접 바인딩 -->
<Border Visibility="{Binding SelectedScale, Converter={StaticResource NullToVisibility}}">

<!-- ✅ 해결: 지연된 바인딩 -->
<Border x:Name="DetailPanel" Visibility="Collapsed">
    <Border.Visibility>
        <MultiBinding Converter="{StaticResource NullToVisibilityConverter}">
            <Binding Path="SelectedScale"/>
            <Binding Path="IsDetailPanelVisible" RelativeSource="{RelativeSource AncestorType=Window}"/>
        </MultiBinding>
    </Border.Visibility>
</Border>
```

---

## 📋 상세 구현 작업

### **작업 1: SharedResourceDictionary 생성**

**파일**: `Themes/GlassWindowsTheme.xaml`

```xaml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ -->
    <!-- iOS 26 Glassmorphism Color Palette -->
    <!-- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ -->

    <!-- Primary Acrylic Background (Light Blue-Gray) -->
    <SolidColorBrush x:Key="Acrylic_PrimaryBackground">#D8E8F5F8</SolidColorBrush>

    <!-- Border Colors -->
    <SolidColorBrush x:Key="Acrylic_BorderBrush">#15FFFFFF</SolidColorBrush>
    <SolidColorBrush x:Key="Acrylic_BorderHover">#30FFFFFF</SolidColorBrush>
    <SolidColorBrush x:Key="Acrylic_BorderSelected">#50FFFFFF</SolidColorBrush>

    <!-- Text Colors -->
    <SolidColorBrush x:Key="Acrylic_TextPrimary">#1A1C1E</SolidColorBrush>
    <SolidColorBrush x:Key="Acrylic_TextSecondary">#777777</SolidColorBrush>

    <!-- Shadow Colors -->
    <SolidColorBrush x:Key="Acrylic_ShadowColor">#FF1A1C1E</SolidColorBrush>

    <!-- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ -->
    <!-- Shared Styles -->
    <!-- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ -->

    <!-- 1. Acrylic Card Style (for all panels) -->
    <Style x:Key="AcrylicCardStyle" TargetType="Border">
        <Setter Property="Background" Value="{StaticResource Acrylic_PrimaryBackground}"/>
        <Setter Property="BorderBrush" Value="{StaticResource Acrylic_BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1.5"/>
        <Setter Property="CornerRadius" Value="24"/>
        <Setter Property="Effect">
            <Setter.Value>
                <DropShadowEffect Color="{Binding Source={StaticResource Acrylic_ShadowColor}, Path=Color}"
                                  Opacity="0.08"
                                  BlurRadius="40"
                                  ShadowDepth="8"/>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 2. Acrylic TextBox Style -->
    <Style x:Key="AcrylicTextBoxStyle" TargetType="TextBox">
        <Setter Property="Background" Value="#30000000"/>
        <Setter Property="Foreground" Value="{StaticResource Acrylic_TextPrimary}"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Padding" Value="12,8"/>
        <Setter Property="FontFamily" Value="Malgun Gothic, Segoe UI"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="TextBox">
                    <Border Background="{TemplateBinding Background}"
                            CornerRadius="6"
                            Padding="{TemplateBinding Padding}">
                        <ScrollViewer x:Name="PART_ContentHost"/>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 3. Acrylic ListBoxItem Style -->
    <Style x:Key="AcrylicListBoxItemStyle" TargetType="ListBoxItem">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Margin" Value="0,4"/>
        <Setter Property="Padding" Value="12"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ListBoxItem">
                    <Border Background="{TemplateBinding Background}"
                            CornerRadius="16"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter/>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="#40FFFFFF"/>
            </Trigger>
            <Trigger Property="IsSelected" Value="True">
                <Setter Property="Background" Value="#60FFFFFF"/>
            </Trigger>
        </Style.Triggers>
    </Style>

</ResourceDictionary>
```

---

### **작업 2: CharacterScaleView.xaml 완전 재작성**

---

### **작업 3: 버그 수정 로직 (ModernModWindow.xaml.cs)**

```csharp
// ModernModWindow.xaml.cs

private async Task ShowCharacterScaleContent()
{
    if (CharacterScaleContent == null) return;

    System.Diagnostics.Debug.WriteLine("[ModWindow] ShowCharacterScaleContent 시작");

    try
    {
        // === STEP 1: 초기 상태 설정 ===
        CharacterScaleContent.Visibility = Visibility.Visible;
        CharacterScaleContent.Opacity = 0;
        CharacterScaleContent.BeginAnimation(UIElement.OpacityProperty, null);

        // === STEP 2: 레이아웃 강제 계산 (중요!) ===
        CharacterScaleContent.InvalidateMeasure();
        CharacterScaleContent.InvalidateArrange();

        // UI 스레드 렌더링 사이클 완료 대기
        await Dispatcher.InvokeAsync(
            () => { },
            DispatcherPriority.Render);

        // === STEP 3: 페이드인 애니메이션 ===
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        CharacterScaleContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        System.Diagnostics.Debug.WriteLine("[ModWindow] CharacterScaleContent 표시됨 (페이드인)");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[ModWindow] ShowCharacterScaleContent 오류: {ex.Message}");
    }
}

private async Task HideCharacterScaleContent()
{
    if (CharacterScaleContent == null) return;

    System.Diagnostics.Debug.WriteLine("[ModWindow] HideCharacterScaleContent 시작");

    try
    {
        // === STEP 1: 페이드아웃 애니메이션 ===
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        // 애니메이션 완료 콜백
        fadeOut.Completed += (s, e) =>
        {
            CharacterScaleContent.Visibility = Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine("[ModWindow] CharacterScaleContent 숨겨짐");
        };

        // === STEP 2: 애니메이션 시작 ===
        CharacterScaleContent.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[ModWindow] HideCharacterScaleContent 오류: {ex.Message}");
    }
}
```

---

## 📋 YokaiStatsView 업그레이드

유사하게 CharacterScaleView를 기준으로 재작성하되, 다음을 추가:

```xaml
<!-- Tab 전환 시 페이드 애니메이션 -->
<Border x:Name="StatsCard">
    <Border.Visibility>
        <MultiBinding Converter="{StaticResource BoolToVisibilityConverter}">
            <Binding Path="IsStatsTabActive"/>
        </MultiBinding>
    </Border.Visibility>
    <Border.Opacity>0</Border.Opacity>
    <!-- 탭 선택 시 페이드인 -->
</Border>
```

---

## 🎯 체크리스트

### Phase 1: 리소스 생성
- [ ] `Themes/GlassWindowsTheme.xaml` 생성
- [ ] 색상, 스타일 정의
- [ ] 모든 View에서 참조 가능하도록 MergedDictionary 등록

### Phase 2: CharacterScaleView 재작성
- [ ] XAML 완전 재작성
- [ ] 스타일 통일
- [ ] Edge Glow 추가
- [ ] 레이아웃 비율 조정 (32:68)

### Phase 3: YokaiStatsView 업그레이드
- [ ] 유사 방식으로 재작성
- [ ] 탭 전환 애니메이션 추가
- [ ] 스타일 통일

### Phase 4: 버그 수정
- [ ] ModernModWindow.xaml.cs 메서드 추가
- [ ] ShowCharacterScaleContent() 구현
- [ ] HideCharacterScaleContent() 구현
- [ ] 레이아웃 강제 계산 추가

### Phase 5: 통합 테스트
- [ ] 도구 메뉴 진입 시 부드러운 페이드인
- [ ] 목록 항목 선택 시 디테일 패널 표시
- [ ] 돌아갈 때 부드러운 페이드아웃
- [ ] "잠깐 보이고 사라지는" 버그 확인 (해결됨)

---

## ⏰ 예상 작업 시간

| 작업 | 예상 시간 | 난이도 |
|-----|---------|--------|
| 리소스 생성 | 1시간 | ⭐ |
| CharacterScaleView 재작성 | 2-3시간 | ⭐⭐ |
| YokaiStatsView 업그레이드 | 1-2시간 | ⭐⭐ |
| 버그 수정 로직 | 1-2시간 | ⭐⭐ |
| 통합 테스트 | 1시간 | ⭐ |
| **총합** | **6-8시간** | |

---

## 🚀 최종 결과

```
✨ 완성된 UI:
┌─────────────────────────────────────────────┐
│ CharacterInfoV3                              │
├─────────────────────────────────────────────┤
│ ✅ iOS 26 Glassmorphism 스타일              │
│ ✅ Edge Glow 반사광                         │
│ ✅ 부드러운 페이드 애니메이션              │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│ CharacterScaleView (재설계)                 │
├─────────────────────────────────────────────┤
│ ✅ 동일 스타일 적용                         │
│ ✅ 동일 레이아웃 (32:68 비율)             │
│ ✅ Edge Glow 반사광 추가                   │
│ ✅ "잠깐 보이고 사라지는" 버그 수정        │
│ ✅ 부드러운 페이드인/아웃 애니메이션      │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│ YokaiStatsView (업그레이드)                 │
├─────────────────────────────────────────────┤
│ ✅ 동일 스타일 적용                         │
│ ✅ 탭 전환 페이드 애니메이션                │
│ ✅ Edge Glow 반사광 추가                   │
│ ✅ 안정적인 렌더링                         │
└─────────────────────────────────────────────┘

결과: 세 View 모두 **일관된 iOS 26 제어센터 스타일** 완성!
```

