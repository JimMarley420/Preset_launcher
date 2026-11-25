namespace PresetLauncher.Models;

public enum LaunchMode { All, MissingOnly }

public static class LaunchInteraction
{
    public static string? ConfirmationAction(AppSettings settings, LaunchMode mode) =>
        settings.ConfirmBeforeLaunch ? ActionKey(mode) : null;

    public static string ActionKey(LaunchMode mode) =>
        mode == LaunchMode.All ? "preset.launchAll" : "preset.launchMissing";
}
