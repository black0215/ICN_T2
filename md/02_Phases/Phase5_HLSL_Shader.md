# 🚀 Phase 5: HLSL 기반 유리 굴절 효과 (Refraction Shader)

> **최종 목표**: iOS 26 제어센터의 **실제 유리처럼 왜곡되는 배경** 구현
>
> 마우스 위치에 따라 동적으로 변하는 고급 Shader 효과를 적용하여
> **프리미엄급 Glassmorphism UI** 완성

---

## 🎯 Phase 5 비전

### 현재 상태 (Phase 1-4)
```
✅ iOS 제어센터 컬러 팔레트
✅ 테두리 Edge Glow 반사광
✅ Spring 애니메이션 (버튼 진입)
✅ 배경 확장 로직 (위쪽만)
✅ Acrylic 배경 색상

❌ 실제 "유리 굴절" 효과
```

### Phase 5 완료 후 (최종)
```
✨ HLSL Pixel Shader로 실시간 굴절 변형
✨ 마우스 위치 기반 동적 왜곡
✨ Perlin Noise 기반 자연스러운 파도 효과
✨ iOS Safari처럼 배경이 흐리면서 동시에 왜곡됨
✨ 60 FPS 고성능 유지
```

---

## 📊 기술 아키텍처

### 1️⃣ **HLSL Shader 구조 (Pixel Shader 3.0)**

#### 파일 구조
```
ICN_T2/UI/WPF/Effects/
├── GlassRefractionEffect.cs      ← WPF Wrapper
├── GlassRefraction.fx             ← HLSL 소스 (새로 작성)
├── GlassRefraction.ps             ← 컴파일된 바이너리
└── ShaderResources.xaml           ← 리소스 등록
```

#### HLSL Shader 스펙
```hlsl
// Platform: DirectX 9 Shader Model 3.0
// Register 요구사항:
//   s0: Input Texture (배경 이미지)
//   c0: RefractionStrength (0.0 ~ 1.0)
//   c1: NoiseScale (0.0 ~ 10.0)
//   c2: MouseX, MouseY (정규화된 좌표)
//   c3: DeltaTime (애니메이션 타이밍)

float4 main(float2 uv : TEXCOORD0) : COLOR
{
    // 1. Noise 함수로 Perlin-like 노이즈 생성
    // 2. 마우스 위치에 따라 노이즈 중심 이동
    // 3. 픽셀 좌표 오프셋 계산
    // 4. 원본 텍스처 샘플링 (왜곡된 좌표로)
    // 5. 최종 색상 반환
}
```

### 2️⃣ **WPF 통합 계층**

#### GlassRefractionEffect.cs 역할
```csharp
public class GlassRefractionEffect : Effect
{
    // Dependency Properties:
    // - RefractionStrength (강도: 0.0 ~ 1.0)
    // - NoiseScale (스케일: 1.0 ~ 10.0)
    // - AnimationIntensity (시간 기반 애니메이션)

    // 이들이 Shader의 Constants에 자동 매핑됨
}
```

#### ModernModWindow.cs 통합
```csharp
// CharacterInfoV3 배경에 효과 적용
CharacterInfoContent.Effect = new GlassRefractionEffect
{
    RefractionStrength = 0.3,
    NoiseScale = 5.0,
    AnimationIntensity = 1.0
};

// 마우스 움직임에 따라 동적 업데이트
window.MouseMove += (s, e) =>
{
    var effect = CharacterInfoContent.Effect as GlassRefractionEffect;
    if (effect != null)
    {
        // Mouse좌표를 정규화해서 Shader에 전달
        effect.MouseX = (float)e.GetPosition(window).X / window.ActualWidth;
        effect.MouseY = (float)e.GetPosition(window).Y / window.ActualHeight;
    }
};
```

---

## 🎨 HLSL Shader 상세 구현

### Shader 핵심 알고리즘

#### 1️⃣ **Perlin-Like Noise 함수**

```hlsl
// 간단한 Pseudo-random 함수
float noise(float2 p)
{
    // sin/cos 기반 해시 함수 (Perlin Noise 유사)
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

// Smoothstep으로 부드러운 보간
float smoothnoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);

    // Hermite 보간
    f = f * f * (3.0 - 2.0 * f);

    float n00 = noise(i);
    float n10 = noise(i + float2(1.0, 0.0));
    float n01 = noise(i + float2(0.0, 1.0));
    float n11 = noise(i + float2(1.0, 1.0));

    float nx0 = lerp(n00, n10, f.x);
    float nx1 = lerp(n01, n11, f.x);
    return lerp(nx0, nx1, f.y);
}
```

