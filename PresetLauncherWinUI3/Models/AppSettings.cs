using System;

namespace PresetLauncher.Models
{
    public class AppSettings
    {
        public string Language { get; set; } = "fr";
        public string Theme { get; set; } = "system";
        // Backdrop: none | mica | acrylic
        public string Backdrop { get; set; } = "mica";
        // Accent color name, e.g. "Blue", "Red"
        public string AccentColor { get; set; } = "Blue";
        // Whether to use accent color for titlebar/background
        public bool UseAccent { get; set; } = true;
        // Whether to use transparency/translucency effects
        public bool UseTransparency { get; set; } = true;
        public string DataPath { get; set; } = string.Empty;
        public bool MinimizeToTray { get; set; } = false;
        public bool ConfirmBeforeLaunch { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public int MaxHistoryEntries { get; set; } = 100;
        public DateTime? LastBackup { get; set; }
        public string AppVersion { get; set; } = "v1.0.0";
        public string SkippedUpdateVersion { get; set; } = "";
        public string DeferredUpdateVersion { get; set; } = "";
        public DateTimeOffset? DeferredUpdateUntil { get; set; }
        public string MinimumSupportedVersion { get; set; } = "0.0.0";
    }
}
