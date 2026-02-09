using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ICN_T2.UI.WPF.Animations;
using ICN_T2.UI.WPF.ViewModels;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2;
using ICN_T2.YokaiWatch.Games.YW2.Logic;
using UserControl = System.Windows.Controls.UserControl;

using System.IO;
using ICN_T2.Logic.Level5.Image;
using ICN_T2.YokaiWatch.Games.YW2;

namespace ICN_T2.UI.WPF.Views
{
    /// <summary>
    /// CharacterInfoV3.xaml에 대한 상호 작용 논리
    /// Bento 스타일의 확장 가능한 패널 구조 UI
    /// </summary>
    public partial class CharacterInfoV3 : UserControl
    {
        private CharacterViewModel? _viewModel;
        private IGame? _game;
        private bool _isFilterOpen;

        // 애니메이션 지속 시간 (ms)
        private const int AnimationDuration = 300;

        public CharacterInfoV3()
        {
            Trace.WriteLine("[CharacterInfoV3] 생성자 시작");
            InitializeComponent();
            Trace.WriteLine("[CharacterInfoV3] InitializeComponent 완료");
        }

        /// <summary>
        /// 게임 인스턴스로 초기화
        /// </summary>
        public void Initialize(IGame game)
        {
            Trace.WriteLine($"[CharacterInfoV3] Initialize 호출: game={game?.GetType().Name}");

            _game = game;
            _viewModel = new CharacterViewModel(game);
            DataContext = _viewModel;

            // 선택 변경 이벤트 구독
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            Trace.WriteLine("[CharacterInfoV3] Initialize 완료");
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CharacterViewModel.SelectedCharacter))
            {
                Trace.WriteLine($"[CharacterInfoV3] 선택된 캐릭터 변경: {_viewModel?.SelectedCharacter?.Name}");
                LoadCharacterIcon();
                LoadCharacterDescription();
            }
        }

        #region 즉시 리스트 선택 (PreviewMouseLeftButtonDown)

        /// <summary>
        /// MouseDown 즉시 선택 — 기본 ListBox는 MouseUp에서 선택하므로 느리게 느껴짐
        /// PreviewMouseLeftButtonDown에서 직접 ListBoxItem을 찾아 선택
        /// </summary>
        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource == null) return;

            // VisualTree를 올라가며 ListBoxItem 찾기
            var listBoxItem = FindParent<ListBoxItem>(originalSource);
            if (listBoxItem != null)
            {
                listBoxItem.IsSelected = true;
                // 포커스도 즉시 이동
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
            // TODO: 나중에 구현
        }

        private void SearchPanel_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // TODO: 나중에 구현
        }

        #endregion

        #region 필터 슬라이드 패널 애니메이션

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
        /// 캐릭터 아이콘 로드 (Legacy Logic)
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
                string pngName = $"face_icon.{character.FileNamePrefix:D2}.{character.FileNameNumber:D3}.{character.FileNameVariant:D2}.png";
                string pngPath = Path.Combine(basePath, pngName);

                if (File.Exists(pngPath))
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

            if (_game == null || !_game.Files.ContainsKey("face_icon")) return null;

            try
            {
                var file = _game.Files["face_icon"];
                string fullPath = $"{file.Path}/{modelName}.xi";

                if (_game is YW2 yw2 && yw2.Game != null && yw2.Game.Directory != null)
                {
                    if (!yw2.Game.Directory.FileExists(fullPath)) return null;

                    var vf = yw2.Game.Directory.GetFileStreamFromFullPath(fullPath);
                    if (vf != null)
                    {
                        byte[] data = vf.ByteContent ?? vf.ReadWithoutCaching();
                        if (data != null && data.Length > 0)
                        {
                            return IMGC.ToBitmap(data);
                        }
                    }
                }
                else
                {
                    // Fallback for non-YW2 or different structure?
                    // Currently focused on YW2 logic as per CharabaseDetailPanel
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

        #region Panel 2: Hash Values (항상 표시, 호버 없음)

        // 해산 패널은 호버 없이 항상 BaseHashDisplay 등 표시 (XAML 바인딩)

        #endregion

        #region Panel 3: Food Mapping

        // 음식 매핑은 향후 GameSupport 구현 시 추가

        #endregion

        #region Panel 4: Description

        /// <summary>
        /// 캐릭터 설명 텍스트 로드
        /// DescriptionHash를 조회하여 실제 텍스트 가져오기
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
                TxtDescription.Text = "Error";
            }
        }

        #endregion
    }
}
