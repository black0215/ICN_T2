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
    public partial class YokaiStatsView : System.Windows.Controls.UserControl
    {
        private YokaiStatsViewModel? _viewModel;
        private IGame? _game;

        public YokaiStatsView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        public void Initialize(IGame game)
        {
            _game = game;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is YokaiStatsViewModel vm)
            {
                _viewModel = vm;
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                
                // Initial load
                LoadCharacterIcon();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(YokaiStatsViewModel.SelectedStats))
            {
                LoadCharacterIcon();
            }
        }

        private void LoadCharacterIcon()
        {
            // Placeholder for now
            // Same logic as CharacterScaleView can be implemented here if needed
        }
    }
}
