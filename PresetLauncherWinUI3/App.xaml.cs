using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Services;

namespace PresetLauncher;

public partial class App : Application
{
    public static SettingsService Settings { get; private set; } = null!;
    public static DataService Data { get; private set; } = null!;
    public static LocalizationService Localization { get; private set; } = null!;
    public static ThemeService Theme { get; private set; } = null!;
    public static new App Current => (App)Application.Current;

    public Window? MainWindow { get; private set; }

    private readonly UpdateService _updateService = new();

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Settings = new SettingsService();
        Settings.Load();

        Localization = new LocalizationService();
        Localization.SetLanguage(Settings.CurrentSettings.Language);

        Theme = new ThemeService();

        Data = new DataService();
        Data.Load();

        MainWindow = new MainWindow();
        MainWindow.Closed += (s, e) => SaveAndExit();
        MainWindow.Activate();

        Theme.SetWindow(MainWindow);
        Theme.ApplyTheme(Settings.CurrentSettings.Theme);
        Theme.ApplyBackdrop(Settings.CurrentSettings.Backdrop);

        _ = CheckForUpdateAsync();
    }

    private async System.Threading.Tasks.Task CheckForUpdateAsync()
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(2000);

            var latest = await _updateService.GetLatestVersionAsync();
            var installed = _updateService.GetInstalledVersion();

            if (!_updateService.IsUpdateAvailable(installed, latest)) return;

            if (MainWindow?.Content?.XamlRoot is not { } xamlRoot) return;

            var dialog = new ContentDialog
            {
                Title = Localization.Get("settings.updateAvailable") ?? "Mise à jour disponible",
                Content = $"La version v{latest} est disponible.\n\nVoulez-vous la télécharger et l'installer ?",
                PrimaryButtonText = Localization.Get("dialog.yes") ?? "Installer",
                CloseButtonText = Localization.Get("dialog.no") ?? "Plus tard",
                XamlRoot = xamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await _updateService.DownloadAndInstallAsync(latest, null);
        }
        catch { }
    }

    public static void SaveAndExit()
    {
        Settings.Save();
        Data.Save();
    }
}