#### 2️⃣ **마우스 위치 기반 동적 왜곡**

```hlsl
float4 main(float2 uv : TEXCOORD0) : COLOR
{
    // 입력값
    float strength = refractionStrength;  // 0.0 ~ 1.0
    float noiseScale = noiseScaleParam;   // 1.0 ~ 10.0
    float2 mouse = float2(mouseX, mouseY); // 정규화된 좌표
    float time = deltaTime;               // 시간 (0~1 루프)

    // === STEP 1: Noise 생성 ===
    // 기본 노이즈: 마우스 중심으로 방사형
    float2 noiseCoord = (uv - mouse) * noiseScale;
    float noise1 = smoothnoise(noiseCoord);

    // 애니메이션 노이즈: 시간에 따라 흐르는 효과
    float noise2 = smoothnoise(uv * 3.0 + time * 0.5);

    // 합성 노이즈
    float combined = noise1 * 0.7 + noise2 * 0.3;
    combined = (combined - 0.5) * 2.0; // -1.0 ~ 1.0 범위로 정규화

    // === STEP 2: 왜곡 벡터 계산 ===
    // 거리에 따른 감쇠 (중심에서 멀수록 약함)
    float distance = length(uv - mouse);
    float falloff = 1.0 - smoothstep(0.0, 0.8, distance);

    // 최종 오프셋
    float2 offset = normalize(uv - mouse) * combined * strength * falloff * 0.02;

    // === STEP 3: 텍스처 샘플링 ===
    // 원본 좌표를 오프셋으로 왜곡
    float2 distortedUv = uv + offset;

    // 경계 처리 (클램핑)
    distortedUv = clamp(distortedUv, 0.0, 1.0);

    // === STEP 4: 색상 샘플링 ===
    float4 color = tex2D(input, distortedUv);

    // === STEP 5: 에지 처리 (선택사항) ===
    // 경계 근처에서 알파 감소 (부자연스러운 끝 숨기기)
    float edgeAlpha = smoothstep(0.0, 0.05, distortedUv.x) *
                      smoothstep(1.0, 0.95, distortedUv.x) *
                      smoothstep(0.0, 0.05, distortedUv.y) *
                      smoothstep(1.0, 0.95, distortedUv.y);

    color.a *= edgeAlpha;

    return color;
}
```

#### 3️⃣ **성능 최적화 버전** (대안)

```hlsl
// 더 간단하지만 빠른 버전
float4 main(float2 uv : TEXCOORD0) : COLOR
{
    // Noise 대신 간단한 sin/cos 파도
    float wave1 = sin((uv.y - mouseY) * 10.0 + time) * 0.01;
    float wave2 = cos((uv.x - mouseX) * 10.0 + time) * 0.01;

    // 거리에 따른 감쇠
    float2 dist = uv - float2(mouseX, mouseY);
    float falloff = 1.0 - length(dist) * 2.0;
    falloff = max(0.0, falloff);

    // 오프셋 적용
    float2 offset = float2(wave1, wave2) * falloff * refractionStrength;
    float2 distortedUv = clamp(uv + offset, 0.0, 1.0);

    return tex2D(input, distortedUv);
}
```

---

## 🛠️ 구현 단계별 가이드

### **Step 1: HLSL 파일 작성**

#### 파일: `GlassRefraction.fx`

