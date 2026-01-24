using System;

namespace PresetLauncher.Models
{
    public class AppSettings
    {
        public string Language { get; set; } = "fr";
        public string Theme { get; set; } = "system";
        public string DataPath { get; set; } = string.Empty;
        public bool MinimizeToTray { get; set; } = false;
        public bool ConfirmBeforeLaunch { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public int MaxHistoryEntries { get; set; } = 100;
        public DateTime? LastBackup { get; set; }
    }
}
