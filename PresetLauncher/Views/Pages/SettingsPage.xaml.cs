using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PresetLauncher.Views;

namespace PresetLauncher.Views.Pages
{
    public partial class SettingsPage : Page
    {
        private readonly MainWindow _mainWindow;
        private bool _isInitializing = true;

        public SettingsPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            LoadSettings();
            UpdateLocalizedText();
            _isInitializing = false;
        }

        private void UpdateLocalizedText()
        {
            TxtTitle.Text = App.Localization.Get("settings.title");
            TxtLanguageLabel.Text = App.Localization.Get("settings.language");
            TxtThemeLabel.Text = App.Localization.Get("settings.theme");
            ThemeSystem.Content = App.Localization.Get("settings.theme.system");
            ThemeLight.Content = App.Localization.Get("settings.theme.light");
            ThemeDark.Content = App.Localization.Get("settings.theme.dark");
            TxtOptionsLabel.Text = App.Localization.Get("settings.options");
            TxtConfirmLaunch.Text = App.Localization.Get("settings.confirmLaunch");
            TxtNotifications.Text = App.Localization.Get("settings.notifications");
            TxtDataLabel.Text = App.Localization.Get("settings.data");
            TxtDataPathLabel.Text = App.Localization.Get("settings.dataPath");
            TxtCleanup.Text = App.Localization.Get("settings.cleanup");
        }

        private void LoadSettings()
        {
            var settings = App.Settings.CurrentSettings;

            foreach (ComboBoxItem item in CmbLanguage.Items)
            {
                if (item.Tag?.ToString() == settings.Language)
                {
                    CmbLanguage.SelectedItem = item;
                    break;
                }
            }

            foreach (ComboBoxItem item in CmbTheme.Items)
            {
                if (item.Tag?.ToString() == settings.Theme)
                {
                    CmbTheme.SelectedItem = item;
                    break;
                }
            }

            ChkConfirmLaunch.IsChecked = settings.ConfirmBeforeLaunch;
            ChkNotifications.IsChecked = settings.ShowNotifications;
            TxtDataPath.Text = settings.DataPath;
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (CmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string language)
            {
                App.Settings.UpdateLanguage(language);
                App.Localization.SetLanguage(language);
                UpdateLocalizedText();
                _mainWindow.RefreshUI();
            }
        }

        private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (CmbTheme.SelectedItem is ComboBoxItem item && item.Tag is string theme)
            {
                App.Settings.UpdateTheme(theme);
                App.Theme.ApplyTheme(theme);
            }
        }

        private void ChkConfirmLaunch_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            App.Settings.CurrentSettings.ConfirmBeforeLaunch = ChkConfirmLaunch.IsChecked ?? true;
            App.Settings.Save();
        }

        private void ChkNotifications_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            App.Settings.CurrentSettings.ShowNotifications = ChkNotifications.IsChecked ?? true;
            App.Settings.Save();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                FileName = "data.json"
            };

            if (dialog.ShowDialog() == true)
            {
                App.Settings.CurrentSettings.DataPath = dialog.FileName;
                App.Settings.Save();
                TxtDataPath.Text = dialog.FileName;
            }
        }

        private void BtnCleanup_Click(object sender, RoutedEventArgs e)
        {
            App.Data.CleanupInvalidExecutables();
            MessageBox.Show(App.Localization.Get("settings.cleanupDone"), 
                App.Localization.Get("settings.cleanup"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
