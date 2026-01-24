using Microsoft.UI.Xaml;
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

        Data = new DataService(Settings.CurrentSettings.DataPath);
        Data.Load();

        MainWindow = new MainWindow();
        MainWindow.Closed += (s, e) => SaveAndExit();
        MainWindow.Activate();
    }

    public static void SaveAndExit()
    {
        Settings.Save();
        Data.Save();
    }
}
