using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace ICN_T2.UI.WPF.Behaviors
{
    /// <summary>
    /// Attached Behavior: 마우스 위치에 따라 Border의 테두리에 Edge Glow 효과 적용
    /// iOS 26 제어센터 스타일의 Liquid Glass Edge Shine 구현
    /// </summary>
    public static class EdgeGlowBehavior
    {
        #region Attached Property: IsEnabled

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(EdgeGlowBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj)
            => (bool)obj.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject obj, bool value)
            => obj.SetValue(IsEnabledProperty, value);

        #endregion

        #region Attached Property: GlowIntensity

        public static readonly DependencyProperty GlowIntensityProperty =
            DependencyProperty.RegisterAttached(
                "GlowIntensity",
                typeof(double),
                typeof(EdgeGlowBehavior),
                new PropertyMetadata(0.3));

        public static double GetGlowIntensity(DependencyObject obj)
            => (double)obj.GetValue(GlowIntensityProperty);

        public static void SetGlowIntensity(DependencyObject obj, double value)
            => obj.SetValue(GlowIntensityProperty, value);

        #endregion

        #region Attached Property: GlowWidth

        public static readonly DependencyProperty GlowWidthProperty =
            DependencyProperty.RegisterAttached(
                "GlowWidth",
                typeof(double),
                typeof(EdgeGlowBehavior),
                new PropertyMetadata(80.0));

        public static double GetGlowWidth(DependencyObject obj)
            => (double)obj.GetValue(GlowWidthProperty);

        public static void SetGlowWidth(DependencyObject obj, double value)
            => obj.SetValue(GlowWidthProperty, value);

        #endregion

        #region Implementation

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Border border)
            {
                if ((bool)e.NewValue)
                {
                    // Enable behavior
                    border.Loaded += OnBorderLoaded;
                    border.Unloaded += OnBorderUnloaded;
                }
                else
                {
                    // Disable behavior
                    border.Loaded -= OnBorderLoaded;
                    border.Unloaded -= OnBorderUnloaded;
                    UnregisterMouseTracking(border);
                }
            }
        }

        private static void OnBorderLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Border border)
            {
                RegisterMouseTracking(border);
                InitializeEdgeGlow(border);
            }
        }

        private static void OnBorderUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is Border border)
            {
                UnregisterMouseTracking(border);
            }
        }

        private static void RegisterMouseTracking(Border border)
        {
            // Find root window
            Window window = Window.GetWindow(border);
            if (window != null)
            {
                System.Windows.Input.MouseEventHandler handler = (s, e) => OnWindowMouseMove(border, e);
                SetMouseMoveHandler(border, handler);
                window.MouseMove += handler;
            }
        }

        private static void UnregisterMouseTracking(Border border)
        {
            Window window = Window.GetWindow(border);
            var handler = GetMouseMoveHandler(border);
            if (window != null && handler != null)
            {
                window.MouseMove -= handler;
            }
            // Clear the gradient
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
        }

        // Helper attached property to store the handler
        private static readonly DependencyProperty MouseMoveHandlerProperty =
            DependencyProperty.RegisterAttached("MouseMoveHandler", typeof(System.Windows.Input.MouseEventHandler), typeof(EdgeGlowBehavior), new PropertyMetadata(null));

        private static void SetMouseMoveHandler(DependencyObject element, System.Windows.Input.MouseEventHandler value)
            => element.SetValue(MouseMoveHandlerProperty, value);

        private static System.Windows.Input.MouseEventHandler GetMouseMoveHandler(DependencyObject element)
            => (System.Windows.Input.MouseEventHandler)element.GetValue(MouseMoveHandlerProperty);

        private static void InitializeEdgeGlow(Border border)
        {
            // Initialize with a default gradient
            var gradient = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0.5),
                EndPoint = new System.Windows.Point(1, 0.5)
            };

            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.0));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF), 0.4));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF), 0.5));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF), 0.6));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1.0));

            border.BorderBrush = gradient;
        }

        private static void OnWindowMouseMove(Border border, System.Windows.Input.MouseEventArgs e)
        {
            if (!GetIsEnabled(border))
                return;

            try
            {
                // Get mouse position relative to the border
                System.Windows.Point mousePos = e.GetPosition(border);
                double width = border.ActualWidth;
                double height = border.ActualHeight;

                // Ignore if border has no size yet
                if (width <= 0 || height <= 0)
                    return;

                // Calculate normalized position (0 to 1)
                double normalizedX = Math.Max(0, Math.Min(1, mousePos.X / width));
                double normalizedY = Math.Max(0, Math.Min(1, mousePos.Y / height));

                // Determine which edge is closest
                double distToLeft = normalizedX;
                double distToRight = 1 - normalizedX;
                double distToTop = normalizedY;
                double distToBottom = 1 - normalizedY;
                double minDist = Math.Min(Math.Min(distToLeft, distToRight), Math.Min(distToTop, distToBottom));

                // Only apply glow if mouse is near an edge (within 25% of width/height)
                if (minDist > 0.25)
                {
                    // Reset to default subtle border
                    if (!(border.BorderBrush is SolidColorBrush))
                    {
                        border.BorderBrush = new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
                    }
                    // Reset Effect
                    if (border.Effect is System.Windows.Media.Effects.DropShadowEffect)
                    {
                        border.Effect = null;
                    }
                    return;
                }

                double glowIntensity = GetGlowIntensity(border);
                double glowWidth = GetGlowWidth(border) / Math.Max(width, height);

                // Apply DropShadowEffect for extra glow
                var shadow = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.White,
                    BlurRadius = 25 * glowIntensity, // Softer glow
                    ShadowDepth = 0,
                    Opacity = glowIntensity * 0.6,   // More subtle
                    Direction = 0,
                    RenderingBias = System.Windows.Media.Effects.RenderingBias.Quality
                };
                border.Effect = shadow;

                // Create gradient based on closest edge
                LinearGradientBrush gradient;

                if (distToLeft == minDist || distToRight == minDist)
                {
                    // Horizontal gradient (left/right edge)
                    gradient = new LinearGradientBrush
                    {
                        StartPoint = new System.Windows.Point(0, 0.5),
                        EndPoint = new System.Windows.Point(1, 0.5)
                    };

                    double center = normalizedX;
                    CreateGradientStops(gradient, center, glowWidth, glowIntensity);
                }
                else
                {
                    // Vertical gradient (top/bottom edge)
                    gradient = new LinearGradientBrush
                    {
                        StartPoint = new System.Windows.Point(0.5, 0),
                        EndPoint = new System.Windows.Point(0.5, 1)
                    };

                    double center = normalizedY;
                    CreateGradientStops(gradient, center, glowWidth, glowIntensity);
                }

                border.BorderBrush = gradient;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EdgeGlow] Error: {ex.Message}");
            }
        }

        private static void CreateGradientStops(LinearGradientBrush gradient, double center, double width, double intensity)
        {
            gradient.GradientStops.Clear();

            double halfWidth = width / 2;
            double start = Math.Max(0, center - halfWidth);
            double end = Math.Min(1, center + halfWidth);

            // Base color (subtle white)
            byte baseAlpha = 0x15;
            byte peakAlpha = (byte)(0xFF * intensity);

            // Create smooth gradient
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(baseAlpha, 0xFF, 0xFF, 0xFF), 0.0));

            if (start > 0)
                gradient.GradientStops.Add(new GradientStop(Color.FromArgb(baseAlpha, 0xFF, 0xFF, 0xFF), start));

            gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(baseAlpha + (peakAlpha - baseAlpha) * 0.7), 0xFF, 0xFF, 0xFF), start + (center - start) * 0.5));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(peakAlpha, 0xFF, 0xFF, 0xFF), center));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(baseAlpha + (peakAlpha - baseAlpha) * 0.7), 0xFF, 0xFF, 0xFF), center + (end - center) * 0.5));

            if (end < 1)
                gradient.GradientStops.Add(new GradientStop(Color.FromArgb(baseAlpha, 0xFF, 0xFF, 0xFF), end));

            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(baseAlpha, 0xFF, 0xFF, 0xFF), 1.0));
        }

        #endregion
    }
}
