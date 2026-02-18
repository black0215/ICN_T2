using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ICN_T2.UI.WPF.Animations;
using ICN_T2.UI.WPF.ViewModels;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2.Logic;
using UserControl = System.Windows.Controls.UserControl;

using System.IO;
using ICN_T2.Logic.Level5.Image;

namespace ICN_T2.UI.WPF.Views
{
    /// <summary>
    /// CharacterInfoV3.xaml??????곹샇 ?묒슜 ?쇰━
    /// Bento ?ㅽ??쇱쓽 ?뺤옣 媛?ν븳 ?⑤꼸 援ъ“ UI
    /// </summary>
    public partial class CharacterInfoV3 : UserControl
    {
        private CharacterViewModel? _viewModel;
        private IGame? _game;
        private bool _isFilterOpen;
        private System.Windows.Size _lastDetailHostSize = System.Windows.Size.Empty;
        private System.Windows.Size _lastDetailMainSize = System.Windows.Size.Empty;

        // ?좊땲硫붿씠??吏???쒓컙 (ms)
        private const int AnimationDuration = 300;

        public CharacterInfoV3()
        {
            Trace.WriteLine("[CharacterInfoV3] Constructor start");
            InitializeComponent();
            DataContext = null;
            // Apply AnimationConfig settings
            ColList.Width = new GridLength(AnimationConfig.CharacterList_WidthRatio, GridUnitType.Star);
            ColDetail.Width = new GridLength(AnimationConfig.CharacterDetail_WidthRatio, GridUnitType.Star);
            DetailScrollViewer.Margin = new Thickness(
                AnimationConfig.CharacterDetail_HorizontalMargin,
                AnimationConfig.CharacterDetail_VerticalMargin,
                AnimationConfig.CharacterDetail_HorizontalMargin,
                AnimationConfig.CharacterDetail_VerticalMargin + 8); // tighter bottom spacing to reduce unnecessary scrolling

            ApplyPanelLayoutOverrides();
            CharacterDetailHost.SizeChanged += (_, _) => UpdateDetailPanelRoundedClip();
            CharacterDetailBackdropBorder.SizeChanged += (_, _) => UpdateDetailPanelRoundedClip();
            CharacterDetailMainBorder.SizeChanged += (_, _) => UpdateDetailPanelRoundedClip();
            DetailScrollViewer.SizeChanged += (_, _) => UpdateDetailPanelRoundedClip();
            LayoutUpdated += CharacterInfoV3_LayoutUpdated;
            Loaded += (_, _) =>
            {
                ApplyPanelLayoutOverrides();
                UpdateDetailPanelRoundedClip();
                Dispatcher.InvokeAsync(UpdateDetailPanelRoundedClip, DispatcherPriority.Render);
            };

            Trace.WriteLine("[CharacterInfoV3] InitializeComponent complete");
        }

        private void ApplyPanelLayoutOverrides()
        {
            // Keep list/right panel layout stable even if parent host layout recalculates.
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

            var panelRadius = new CornerRadius(AnimationConfig.CharacterDetailPanel_CornerRadius);
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

            CharacterDetailMainBorder.CornerRadius = panelRadius;
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
            // Force rounded silhouette at every visible layer to prevent bottom corners from appearing squared.
            double mainRadius = AnimationConfig.CharacterDetailPanel_CornerRadius;
            ApplyRoundedClip(CharacterDetailHost, mainRadius, overscanX: 6.0, overscanTop: 6.0, overscanBottom: 12.0);
            ApplyRoundedClip(CharacterDetailBackdropBorder);
            ApplyRoundedClip(CharacterDetailMainBorder);
            ApplyRoundedClip(DetailScrollViewer, Math.Max(0.0, mainRadius - 12.0), overscanX: 4.0, overscanTop: 4.0, overscanBottom: 10.0);
        }

        private void CharacterInfoV3_LayoutUpdated(object? sender, EventArgs e)
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