```hlsl
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Glass Refraction Shader for WPF
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Platform: DirectX 9 / Shader Model 3.0
// Input: Render target texture (배경)
// Output: Refracted color with glass-like distortion
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

// === INPUT TEXTURE ===
sampler2D inputSampler : register(s0);

// === SHADER CONSTANTS (from WPF) ===
// Register c0
float refractionStrength : register(c0);

// Register c1
float noiseScale : register(c1);

// Register c2: float2 mousePos
// Register c3: float time (animation timer)

float mouseX : register(c2x);
float mouseY : register(c2y);
float time : register(c3);

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// UTILITY FUNCTIONS
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

// Pseudo-random hash function (for Perlin-like noise)
float rand(float2 n)
{
    return frac(sin(dot(n, float2(12.9898, 78.233))) * 43758.5453);
}

// Interpolation function (smoothstep)
float smoothstep_custom(float edge0, float edge1, float x)
{
    float t = clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}

// Smooth 2D noise (Perlin-like)
float simplex_noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);

    // Hermite interpolation
    float2 u = f * f * (3.0 - 2.0 * f);

    float n00 = rand(i);
    float n10 = rand(i + float2(1.0, 0.0));
    float n01 = rand(i + float2(0.0, 1.0));
    float n11 = rand(i + float2(1.0, 1.0));

    float nx0 = lerp(n00, n10, u.x);
    float nx1 = lerp(n01, n11, u.x);
    return lerp(nx0, nx1, u.y);
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// MAIN PIXEL SHADER
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

float4 main(float2 uv : TEXCOORD0) : COLOR
{
    // === CONFIGURE ===
    float2 mouse = float2(mouseX, mouseY);
    float strength = refractionStrength * 0.5;  // Scale down for stability
    float scale = noiseScale;

    // === NOISE GENERATION ===
    // Radial noise from mouse position
    float2 relPos = (uv - mouse) * scale;
    float noiseMouse = simplex_noise(relPos);

    // Time-based animated noise
    float2 animCoord = uv * 3.0 + time * 0.2;
    float noiseAnim = simplex_noise(animCoord);

    // Combine noises (weighted)
    float noise = noiseMouse * 0.6 + noiseAnim * 0.4;

    // Remap to -1.0 ~ 1.0 range
    noise = (noise - 0.5) * 2.0;

    // === DISTANCE FALLOFF ===
    // Distortion gets weaker far from mouse
    float dist = length(uv - mouse);
    float falloff = 1.0 - smoothstep_custom(0.0, 0.7, dist);

    // === OFFSET CALCULATION ===
    // Direction from mouse
    float2 direction = normalize(uv - mouse + 0.001);  // +0.001 to avoid division by zero

    // Final offset vector
    float2 offset = direction * noise * strength * falloff * 0.03;

    // === DISTORTION ===
    // Apply offset to UV coordinates
    float2 distortedUv = uv + offset;

    // Clamp to valid texture coordinates
    distortedUv = clamp(distortedUv, 0.01, 0.99);

    // === SAMPLE TEXTURE ===
    float4 color = tex2D(inputSampler, distortedUv);

    // === EDGE FADE (optional) ===
    // Smooth alpha at edges to hide distortion artifacts
    float edgeAlpha = smoothstep_custom(0.0, 0.05, distortedUv.x) *
                      smoothstep_custom(1.0, 0.95, distortedUv.x) *
                      smoothstep_custom(0.0, 0.05, distortedUv.y) *
                      smoothstep_custom(1.0, 0.95, distortedUv.y);

    // Reduce alpha at edges for smooth blending
    color.a *= edgeAlpha;

    return color;
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// COMPILE COMMAND (using FXC from Windows SDK)
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// fxc /T ps_3_0 /E main /Fo GlassRefraction.ps GlassRefraction.fx
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

### **Step 2: Shader 컴파일**

#### 빌드 도구: FXC (Visual Studio DirectX SDK)

```powershell
# PowerShell에서 실행

# Windows SDK fxc.exe 위치
$fxcPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\fxc.exe"

# 컴파일 명령어
& $fxcPath /T ps_3_0 /E main /Fo "C:\Users\home\Desktop\ICN_T2\ICN_T2\UI\WPF\Effects\GlassRefraction.ps" "C:\Users\home\Desktop\ICN_T2\ICN_T2\UI\WPF\Effects\GlassRefraction.fx"

# 성공 확인
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Shader compiled successfully" -ForegroundColor Green
} else {
    Write-Host "✗ Compilation failed" -ForegroundColor Red
}
```

#### 또는 Build Event로 자동 컴파일

**프로젝트 파일 (.csproj) 수정**:

```xml
<!-- ICN_T2.csproj 에 추가 -->
<Target Name="CompileShaders" BeforeTargets="Build">
    <Exec Command="fxc /T ps_3_0 /E main /Fo &quot;$(ProjectDir)UI\WPF\Effects\GlassRefraction.ps&quot; &quot;$(ProjectDir)UI\WPF\Effects\GlassRefraction.fx&quot;" />
