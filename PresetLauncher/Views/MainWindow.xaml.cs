using System.Windows;
using System.Windows.Controls;
using PresetLauncher.Views.Pages;

namespace PresetLauncher.Views
{
    public partial class MainWindow : Window
    {
        private Button? _currentNavButton;
        private Border? _currentIndicator;

        public MainWindow()
        {
            InitializeComponent();
            UpdateLocalizedText();
            NavigateToPresets();
            _currentNavButton = BtnPresets;
            _currentIndicator = PresetsIndicator;
        }

        private void UpdateLocalizedText()
        {
            Title = App.Localization.Get("app.title");
            NavPresets.Text = App.Localization.Get("nav.presets");
            NavHistory.Text = App.Localization.Get("nav.history");
            NavStats.Text = App.Localization.Get("nav.statistics");
            NavSettings.Text = App.Localization.Get("nav.settings");
        }

        private void SetActiveNavButton(Button button, Border? indicator)
        {
            if (_currentNavButton != null)
            {
                _currentNavButton.Background = System.Windows.Media.Brushes.Transparent;
            }
            if (_currentIndicator != null)
            {
                _currentIndicator.Visibility = Visibility.Collapsed;
            }
            
            button.Background = (System.Windows.Media.Brush)FindResource("NavSelectedBrush");
            if (indicator != null)
            {
                indicator.Visibility = Visibility.Visible;
            }
            
            _currentNavButton = button;
            _currentIndicator = indicator;
        }

        private void BtnPresets_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavButton(BtnPresets, PresetsIndicator);
            NavigateToPresets();
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavButton(BtnHistory, HistoryIndicator);
            MainFrame.Navigate(new HistoryPage());
        }

        private void BtnStats_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavButton(BtnStats, StatsIndicator);
            MainFrame.Navigate(new StatisticsPage());
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavButton(BtnSettings, SettingsIndicator);
            MainFrame.Navigate(new SettingsPage(this));
        }

        public void NavigateToPresets()
        {
            MainFrame.Navigate(new PresetsPage());
        }

        public void RefreshUI()
        {
            UpdateLocalizedText();
        }
    }
}
