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

    private void CmbAccent_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (CmbAccent.SelectedItem is ComboBoxItem item && item.Tag is string color)
        {
            App.Settings.UpdateAccentColor(color);
            App.Theme.ApplyAccent(color, App.Settings.CurrentSettings.UseAccent, App.Settings.CurrentSettings.UseTransparency);
        }
    }

    private void TglUseAccent_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        App.Settings.UpdateUseAccent(TglUseAccent.IsOn);
        App.Theme.ApplyAccent(App.Settings.CurrentSettings.AccentColor, TglUseAccent.IsOn, App.Settings.CurrentSettings.UseTransparency);
    }

    private void TglTransparency_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        App.Settings.UpdateUseTransparency(TglTransparency.IsOn);
        App.Theme.ApplyAccent(App.Settings.CurrentSettings.AccentColor, App.Settings.CurrentSettings.UseAccent, TglTransparency.IsOn);
    }

    private void UpdateLocalizedText()
    {
        TxtLanguageCategory.Text = App.Localization.Get("settings.language");
        // language names
        LangFr.Content = App.Localization.Get("language.fr");
        LangEn.Content = App.Localization.Get("language.en");
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
        BackdropNone.Content = App.Localization.Get("settings.backdrop.none");
        BackdropMica.Content = App.Localization.Get("settings.backdrop.mica");
        BackdropAcrylic.Content = App.Localization.Get("settings.backdrop.acrylic");
        // accent texts
        TxtAccent.Text = App.Localization.Get("settings.accent");
        TglUseAccent.Header = App.Localization.Get("settings.accent.use");
        TglTransparency.Header = App.Localization.Get("settings.accent.transparency");
        AccentBlue.Content = App.Localization.Get("accent.blue");
        AccentRed.Content = App.Localization.Get("accent.red");
        AccentGreen.Content = App.Localization.Get("accent.green");
        AccentPurple.Content = App.Localization.Get("accent.purple");
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
        // Backdrop
        foreach (ComboBoxItem item in CmbBackdrop.Items)
        {
            if (item.Tag?.ToString() == settings.Backdrop)
            {
                CmbBackdrop.SelectedItem = item;
                break;
            }
        }
        // accent
        foreach (ComboBoxItem item in CmbAccent.Items)
        {
            if (item.Tag?.ToString() == settings.AccentColor)
            {
                CmbAccent.SelectedItem = item;
                break;
            }
        }
        TglUseAccent.IsOn = settings.UseAccent;
        TglTransparency.IsOn = settings.UseTransparency;
    }

    private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (CmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string language)
        {
            App.Settings.UpdateLanguage(language);
            App.Localization.SetLanguage(language);
            // Update UI texts to new language
            UpdateLocalizedText();
            // Notify main window to refresh its UI labels
            if (App.Current.MainWindow is MainWindow mw)
            {
                mw.RefreshUI();
            }
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

    private void CmbBackdrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (CmbBackdrop.SelectedItem is ComboBoxItem item && item.Tag is string backdrop)
        {
            App.Settings.UpdateBackdrop(backdrop);
            App.Theme.ApplyBackdrop(backdrop);
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
