using System.Windows;
using System.Windows.Controls;
using PresetLauncher.Views.Pages;

namespace PresetLauncher.Views
{
    public partial class MainWindow : Window
    {
        private Button? _currentNavButton;

        public MainWindow()
        {
            InitializeComponent();
            UpdateLocalizedText();
            NavigateToPresets();
            _currentNavButton = BtnPresets;
        }

        private void UpdateLocalizedText()
        {
            Title = App.Localization.Get("app.title");
            NavPresets.Text = App.Localization.Get("nav.presets");
            NavHistory.Text = App.Localization.Get("nav.history");
            NavStats.Text = App.Localization.Get("nav.statistics");
            NavSettings.Text = App.Localization.Get("nav.settings");
        }

        private void SetActiveNavButton(Button button)
        {
            if (_currentNavButton != null)
            {
                _currentNavButton.Background = System.Windows.Media.Brushes.Transparent;
            }
            button.Background = (System.Windows.Media.Brush)FindResource("NavSelectedBrush");
            _currentNavButton = button;
        }

        private void BtnPresets_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavButton(BtnPresets);
            NavigateToPresets();
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavButton(BtnHistory);
            MainFrame.Navigate(new HistoryPage());
        }

        private void BtnStats_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavButton(BtnStats);
            MainFrame.Navigate(new StatisticsPage());
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavButton(BtnSettings);
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
