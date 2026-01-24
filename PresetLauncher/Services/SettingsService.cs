using System;
using System.IO;
using Newtonsoft.Json;
using PresetLauncher.Models;

namespace PresetLauncher.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;
        public AppSettings CurrentSettings { get; private set; } = new();

        public SettingsService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PresetLauncher"
            );
            Directory.CreateDirectory(appDataPath);
            _settingsPath = Path.Combine(appDataPath, "settings.json");

            if (string.IsNullOrEmpty(CurrentSettings.DataPath))
            {
                CurrentSettings.DataPath = Path.Combine(appDataPath, "data.json");
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    CurrentSettings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                CurrentSettings = new AppSettings();
            }

            if (string.IsNullOrEmpty(CurrentSettings.DataPath))
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PresetLauncher"
                );
                CurrentSettings.DataPath = Path.Combine(appDataPath, "data.json");
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(CurrentSettings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public void UpdateTheme(string theme)
        {
            CurrentSettings.Theme = theme;
            Save();
        }

        public void UpdateLanguage(string language)
        {
            CurrentSettings.Language = language;
            Save();
        }
    }
}