</Target>
```

---

### **Step 3: GlassRefractionEffect.cs 완성**

```csharp
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ICN_T2.UI.WPF.Effects
{
    /// <summary>
    /// iOS 26 제어센터 스타일 유리 굴절 효과
    /// HLSL Pixel Shader 3.0 기반 실시간 동적 왜곡
    /// </summary>
    public class GlassRefractionEffect : Effect
    {
        #region Dependency Properties

        // === Property 1: Refraction Strength ===
        // 왜곡 강도 (0.0 = 보이지 않음, 1.0 = 최대)
        public static readonly DependencyProperty RefractionStrengthProperty =
            DependencyProperty.Register(
                nameof(RefractionStrength),
                typeof(double),
                typeof(GlassRefractionEffect),
                new PropertyMetadata(0.3, PixelShaderConstantCallback(0)));

        public double RefractionStrength
        {
            get => (double)GetValue(RefractionStrengthProperty);
            set => SetValue(RefractionStrengthProperty, value);
        }

        // === Property 2: Noise Scale ===
        // 노이즈 스케일 (1.0 = 작은 파동, 10.0 = 큰 파동)
        public static readonly DependencyProperty NoiseScaleProperty =
            DependencyProperty.Register(
                nameof(NoiseScale),
                typeof(double),
                typeof(GlassRefractionEffect),
                new PropertyMetadata(5.0, PixelShaderConstantCallback(1)));

        public double NoiseScale
        {
            get => (double)GetValue(NoiseScaleProperty);
            set => SetValue(NoiseScaleProperty, value);
        }

        // === Property 3: Mouse X (정규화된 좌표) ===
        public static readonly DependencyProperty MouseXProperty =
            DependencyProperty.Register(
                nameof(MouseX),
                typeof(double),
                typeof(GlassRefractionEffect),
                new PropertyMetadata(0.5, PixelShaderConstantCallback(2)));

        public double MouseX
        {
            get => (double)GetValue(MouseXProperty);
            set => SetValue(MouseXProperty, value);
        }

        // === Property 4: Mouse Y (정규화된 좌표) ===
        public static readonly DependencyProperty MouseYProperty =
            DependencyProperty.Register(
                nameof(MouseY),
                typeof(double),
                typeof(GlassRefractionEffect),
                new PropertyMetadata(0.5, PixelShaderConstantCallback(2)));

        public double MouseY
        {
            get => (double)GetValue(MouseYProperty);
            set => SetValue(MouseYProperty, value);
        }

        // === Property 5: Animation Time ===
        // 애니메이션 타이밍 (0.0 ~ 1.0 루프)
        public static readonly DependencyProperty AnimationTimeProperty =
            DependencyProperty.Register(
                nameof(AnimationTime),
                typeof(double),
                typeof(GlassRefractionEffect),
                new PropertyMetadata(0.0, PixelShaderConstantCallback(3)));

        public double AnimationTime
        {
            get => (double)GetValue(AnimationTimeProperty);
            set => SetValue(AnimationTimeProperty, value);
        }

        #endregion

        #region Constructor & PixelShader

        private static readonly PixelShader _pixelShader;

        static GlassRefractionEffect()
        {
            // Shader 리소스 로드
            // pack://application:,,,/ICN_T2;component/UI/WPF/Effects/GlassRefraction.ps
            string uri = "pack://application:,,,/ICN_T2;component/UI/WPF/Effects/GlassRefraction.ps";
            _pixelShader = new PixelShader();

            try
            {
                _pixelShader.SetStreamSource(new Uri(uri, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Shader] 로드 실패: {ex.Message}");
                // Fallback: 효과 비활성화
            }
        }

        public GlassRefractionEffect()
        {
            try
            {
                PixelShader = _pixelShader;
                UpdateShaderValue(RefractionStrengthProperty);
                UpdateShaderValue(NoiseScaleProperty);
                UpdateShaderValue(MouseXProperty);
                UpdateShaderValue(MouseYProperty);
                UpdateShaderValue(AnimationTimeProperty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Shader] 초기화 실패: {ex.Message}");
            }
        }

        #endregion

        #region Protected Methods

        protected override Effect DeepCopy()
        {
            return new GlassRefractionEffect
            {
                RefractionStrength = RefractionStrength,
                NoiseScale = NoiseScale,
                MouseX = MouseX,
                MouseY = MouseY,
                AnimationTime = AnimationTime
            };
        }

        #endregion
    }
}
```

---

### **Step 4: ModernModWindow.cs에 통합**

```csharp
// ModernModWindow.xaml.cs

// === 1. Shader Effect 선언 ===
private GlassRefractionEffect _glassRefractionEffect;
private System.Diagnostics.Stopwatch _shaderTimer;

// === 2. Window 초기화 ===
private void Window_Loaded(object sender, RoutedEventArgs e)
{
    // Shader 초기화
    _glassRefractionEffect = new GlassRefractionEffect
    {
        RefractionStrength = 0.3,   // 중간 강도
        NoiseScale = 5.0,           // 중간 스케일
    };

    // CharacterInfo에 적용
    if (CharacterInfoContent != null)
    {
        CharacterInfoContent.Effect = _glassRefractionEffect;
    }

    // 애니메이션 타이머 시작
    _shaderTimer = System.Diagnostics.Stopwatch.StartNew();

    // 마우스 이벤트 등록
    this.MouseMove += Window_MouseMove;

    // 렌더링 루프 (60 FPS)
    var timer = new DispatcherTimer();
    timer.Interval = TimeSpan.FromMilliseconds(16.67); // ~60 FPS
    timer.Tick += UpdateShaderAnimation;
    timer.Start();
}

// === 3. 마우스 추적 ===
private void Window_MouseMove(object sender, MouseEventArgs e)
{
    if (_glassRefractionEffect == null) return;

    // 정규화된 좌표 (0.0 ~ 1.0)
    double normalizedX = e.GetPosition(this).X / this.ActualWidth;
    double normalizedY = e.GetPosition(this).Y / this.ActualHeight;

    // Shader에 전달
    _glassRefractionEffect.MouseX = normalizedX;
    _glassRefractionEffect.MouseY = normalizedY;

    System.Diagnostics.Debug.WriteLine(
        $"[Shader] Mouse: ({normalizedX:F2}, {normalizedY:F2})");
}

// === 4. 애니메이션 업데이트 (렌더링 루프) ===
private void UpdateShaderAnimation(object sender, EventArgs e)
{
    if (_glassRefractionEffect == null || _shaderTimer == null) return;

    // 시간 기반 애니메이션 (0.0 ~ 1.0 루프, 4초 주기)
    double totalSeconds = _shaderTimer.Elapsed.TotalSeconds;
    double animationTime = (totalSeconds % 4.0) / 4.0; // 4초 루프

    _glassRefractionEffect.AnimationTime = animationTime;
}

// === 5. 도구 메뉴 진입 시 효과 제어 ===
private async Task TransitionToToolWindow(Button btn)
{
    // ... 기존 코드 ...

    // 도구 메뉴 진입 시 Shader 강도 증가
    if (_glassRefractionEffect != null)
    {
        // Animated intensity change
        for (int i = 0; i <= 10; i++)
        {
            _glassRefractionEffect.RefractionStrength = 0.3 + (i * 0.05);
            await Task.Delay(30);
        }
    }
}

// === 6. Cleanup ===
private void Window_Unloaded(object sender, RoutedEventArgs e)
{
    _shaderTimer?.Stop();
    _glassRefractionEffect = null;
}
```

---

## 🎯 성능 최적화 전략

### 1️⃣ **Shader 복잡도 선택**

| 버전 | 특징 | FPS | 복잡도 |
|-----|------|-----|--------|
| **Full (Perlin)** | Perlin-like noise, 부드러운 왜곡 | 50-60 | ⭐⭐⭐⭐⭐ |
| **Optimized** | 단순 sin/cos 파도 | 55-60 | ⭐⭐⭐ |
| **Lite** | 기본 blur + offset | 60+ | ⭐⭐ |

**추천**: **Optimized** (성능과 품질의 균형)

### 2️⃣ **렌더링 최적화**

```csharp
// A. Effect 적용 범위 제한
if (CharacterInfoContent != null)
{
    // 도구 메뉴에서만 Shader 활성화
    CharacterInfoContent.Effect = isToolMode ? _glassRefractionEffect : null;
}

// B. Shader 강도 조정
// 마우스가 카드 위에 있을 때만 강도 증가
if (IsMouseOverCard())
{
    _glassRefractionEffect.RefractionStrength = 0.5;  // 높음
}
else
{
    _glassRefractionEffect.RefractionStrength = 0.1;  // 낮음 (거의 보이지 않음)
}

// C. 업데이트 빈도 조절
// 마우스가 움직일 때만 업데이트
private Point _lastMousePos;

private void Window_MouseMove(object sender, MouseEventArgs e)
{
    var currentPos = e.GetPosition(this);

    // 일정 거리 이상 이동했을 때만 업데이트
    if (Math.Abs(currentPos.X - _lastMousePos.X) > 5 ||
        Math.Abs(currentPos.Y - _lastMousePos.Y) > 5)
    {
        UpdateShaderValues(currentPos);
        _lastMousePos = currentPos;
    }
}
```

### 3️⃣ **메모리 최적화**

```csharp
// Shader 리소스 재사용
private static GlassRefractionEffect _sharedEffect;

public static GlassRefractionEffect GetSharedEffect()
{
    if (_sharedEffect == null)
    {
        _sharedEffect = new GlassRefractionEffect();
    }
    return _sharedEffect;
}

// 여러 요소에 적용
CharacterInfoContent.Effect = GetSharedEffect();
CharacterScaleContent.Effect = GetSharedEffect();
YokaiStatsContent.Effect = GetSharedEffect();
```

---

## 📋 구현 체크리스트

### **Phase 5-1: Shader 작성 & 컴파일** (3-4시간)
- [ ] `GlassRefraction.fx` 파일 작성
- [ ] FXC로 컴파일하여 `.ps` 파일 생성
- [ ] 파일을 프로젝트에 추가
- [ ] 빌드 이벤트 설정 (자동 컴파일)

### **Phase 5-2: WPF Effect Wrapper** (1-2시간)
- [ ] `GlassRefractionEffect.cs` 구현
- [ ] Dependency Property 정의 (5개)
- [ ] PixelShader 로드 (pack:// URI)
- [ ] DeepCopy 메서드 구현

### **Phase 5-3: ModernModWindow 통합** (1-2시간)
- [ ] Effect 인스턴스 생성
- [ ] MouseMove 이벤트 추가
- [ ] DispatcherTimer로 애니메이션 루프
- [ ] CharacterInfoContent에 적용

### **Phase 5-4: 성능 테스트 & 최적화** (1-2시간)
- [ ] 60 FPS 확보 확인
- [ ] 메모리 누수 테스트
- [ ] Shader 강도 튜닝
- [ ] 다양한 해상도에서 테스트

### **Phase 5-5: 시각적 품질 조정** (1시간)
- [ ] 왜곡 정도 (RefractionStrength) 조정
- [ ] 노이즈 스케일 (NoiseScale) 조정
- [ ] 마우스 영향 범위 조정
- [ ] 애니메이션 속도 조정

### **Phase 5-6: 최종 통합 테스트** (1-2시간)
- [ ] Phase 2-4와의 호환성
- [ ] Edge Glow와 Shader 동시 동작
- [ ] Spring 애니메이션 + Shader
- [ ] 도구 메뉴 진입/복귀 시 효과 제어

---

## 🎬 예상 결과

### 도구 메뉴 진입 시
```
1. 버튼이 Spring 애니메이션으로 나타남 (Phase 2)
2. 배경이 위쪽으로 80px 확장 (Phase 3)
3. CharacterInfoContent에 Shader 효과 활성화
4. 마우스 움직임에 따라 실시간 유리 왜곡 효과
5. 테두리에 Edge Glow 반사광 (Phase 2)
6. Acrylic 배경 색상과 조화됨 (Phase 1)

결과: iOS 26 제어센터처럼 프리미엄한 Glassmorphism UI!
```

---

## ⚠️ 주의사항 & 트러블슈팅

### 1️⃣ **Shader 로드 실패**
```csharp
// 문제: "pack://application" URI 인식 안 됨
// 해결:
// 1. .ps 파일이 프로젝트에 포함되어 있는가?
// 2. Build Action = "Resource"로 설정했는가?
// 3. URI 경로가 정확한가?

// 디버그
System.Diagnostics.Debug.WriteLine($"Shader loaded: {_pixelShader != null}");
```

### 2️⃣ **성능 저하 (FPS 드롭)**
```csharp
// 문제: Shader 계산이 너무 복잡해서 FPS 드롭
// 해결:
// 1. RefractionStrength 감소 (0.3 → 0.1)
// 2. NoiseScale 감소 (5.0 → 2.0)
// 3. Shader 업데이트 빈도 감소
// 4. 더 간단한 Shader 버전으로 변경

// 성능 모니터링
private void MonitorFrameRate()
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    // 렌더링 코드
    sw.Stop();

    double fps = 1000.0 / sw.ElapsedMilliseconds;
    System.Diagnostics.Debug.WriteLine($"FPS: {fps:F1}");
}
```

### 3️⃣ **Shader 컴파일 오류**
```powershell
# 문제: fxc.exe를 찾을 수 없음
# 해결: Windows SDK 설치 확인