        /// <summary>
        /// 寃뚯엫 ?몄뒪?댁뒪濡?珥덇린??
        /// </summary>
        public void Initialize(IGame game)
        {
            Trace.WriteLine($"[CharacterInfoV3] Initialize called: game={game?.GetType().Name}");

            _game = game;
            _viewModel = new CharacterViewModel(game);
            DataContext = _viewModel;

            // ?좏깮 蹂寃??대깽??援щ룆
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            Trace.WriteLine("[CharacterInfoV3] Initialize complete");
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CharacterViewModel.SelectedCharacter))
            {
                Trace.WriteLine($"[CharacterInfoV3] SelectedCharacter changed: {_viewModel?.SelectedCharacter?.Name}");
                LoadCharacterIcon();
                LoadCharacterDescription();
            }
            else if (e.PropertyName == nameof(CharacterViewModel.DescriptionDisplay))
            {
                LoadCharacterDescription();
            }
        }

        #region 利됱떆 由ъ뒪???좏깮 (PreviewMouseLeftButtonDown)

        /// <summary>
        /// MouseDown 利됱떆 ?좏깮 ??湲곕낯 ListBox??MouseUp?먯꽌 ?좏깮?섎?濡??먮━寃??먭뺨吏?
        /// PreviewMouseLeftButtonDown?먯꽌 吏곸젒 ListBoxItem??李얠븘 ?좏깮
        /// </summary>
        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource == null) return;

            // VisualTree瑜??щ씪媛硫?ListBoxItem 李얘린
            var listBoxItem = FindParent<ListBoxItem>(originalSource);
            if (listBoxItem != null)
            {
                listBoxItem.IsSelected = true;
                // ?ъ빱?ㅻ룄 利됱떆 ?대룞
                listBoxItem.Focus();
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T found)
                    return found;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        #endregion

        #region Panel 0: Search List

        private void SearchPanel_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // TODO: ?섏쨷??援ы쁽
        }

        private void SearchPanel_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // TODO: ?섏쨷??援ы쁽
        }

        #endregion

        #region ?꾪꽣 ?щ씪?대뱶 ?⑤꼸 ?좊땲硫붿씠??

        private void BtnToggleFilter_Click(object sender, RoutedEventArgs e)
        {
            if (_isFilterOpen)
                CloseFilterPanel();
            else
                OpenFilterPanel();
        }

        private void BtnCloseFilter_Click(object sender, RoutedEventArgs e)
        {
            CloseFilterPanel();
        }

        private void OpenFilterPanel()
        {
            _isFilterOpen = true;
            FilterPanel.Visibility = Visibility.Visible;

            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var anim = new DoubleAnimation(300, 0, TimeSpan.FromMilliseconds(AnimationDuration))
            {
                EasingFunction = easing
            };
            FilterPanelTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        private void CloseFilterPanel()
        {
            _isFilterOpen = false;

            var easing = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            var anim = new DoubleAnimation(0, 300, TimeSpan.FromMilliseconds(AnimationDuration))
            {
                EasingFunction = easing
            };
            anim.Completed += (s, e) =>
            {
                FilterPanel.Visibility = Visibility.Collapsed;
            };
            FilterPanelTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        #endregion

        #region Panel 1: Character Icon

        /// <summary>
        /// 罹먮┃???꾩씠肄?濡쒕뱶 (Legacy Logic)
        /// 1. Get ModelName from GameSupport
        /// 2. Load .xi from face_icon folder using IMGC
        /// </summary>
        private void LoadCharacterIcon()
        {
            if (_viewModel?.SelectedCharacter == null)
            {
                ImgCharacterIcon.Source = null;
                return;
            }

            // Prefer icon already resolved by ViewModel so list/detail stay consistent.
            if (_viewModel.SelectedCharacter.Icon != null)
            {
                ImgCharacterIcon.Source = _viewModel.SelectedCharacter.Icon;
                return;
            }

            try
            {
                var character = _viewModel.SelectedCharacter.Model;

                // 1. Prefix Check (Legacy Logic)
                int prefix = character.FileNamePrefix;
                if (!GameSupport.PrefixLetter.ContainsKey(prefix) || GameSupport.PrefixLetter[prefix] == '?')
                {
                    ImgCharacterIcon.Source = null;
                    return;
                }

                // 2. Get Model Name
                string modelName = GameSupport.GetFileModelText(prefix, character.FileNameNumber, character.FileNameVariant);

                // 3. Load Icon from Game Files (Legacy Logic)
                // First, try loading internal game file (Priority: Game File > External)
                // However, user might want to override with external logic if present?
                // CharabaseDetailPanel implementation PRIORITIZES internal file over external for "face_icon" folder logic,
                // BUT CharabaseWindow.cs implementation had some external checks too.
                // The user said "Logic is legacy (Albatross's form/charabase)".
                // CharabaseDetailPanel uses LoadYokaiIcon which checks game.Files["face_icon"].

                System.Drawing.Image? iconImage = LoadYokaiIcon(modelName);

                if (iconImage != null)
                {
                    ImgCharacterIcon.Source = ToBitmapImage(iconImage);
                    return;
                }

                // Fallback: Try external PNG if internal failed
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string formattedName = $"face_icon.{character.FileNamePrefix:D2}.{character.FileNameNumber:D3}.{character.FileNameVariant:D2}.png";
                string modelPng = $"{modelName}.png";
                string[] candidatePaths =
                {
                    Path.Combine(basePath, formattedName),
                    Path.Combine(basePath, modelPng),
                    Path.Combine(basePath, "Resources", "face_icon", formattedName),
                    Path.Combine(basePath, "Resources", "Face Icon", formattedName),
                    Path.Combine(basePath, "Resources", "face_icon", modelPng),
                    Path.Combine(basePath, "Resources", "Face Icon", modelPng)
                };

                string? pngPath = candidatePaths.FirstOrDefault(File.Exists);
                if (!string.IsNullOrWhiteSpace(pngPath))
                {
                    ImgCharacterIcon.Source = LoadBitmapFromPath(pngPath);
                    return;
                }

                ImgCharacterIcon.Source = null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[CharacterInfoV3] Icon Load Error: {ex.Message}");
                ImgCharacterIcon.Source = null;
            }
        }

        private System.Drawing.Image? LoadYokaiIcon(string modelName)
        {
            // Check Cache? (If we want to implement cache, we need a dictionary)
            // For now, load directly to avoid state issues, or add a simple cache if performance needed.
            // ImgCharacterIcon.Source takes memory, so caching Bitmap might be redundant if we convert to WPF BitmapImage.

            if (_game == null || !_game.Files.TryGetValue("face_icon", out var faceIconFile)) return null;
            if (faceIconFile.File?.Directory == null || string.IsNullOrWhiteSpace(faceIconFile.Path)) return null;

            try
            {
                string fullPath = $"{faceIconFile.Path}/{modelName}.xi";
                var directory = faceIconFile.File.Directory;
                if (!directory.FileExists(fullPath))
                {
                    return null;
                }

                var vf = directory.GetFileStreamFromFullPath(fullPath);
                if (vf != null)
                {
                    byte[] data = vf.ByteContent ?? vf.ReadWithoutCaching();
                    if (data != null && data.Length > 0)
                    {
                        return IMGC.ToBitmap(data);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[CharacterInfoV3] LoadYokaiIcon Error for {modelName}: {ex.Message}");
            }

            return null;
        }

        private BitmapImage? ToBitmapImage(System.Drawing.Image? bitmap)
        {
            if (bitmap == null) return null;
            try
            {
                using (var memory = new MemoryStream())
                {
                    bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                    memory.Position = 0;

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    return bitmapImage;
                }
            }
            catch
            {
                return null;
            }
        }

        private BitmapImage? LoadBitmapFromPath(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Panel 2: Hash Values (??긽 ?쒖떆, ?몃쾭 ?놁쓬)

        // ?댁궛 ?⑤꼸? ?몃쾭 ?놁씠 ??긽 BaseHashDisplay ???쒖떆 (XAML 諛붿씤??

        #endregion

        #region Panel 3: Food Mapping

        // ?뚯떇 留ㅽ븨? ?ν썑 GameSupport 援ы쁽 ??異붽?

        #endregion

        #region Panel 4: Description

        /// <summary>
        /// 罹먮┃???ㅻ챸 ?띿뒪??濡쒕뱶
        /// DescriptionHash瑜?議고쉶?섏뿬 ?ㅼ젣 ?띿뒪??媛?몄삤湲?
        /// </summary>
        private void LoadCharacterDescription()
        {
            if (_viewModel?.SelectedCharacter == null)
            {
                TxtDescription.Text = "";
                return;
            }

            try
            {
                // Use the resolved Description from ViewModel
                string desc = _viewModel.SelectedCharacter.Description;

                // Process newlines
                desc = desc.Replace("\\n", Environment.NewLine).Replace("\n", Environment.NewLine);

                TxtDescription.Text = desc;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[CharacterInfoV3] Error loading description: {ex.Message}");
                TxtDescription.Text = "?ㅻ쪟";
            }
        }

        #endregion
    }
}


