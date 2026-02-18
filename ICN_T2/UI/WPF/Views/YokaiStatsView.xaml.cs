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
            DataContext = null;
            DataContextChanged += OnDataContextChanged;
        }

        public void Initialize(IGame game)
        {
            _game = game;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is YokaiStatsViewModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is YokaiStatsViewModel vm)
            {
                _viewModel = vm;
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                
                // Initial load
                LoadCharacterIcon();
                SetVisibleTab("Stats");
            }
            else
            {
                _viewModel = null;
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

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string tag)
            {
                SetVisibleTab(tag);
            }
        }

        private void SetVisibleTab(string tag)
        {
            if (StatsCard == null || TechCard == null || QuoteCard == null) return;

            StatsCard.Visibility = tag == "Stats" ? Visibility.Visible : Visibility.Collapsed;
            TechCard.Visibility = tag == "Tech" ? Visibility.Visible : Visibility.Collapsed;
            QuoteCard.Visibility = tag == "Quotes" ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
