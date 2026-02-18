using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ICN_T2.Logic.Level5.Image;
using ICN_T2.UI.WPF.ViewModels;
using ICN_T2.YokaiWatch.Games;
using ICN_T2.YokaiWatch.Games.YW2;

namespace ICN_T2.UI.WPF.Views
{
    public partial class CharacterScaleView : System.Windows.Controls.UserControl
    {
        private CharacterScaleViewModel? _viewModel;
        private IGame? _game;

        public CharacterScaleView()
        {
            InitializeComponent();
            DataContext = null;
            DataContextChanged += OnDataContextChanged;
        }

        public void Initialize(IGame game)
        {
            _game = game;
            LoadCharacterIcon();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is CharacterScaleViewModel vm)
            {
                _viewModel = vm;
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                
                // Initial load
                LoadCharacterIcon();
                return;
            }

            _viewModel = null;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CharacterScaleViewModel.SelectedScale))
            {
                LoadCharacterIcon();
            }
        }

        private void LoadCharacterIcon()
        {
            if (_viewModel?.SelectedScale == null || _viewModel.SelectedScale.BaseInfo == null)
            {
                ImgCharacterIcon.Source = null;
                return;
            }

            try
            {
                var character = _viewModel.SelectedScale.BaseInfo;

                // 1. Prefix Check
                int prefix = character.FileNamePrefix;
                if (!GameSupport.PrefixLetter.ContainsKey(prefix) || GameSupport.PrefixLetter[prefix] == '?')
                {
                    ImgCharacterIcon.Source = null;
                    return;
                }

                // 2. Get Model Name
                string modelName = GameSupport.GetFileModelText(prefix, character.FileNameNumber, character.FileNameVariant);

                // 3. Load Icon from Game Files
                System.Drawing.Image? iconImage = LoadYokaiIcon(modelName);

                if (iconImage != null)
                {
                    ImgCharacterIcon.Source = ToBitmapImage(iconImage);
                    return;
                }

                // Fallback: Try external PNG
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
                Trace.WriteLine($"[CharacterScaleView] Icon Load Error: {ex.Message}");
                ImgCharacterIcon.Source = null;
            }
        }

        private System.Drawing.Image? LoadYokaiIcon(string modelName)
        {
            if (_game == null || _game.Files == null || !_game.Files.ContainsKey("face_icon")) return null;

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
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[CharacterScaleView] LoadYokaiIcon Error for {modelName}: {ex.Message}");
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
    }
}
