using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ICN_T2.UI.WPF.Effects
{
    /// <summary>
    /// [Phase 5] iOS 26 제어센터 스타일 유리 굴절 효과
    /// HLSL Pixel Shader 3.0 기반 실시간 동적 왜곡
    /// - Perlin-like noise로 자연스러운 유리 느낌
    /// - 마우스 위치 기반 동적 왜곡
    /// - 시간 기반 애니메이션
    /// </summary>
    public class GlassRefractionEffect : ShaderEffect
    {
        #region Dependency Properties

        // === Input Texture ===
        public static readonly DependencyProperty InputProperty =
            RegisterPixelShaderSamplerProperty(
                nameof(Input),
                typeof(GlassRefractionEffect),
                0);

        public System.Windows.Media.Brush Input
        {
            get => (System.Windows.Media.Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        // === Property: Normal Map (Register s1) ===
        // 유리 질감 및 노이즈 텍스처
        public static readonly DependencyProperty NormalMapProperty =
            RegisterPixelShaderSamplerProperty(
                nameof(NormalMap),
                typeof(GlassRefractionEffect),
                1);

        public System.Windows.Media.Brush NormalMap
        {
            get => (System.Windows.Media.Brush)GetValue(NormalMapProperty);
            set => SetValue(NormalMapProperty, value);
        }

        // === Property 1: Refraction Strength ===
        // 왜곡 강도 (0.0 = 없음, 1.0 = 최대)
        public static readonly DependencyProperty RefractionStrengthProperty =
            DependencyProperty.Register(
                nameof(RefractionStrength),
                typeof(double),
                typeof(GlassRefractionEffect),
                new UIPropertyMetadata(0.3, PixelShaderConstantCallback(0)));

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
                new UIPropertyMetadata(5.0, PixelShaderConstantCallback(1)));

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
                new UIPropertyMetadata(0.5, PixelShaderConstantCallback(2)));

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
                new UIPropertyMetadata(0.5, PixelShaderConstantCallback(3)));

        public double MouseY
        {
            get => (double)GetValue(MouseYProperty);
            set => SetValue(MouseYProperty, value);
        }

        // === Property 5: Animation Time ===
        // 애니메이션 타이밍 (0.0 ~ 1.0 루프, 호환 유지)
        public static readonly DependencyProperty AnimationTimeProperty =
            DependencyProperty.Register(
                nameof(AnimationTime),
                typeof(double),
                typeof(GlassRefractionEffect),
                new UIPropertyMetadata(0.0, PixelShaderConstantCallback(4)));

        public double AnimationTime
        {
            get => (double)GetValue(AnimationTimeProperty);
            set => SetValue(AnimationTimeProperty, value);
        }

        // === Property 6: Specular Strength ===
        // 반사광 강도 (0.0 = 없음, 0.1~0.2 = 은은한 빛)
        public static readonly DependencyProperty SpecularStrengthProperty =
            DependencyProperty.Register(
                nameof(SpecularStrength),
                typeof(double),
                typeof(GlassRefractionEffect),
                new UIPropertyMetadata(0.15, PixelShaderConstantCallback(5)));

        public double SpecularStrength
        {
            get => (double)GetValue(SpecularStrengthProperty);
            set => SetValue(SpecularStrengthProperty, value);
        }

        // === Property 7: Inner Shadow Size ===
        // 내부 그림자 크기 (0.0 = 없음, 0.02~0.04 = 얇은 테두리)
        public static readonly DependencyProperty InnerShadowSizeProperty =
            DependencyProperty.Register(
                nameof(InnerShadowSize),
                typeof(double),
                typeof(GlassRefractionEffect),
                new UIPropertyMetadata(0.03, PixelShaderConstantCallback(6)));

        public double InnerShadowSize
        {
            get => (double)GetValue(InnerShadowSizeProperty);
            set => SetValue(InnerShadowSizeProperty, value);
        }

        // === Property 8: Density ===
        // 시각적 밀도 (0.0 = 투명, 1.0 = 불투명)
        public static readonly DependencyProperty DensityProperty =
            DependencyProperty.Register(
                nameof(Density),
                typeof(double),
                typeof(GlassRefractionEffect),
                new UIPropertyMetadata(0.2, PixelShaderConstantCallback(7)));

        public double Density
        {
            get => (double)GetValue(DensityProperty);
            set => SetValue(DensityProperty, value);
        }

        // === Property 9: Mouse Radius ===
        // 마우스 영향 반경 (정규화 좌표 기준)
        public static readonly DependencyProperty MouseRadiusProperty =
            DependencyProperty.Register(
                nameof(MouseRadius),
                typeof(double),
                typeof(GlassRefractionEffect),
                new UIPropertyMetadata(0.4, PixelShaderConstantCallback(8)));

        public double MouseRadius
        {
            get => (double)GetValue(MouseRadiusProperty);
            set => SetValue(MouseRadiusProperty, value);
        }

        // === Property 10: Mouse Falloff Power ===
        // 감쇠 곡선 기울기 (값이 클수록 중심부에 더 집중)
        public static readonly DependencyProperty MouseFalloffPowerProperty =
            DependencyProperty.Register(
                nameof(MouseFalloffPower),
                typeof(double),
                typeof(GlassRefractionEffect),
                new UIPropertyMetadata(1.0, PixelShaderConstantCallback(9)));

        public double MouseFalloffPower
        {
            get => (double)GetValue(MouseFalloffPowerProperty);
            set => SetValue(MouseFalloffPowerProperty, value);
        }

        // === Property 11: Mouse Offset Strength ===
        // 마우스 기여 오프셋 강도 계수
        public static readonly DependencyProperty MouseOffsetStrengthProperty =
            DependencyProperty.Register(
                nameof(MouseOffsetStrength),
                typeof(double),
                typeof(GlassRefractionEffect),
                new UIPropertyMetadata(0.2, PixelShaderConstantCallback(10)));

        public double MouseOffsetStrength
        {
            get => (double)GetValue(MouseOffsetStrengthProperty);
            set => SetValue(MouseOffsetStrengthProperty, value);
        }

        // === Property 12: Edge Highlight Strength ===
        // 외곽 하이라이트 강도
        public static readonly DependencyProperty EdgeHighlightStrengthProperty =
            DependencyProperty.Register(
                nameof(EdgeHighlightStrength),
                typeof(double),
                typeof(GlassRefractionEffect),
                new UIPropertyMetadata(0.12, PixelShaderConstantCallback(11)));

        public double EdgeHighlightStrength
        {
            get => (double)GetValue(EdgeHighlightStrengthProperty);
            set => SetValue(EdgeHighlightStrengthProperty, value);
        }

        #endregion

        #region Constructor & PixelShader

        private static readonly PixelShader _pixelShader;

        static GlassRefractionEffect()
        {
            // Shader 리소스 로드
            _pixelShader = new PixelShader();

            try
            {
                string uri = "pack://application:,,,/ICN_T2;component/UI/WPF/Effects/GlassRefraction.ps";
                _pixelShader.UriSource = new Uri(uri, UriKind.Absolute);
                System.Diagnostics.Debug.WriteLine("[GlassShader] Shader 로드 성공");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlassShader] Shader 로드 실패: {ex.Message}");
            }
        }

        public GlassRefractionEffect()
        {
            try
            {
                PixelShader = _pixelShader;

                UpdateShaderValue(InputProperty);
                UpdateShaderValue(NormalMapProperty);

                PaddingTop = 0;
                PaddingBottom = 0;
                PaddingLeft = 0;
                PaddingRight = 0;

                // 초기값 설정 (모든 셰이더 상수)
                UpdateShaderValue(RefractionStrengthProperty);
                UpdateShaderValue(NoiseScaleProperty);
                UpdateShaderValue(MouseXProperty);
                UpdateShaderValue(MouseYProperty);
                UpdateShaderValue(AnimationTimeProperty);
                UpdateShaderValue(SpecularStrengthProperty);
                UpdateShaderValue(InnerShadowSizeProperty);
                UpdateShaderValue(DensityProperty);
                UpdateShaderValue(MouseRadiusProperty);
                UpdateShaderValue(MouseFalloffPowerProperty);
                UpdateShaderValue(MouseOffsetStrengthProperty);
                UpdateShaderValue(EdgeHighlightStrengthProperty);

                System.Diagnostics.Debug.WriteLine("[GlassShader] Effect 초기화 완료 (Liquid Glass)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlassShader] Effect 초기화 실패: {ex.Message}");
            }
        }

        #endregion
    }

    /// <summary>
    /// 간단한 대안: BlurEffect 래퍼
    /// 실제 굴절 대신 블러로 유리 느낌 시뮬레이션 (Fallback용)
    /// </summary>
    public static class LightweightGlassEffect
    {
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled",
                typeof(bool),
                typeof(LightweightGlassEffect),
                new PropertyMetadata(false, OnEnabledChanged));

        public static bool GetEnabled(DependencyObject obj)
            => (bool)obj.GetValue(EnabledProperty);

        public static void SetEnabled(DependencyObject obj, bool value)
            => obj.SetValue(EnabledProperty, value);

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue)
                {
                    // Apply subtle blur to simulate glass refraction
                    var blur = new BlurEffect
                    {
                        Radius = 1.5,
                        KernelType = KernelType.Gaussian,
                        RenderingBias = RenderingBias.Quality
                    };
                    element.Effect = blur;
                }
                else
                {
                    element.Effect = null;
                }
            }
        }
    }
}
