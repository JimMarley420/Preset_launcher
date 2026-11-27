using System.Diagnostics;
using Microsoft.UI.Xaml;
using PresetLauncher.Services;

namespace PresetLauncher;

public partial class App : Application
{
    public static SettingsService Settings { get; private set; } = null!;
    public static DataService Data { get; private set; } = null!;
    public static LocalizationService Localization { get; private set; } = null!;
    internal static ThemeService Theme { get; private set; } = null!;
    public static new App Current => (App)Application.Current;

    public MainWindow MainWindow { get; private set; } = null!;
    public UpdateCoordinator Updates { get; } = new();

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = Task.Run(() => UpdateStaging.CleanOldAsync(Environment.GetCommandLineArgs()));
        Settings = new SettingsService();
        Settings.Load();
        Localization = new LocalizationService();
        Theme = new ThemeService();
        Data = new DataService();
        Data.Load();

        MainWindow = new MainWindow();
        var root = (FrameworkElement)MainWindow.Content;
        root.Loaded += OnRootLoaded;
        MainWindow.Activate();
        Program.AttachWindow(MainWindow);

        async void OnRootLoaded(object sender, RoutedEventArgs e)
        {
            root.Loaded -= OnRootLoaded;
            try { UpdateStartup.SignalReady(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
            {
                Debug.WriteLine(ex);
            }
            try { await Updates.CheckAsync(manual: false); }
            finally { MainWindow.FinishStartup(); }
        }
    }
}
