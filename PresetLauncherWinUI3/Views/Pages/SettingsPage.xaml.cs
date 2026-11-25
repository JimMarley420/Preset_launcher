using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using Windows.Storage.Pickers;
using WinRT.Interop;
using PresetLauncher.Services;
using System;
using System.Threading.Tasks;

namespace PresetLauncher.Views.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly UpdateService _updateService = new();
    private bool _isInitializing = true;

    public SettingsPage()
    {
        this.InitializeComponent();
        LoadSettings();
        _isInitializing = false;
    }

    public void RefreshUI()
    {
        _isInitializing = true;
        LoadSettings();
        _isInitializing = false;
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
        TxtDataPathValue.Text = settings.DataPath;
        TxtVersionValue.Text = $"v{_updateService.GetInstalledVersion()}";

        foreach (ComboBoxItem item in CmbBackdrop.Items)
        {
            if (item.Tag?.ToString() == settings.Backdrop)
            {
                CmbBackdrop.SelectedItem = item;
                break;
            }
        }

        void SetText(string name, string value)
        {
            var el = this.FindName(name);
            if (el is TextBlock tb)
                tb.Text = value;
            else if (el is ComboBoxItem cbi)
                cbi.Content = value;
            else if (el is Button btn)
                btn.Content = value;
            else if (el is ComboBox cb)
            {
                try { cb.Header = value; } catch { }
            }
        }

        string Localized(string key, string @default = "")
        {
            var v = App.Localization.Get(key) ?? string.Empty;
            if (string.IsNullOrEmpty(v) || v == key) return @default;
            return v;
        }

        SetText("TxtSettingsTitle", Localized("settings.title", "Settings"));
        SetText("TxtSettingsSubtitle", Localized("settings.subtitle", string.Empty));

        SetText("TxtAppearanceTitle", Localized("settings.appearance", string.Empty));
        SetText("TxtThemeLabel", Localized("settings.theme", string.Empty));
        SetText("TxtThemeSubtitle", Localized("settings.theme.subtitle", string.Empty));
        SetText("ThemeSystem", Localized("settings.theme.system", "System"));
        SetText("ThemeLight", Localized("settings.theme.light", "Light"));
        SetText("ThemeDark", Localized("settings.theme.dark", "Dark"));

        SetText("TxtLanguageLabel", Localized("settings.language", "Language"));
        SetText("TxtLanguageSubtitle", Localized("settings.language.subtitle", string.Empty));
        SetText("LangFr", Localized("language.fr", "Français"));
        SetText("LangEn", Localized("language.en", "English"));

        SetText("TxtBackdropLabel", Localized("settings.backdrop", string.Empty));
        SetText("TxtBackdropSubtitle", Localized("settings.backdrop.subtitle", string.Empty));
        SetText("BackdropNone", Localized("settings.backdrop.none", "None"));
        SetText("BackdropMica", Localized("settings.backdrop.mica", "Mica"));
        SetText("BackdropAcrylic", Localized("settings.backdrop.acrylic", "Acrylic"));

        SetText("TxtBehaviorTitle", Localized("settings.behavior", string.Empty));
        SetText("TxtConfirmLaunchLabel", Localized("settings.confirmLaunch", string.Empty));
        SetText("TxtConfirmLaunchSubtitle", Localized("settings.confirmLaunch.subtitle", string.Empty));

        SetText("TxtDataTitle", Localized("settings.data", string.Empty));
        SetText("TxtDataPathLabel", Localized("settings.dataPath", string.Empty));
        SetText("BtnBrowse", Localized("settings.browse", string.Empty));

        SetText("TxtCleanupLabel", Localized("settings.cleanup", string.Empty));
        SetText("TxtCleanupSubtitle", Localized("settings.cleanup.subtitle", string.Empty));
        SetText("BtnCleanup", Localized("settings.cleanup.action", string.Empty));

        SetText("TxtResetLabel", Localized("settings.reset.title", string.Empty));
        SetText("TxtResetSubtitle", Localized("settings.reset.subtitle", string.Empty));
        SetText("BtnReset", Localized("settings.reset.action", string.Empty));

        SetText("TxtAboutTitle", Localized("settings.about", string.Empty));
        SetText("TxtVersionLabel", Localized("settings.version", string.Empty));
        SetText("BtnCheckUpdate", Localized("settings.checkUpdate", string.Empty));
    }

    private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (CmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string language)
        {
            App.Settings.UpdateLanguage(language);
            App.Localization.SetLanguage(language);
            if (App.Current.MainWindow is MainWindow mw)
                mw.RefreshUI();
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
            try
            {
                var sourceFile = new System.IO.FileInfo(System.IO.Path.Combine(App.Settings.CurrentSettings.DataPath, "data.json"));
                if (sourceFile.Exists)
                {
                    System.IO.File.Copy(sourceFile.FullName, file.Path, overwrite: true);

                    var dialog = new ContentDialog
                    {
                        Title = App.Localization.Get("dialog.export") ?? "Export",
                        Content = App.Localization.Get("preset.exported") ?? "Preset exported successfully!",
                        CloseButtonText = App.Localization.Get("dialog.ok") ?? "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = App.Localization.Get("dialog.error") ?? "Error",
                    Content = $"{App.Localization.Get("settings.export.error") ?? "Error exporting"}: {ex.Message}",
                    CloseButtonText = App.Localization.Get("dialog.ok") ?? "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
    }

    private async void BtnCleanup_Click(object sender, RoutedEventArgs e)
    {
        App.Data.CleanupInvalidExecutables();

        var dialog = new ContentDialog
        {
            Title = App.Localization.Get("settings.cleanup.title") ?? "Cleanup",
            Content = App.Localization.Get("settings.cleanup.success") ?? "Data cleaned successfully.",
            CloseButtonText = App.Localization.Get("dialog.ok") ?? "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        var confirmDialog = new ContentDialog
        {
            Title = App.Localization.Get("settings.reset.title") ?? "Reset",
            Content = "Êtes-vous sûr de vouloir réinitialiser l'application ?\n\nCette action va :\n- Supprimer tous les presets\n- Supprimer l'historique d'exécution\n- Réinitialiser tous les paramètres\n\nCette action est IRRÉVERSIBLE !",
            PrimaryButtonText = App.Localization.Get("settings.reset.action") ?? "Reset",
            CloseButtonText = App.Localization.Get("dialog.cancel") ?? "Cancel",
            XamlRoot = this.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            try
            {
                App.Data.ClearAllData();
                App.Settings.Reset();

                App.Localization.SetLanguage("fr");
                App.Theme.ApplyTheme("system");
                App.Theme.ApplyBackdrop("mica");

                _isInitializing = true;
                LoadSettings();
                _isInitializing = false;

                if (App.Current.MainWindow is MainWindow mw)
                    mw.RefreshUI();

                var successDialog = new ContentDialog
                {
                    Title = App.Localization.Get("settings.cleanupDone") ?? "Cleanup completed!",
                    Content = App.Localization.Get("settings.cleanup.success") ?? "Data cleaned successfully.",
                    CloseButtonText = App.Localization.Get("dialog.ok") ?? "OK",
                    XamlRoot = this.XamlRoot
                };
                await successDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = App.Localization.Get("dialog.error") ?? "Error",
                    Content = $"Erreur lors de la réinitialisation : {ex.Message}",
                    CloseButtonText = App.Localization.Get("dialog.ok") ?? "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
    }

    private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        BtnCheckUpdate.IsEnabled = false;
        try { await App.Current.Updates.CheckAsync(manual: true); }
        finally { BtnCheckUpdate.IsEnabled = true; }
    }
}
