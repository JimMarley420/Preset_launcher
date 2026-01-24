using System;
using System.Globalization;
using System.Windows;
using PresetLauncher.Services;

namespace PresetLauncher
{
    public partial class App : Application
    {
        public static SettingsService Settings { get; private set; } = null!;
        public static DataService Data { get; private set; } = null!;
        public static LocalizationService Localization { get; private set; } = null!;
        public static ThemeService Theme { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Settings = new SettingsService();
            Settings.Load();

            Localization = new LocalizationService();
            Localization.SetLanguage(Settings.CurrentSettings.Language);

            Theme = new ThemeService();
            Theme.ApplyTheme(Settings.CurrentSettings.Theme);

            Data = new DataService(Settings.CurrentSettings.DataPath);
            Data.Load();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Settings.Save();
            Data.Save();
            base.OnExit(e);
        }
    }
}