# Windows SDK 위치 찾기
Get-ChildItem "C:\Program Files*" -Filter "fxc.exe" -Recurse

# 또는 Visual Studio 내장 도구
"C:\Program Files (x86)\Microsoft Visual Studio\2022\Community\Common7\Tools\fxc.exe"
```

---

## 🔗 최종 파일 구조

```
ICN_T2/
├── UI/WPF/
│   ├── Effects/
│   │   ├── GlassRefractionEffect.cs      ← WPF Wrapper (수정)
│   │   ├── GlassRefraction.fx            ← HLSL 소스 (새로 작성)
│   │   └── GlassRefraction.ps            ← 컴파일된 바이너리
│   ├── ModernModWindow.xaml.cs           ← 통합 로직 추가
│   ├── Animations/
│   │   └── AnimationConfig.cs            ← (변경 없음)
│   └── Behaviors/
│       └── EdgeGlowBehavior.cs           ← (변경 없음)
├── ICN_T2.csproj                         ← Build Event 추가
```

---

## ⏰ 예상 총 작업 시간

| 단계 | 작업 | 예상 시간 |
|-----|------|---------|
| 5-1 | Shader 작성 & 컴파일 | 3-4시간 |
| 5-2 | WPF Wrapper | 1-2시간 |
| 5-3 | ModernModWindow 통합 | 1-2시간 |
| 5-4 | 성능 최적화 | 1-2시간 |
| 5-5 | 시각적 품질 조정 | 1시간 |
| 5-6 | 최종 테스트 | 1-2시간 |
| **합계** | | **8-13시간** |

---

## 🚀 다음 단계 (선택사항)

### Phase 6 (나중에)
- DirectComposition API 직접 활용 (더 고성능)
- Raytracing 기반 고급 광학 효과
- 다중 Shader 레이어 조합
- 머신러닝 기반 적응형 효과

---

## 📚 참고 자료

### HLSL 학습
- [Microsoft HLSL Documentation](https://docs.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl)
- [Shader Model 3.0 Reference](https://docs.microsoft.com/en-us/windows/win32/direct3d9/dx9-graphics-reference-effects)
- [WPF Shader Effects](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/effect)

### Noise Algorithms
- [Perlin Noise](https://en.wikipedia.org/wiki/Perlin_noise)
- [Simplex Noise](https://en.wikipedia.org/wiki/Simplex_noise)
- [Worley Noise](https://en.wikipedia.org/wiki/Worley_noise)

### Glassmorphism Design
- [iOS Human Interface Guidelines](https://developer.apple.com/design/human-interface-guidelines/)
- [Windows 11 Design](https://www.microsoft.com/design/fluent/)

---

## 🎯 최종 비전

**완성된 ICN_T2 UI**:
```
┌─────────────────────────────────────────┐
│  📱 iOS 26 제어센터 느낌                 │
│                                         │
│  ✨ Shader 기반 유리 왜곡               │
│  ✨ 마우스 위치 기반 동적 효과          │
│  ✨ Edge Glow 반사광                    │
│  ✨ Spring 애니메이션 (버튼)           │
│  ✨ Acrylic 배경 색상                   │
│  ✨ 60 FPS 고성능                       │
│                                         │
│  → 프리미엄급 Glassmorphism UI ✓       │
└─────────────────────────────────────────┘
```

**이것이 진정한 "iOS 26 제어센터 스타일"의 완성입니다!** 🎉

