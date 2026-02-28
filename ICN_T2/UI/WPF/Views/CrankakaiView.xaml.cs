using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ICN_T2.UI.WPF.Animations;

namespace ICN_T2.UI.WPF.Views
{
    public partial class CrankakaiView : System.Windows.Controls.UserControl
    {
        private System.Windows.Size _lastDetailHostSize = System.Windows.Size.Empty;
        private System.Windows.Size _lastDetailMainSize = System.Windows.Size.Empty;

        public CrankakaiView()
        {
            InitializeComponent();
            ApplyPanelLayoutOverrides();

            CharacterDetailHost.SizeChanged += (_, _) => UpdateDetailPanelRoundedClip();
            CharacterDetailBackdropBorder.SizeChanged += (_, _) => UpdateDetailPanelRoundedClip();
            CharacterDetailMainBorder.SizeChanged += (_, _) => UpdateDetailPanelRoundedClip();
            DetailScrollViewer.SizeChanged += (_, _) => UpdateDetailPanelRoundedClip();
            LayoutUpdated += CrankakaiView_LayoutUpdated;
            Loaded += (_, _) =>
            {
                ApplyPanelLayoutOverrides();
                UpdateDetailPanelRoundedClip();
                Dispatcher.InvokeAsync(UpdateDetailPanelRoundedClip, DispatcherPriority.Render);
            };
        }

        private void ApplyPanelLayoutOverrides()
        {
            CharacterListHost.Margin = new Thickness(
                0,
                AnimationConfig.CharacterListPanel_TopMargin,
                0,
                AnimationConfig.CharacterListPanel_BottomMargin);

            CharacterDetailHost.Margin = new Thickness(
                0,
                AnimationConfig.CharacterDetailPanel_TopMargin,
                0,
                AnimationConfig.CharacterDetailPanel_BottomMargin);

            CharacterListBackdropBorder.Margin = new Thickness(
                -AnimationConfig.CharacterListBackdrop_Expand,
                -AnimationConfig.CharacterListBackdrop_Expand,
                -AnimationConfig.CharacterListBackdrop_Expand,
                -AnimationConfig.CharacterListBackdrop_Expand);
            CharacterListBackdropBorder.CornerRadius = new CornerRadius(
                PART_SearchPanel.CornerRadius.TopLeft + AnimationConfig.CharacterListBackdrop_RadiusBoost);

            CharacterDetailBackdropBorder.Margin = new Thickness(
                -AnimationConfig.CharacterDetailBackdrop_Expand,
                -AnimationConfig.CharacterDetailBackdrop_Expand,
                -AnimationConfig.CharacterDetailBackdrop_Expand,
                -AnimationConfig.CharacterDetailBackdrop_Expand);
            CharacterDetailBackdropBorder.CornerRadius = new CornerRadius(
                AnimationConfig.CharacterDetailPanel_CornerRadius + AnimationConfig.CharacterDetailBackdrop_RadiusBoost);

            CharacterDetailMainBorder.CornerRadius = new CornerRadius(AnimationConfig.CharacterDetailPanel_CornerRadius);
            UpdateDetailPanelRoundedClip();
        }

        private static void ApplyRoundedClip(Border border)
        {
            double width = border.ActualWidth;
            double height = border.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            const double overscanX = 6.0;
            const double overscanTop = 6.0;
            const double overscanBottom = 10.0;

            double baseRadius = Math.Max(
                Math.Max(border.CornerRadius.TopLeft, border.CornerRadius.TopRight),
                Math.Max(border.CornerRadius.BottomLeft, border.CornerRadius.BottomRight));

            double clipWidth = width + (overscanX * 2.0);
            double clipHeight = height + overscanTop + overscanBottom;
            double radiusX = Math.Max(0.0, Math.Min(baseRadius + overscanX, clipWidth * 0.5));
            double radiusY = Math.Max(0.0, Math.Min(baseRadius + Math.Max(overscanTop, overscanBottom), clipHeight * 0.5));

            var clip = new RectangleGeometry(
                new Rect(-overscanX, -overscanTop, clipWidth, clipHeight),
                radiusX,
                radiusY);
            clip.Freeze();
            border.Clip = clip;
        }

        private static void ApplyRoundedClip(FrameworkElement element, double radius, double overscanX, double overscanTop, double overscanBottom)
        {
            double width = element.ActualWidth;
            double height = element.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            double clipWidth = width + (overscanX * 2.0);
            double clipHeight = height + overscanTop + overscanBottom;
            double radiusX = Math.Max(0.0, Math.Min(radius + overscanX, clipWidth * 0.5));
            double radiusY = Math.Max(0.0, Math.Min(radius + Math.Max(overscanTop, overscanBottom), clipHeight * 0.5));

            var clip = new RectangleGeometry(
                new Rect(-overscanX, -overscanTop, clipWidth, clipHeight),
                radiusX,
                radiusY);
            clip.Freeze();
            element.Clip = clip;
        }

        private void UpdateDetailPanelRoundedClip()
        {
            double mainRadius = AnimationConfig.CharacterDetailPanel_CornerRadius;
            ApplyRoundedClip(CharacterDetailHost, mainRadius, overscanX: 6.0, overscanTop: 6.0, overscanBottom: 12.0);
            ApplyRoundedClip(CharacterDetailBackdropBorder);
            ApplyRoundedClip(CharacterDetailMainBorder);
            ApplyRoundedClip(DetailScrollViewer, Math.Max(0.0, mainRadius - 12.0), overscanX: 4.0, overscanTop: 4.0, overscanBottom: 10.0);
        }

        private void CrankakaiView_LayoutUpdated(object? sender, EventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            var hostSize = new System.Windows.Size(CharacterDetailHost.ActualWidth, CharacterDetailHost.ActualHeight);
            var mainSize = new System.Windows.Size(CharacterDetailMainBorder.ActualWidth, CharacterDetailMainBorder.ActualHeight);
            if (IsSameSize(hostSize, _lastDetailHostSize) && IsSameSize(mainSize, _lastDetailMainSize))
            {
                return;
            }

            _lastDetailHostSize = hostSize;
            _lastDetailMainSize = mainSize;
            UpdateDetailPanelRoundedClip();
        }

        private static bool IsSameSize(System.Windows.Size a, System.Windows.Size b)
        {
            return Math.Abs(a.Width - b.Width) < 0.5 &&
                   Math.Abs(a.Height - b.Height) < 0.5;
        }
    }
}
