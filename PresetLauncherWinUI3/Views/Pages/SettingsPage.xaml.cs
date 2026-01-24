using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PresetLauncher.Views.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _isInitializing = true;

    public SettingsPage()
    {
        this.InitializeComponent();
        UpdateLocalizedText();
        LoadSettings();
        _isInitializing = false;
    }

    private void UpdateLocalizedText()
    {
        TxtLanguageCategory.Text = App.Localization.Get("settings.language");
        TxtThemeCategory.Text = App.Localization.Get("settings.theme");
        TxtOptionsCategory.Text = App.Localization.Get("settings.options");
        TxtDataCategory.Text = App.Localization.Get("settings.data");
        TxtConfirmLaunch.Text = App.Localization.Get("settings.confirmLaunch");
        TxtNotifications.Text = App.Localization.Get("settings.notifications");
        TxtDataPath.Text = App.Localization.Get("settings.dataPath");
        TxtCleanup.Text = App.Localization.Get("settings.cleanup");
        ThemeSystem.Content = App.Localization.Get("settings.theme.system");
        ThemeLight.Content = App.Localization.Get("settings.theme.light");
        ThemeDark.Content = App.Localization.Get("settings.theme.dark");
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

        TglConfirmLaunch.IsOn = settings.ConfirmBeforeLaunch;
        TglNotifications.IsOn = settings.ShowNotifications;
        TxtDataPathValue.Text = settings.DataPath;
    }

    private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (CmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string language)
        {
            App.Settings.UpdateLanguage(language);
            App.Localization.SetLanguage(language);
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

    private void TglConfirmLaunch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        App.Settings.CurrentSettings.ConfirmBeforeLaunch = TglConfirmLaunch.IsOn;
        App.Settings.Save();
    }

    private void TglNotifications_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        App.Settings.CurrentSettings.ShowNotifications = TglNotifications.IsOn;
        App.Settings.Save();
    }

    private async void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("JSON files", new[] { ".json" });
        picker.SuggestedFileName = "data";

        var hwnd = WindowNative.GetWindowHandle(App.Current.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            App.Settings.CurrentSettings.DataPath = file.Path;
            App.Settings.Save();
            TxtDataPathValue.Text = file.Path;
        }
    }

    private async void BtnCleanup_Click(object sender, RoutedEventArgs e)
    {
        App.Data.CleanupInvalidExecutables();
        
        var dialog = new ContentDialog
        {
            Title = App.Localization.Get("settings.cleanup"),
            Content = App.Localization.Get("settings.cleanupDone"),
            CloseButtonText = App.Localization.Get("dialog.ok"),
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
