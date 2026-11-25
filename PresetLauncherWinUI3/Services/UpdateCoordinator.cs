using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PresetLauncher.Services;

public sealed class UpdateCoordinator
{
    private readonly UpdateService _service = new();
    private bool _checking;
    private static string T(string key, params object[] args) => App.Localization.Get(key, args);

    public async Task CheckAsync(bool manual)
    {
        if (_checking || App.Current.MainWindow?.Content?.XamlRoot is not { } root)
            return;
        _checking = true;
        var installed = _service.GetInstalledVersion();
        var settings = App.Settings.CurrentSettings;
        if (!UpdateService.TryVersion(settings.MinimumSupportedVersion, out _))
            settings.MinimumSupportedVersion = "0.0.0";
        var required = installed != "0.0.0" && _service.IsUpdateAvailable(installed, settings.MinimumSupportedVersion);
        try
        {
            if (installed == "0.0.0")
            {
                if (manual) await MessageAsync(root, "settings.checkUpdate", T("update.sourceBuild"));
                return;
            }

            SetGate(required);
            while (true)
            {
                ReleaseUpdate? update;
                try
                {
                    update = await _service.GetLatestUpdateAsync(installed);
                    if (update is not null && _service.IsUpdateAvailable(settings.MinimumSupportedVersion, update.MinimumSupportedVersion.ToString(3)))
                    {
                        settings.MinimumSupportedVersion = update.MinimumSupportedVersion.ToString(3);
                        App.Settings.Save();
                    }
                    required = _service.IsUpdateAvailable(installed, settings.MinimumSupportedVersion);
                    SetGate(required);
                    if (required && (update is null || _service.IsUpdateAvailable(update.Version.ToString(3), settings.MinimumSupportedVersion)))
                        throw new InvalidDataException("No release satisfies the known minimum version.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    if (required)
                    {
                        if (await RetryRequiredAsync(root, T("settings.updateCheckFailed"))) continue;
                        CloseApp();
                    }
                    else if (manual)
                        await MessageAsync(root, "dialog.error", T("settings.updateCheckFailed"));
                    return;
                }

                if (update is null)
                {
                    if (manual) await MessageAsync(root, "settings.checkUpdate", T("settings.updateUpToDate"));
                    return;
                }

                required |= UpdateService.IsRequired(installed, update);
                SetGate(required);
                if (!required && !UpdateService.ShouldPrompt(installed, update, settings.SkippedUpdateVersion, manual,
                    settings.DeferredUpdateVersion, settings.DeferredUpdateUntil))
                    return;

                var recommended = !required && update.Policy == UpdatePolicy.Recommended;
                var dialog = new ContentDialog
                {
                    XamlRoot = root,
                    Title = T(required ? "update.requiredTitle" : recommended ? "update.recommendedTitle" : "settings.updateAvailable"),
                    Content = T(required ? "update.requiredMessage" : recommended ? "update.recommendedMessage" : "update.optionalMessage", installed, update.Version.ToString(3)),
                    PrimaryButtonText = T("update.install"),
                    SecondaryButtonText = required || recommended ? "" : T("update.skip"),
                    CloseButtonText = T(required ? "update.exit" : recommended ? "update.remindTomorrow" : "update.later"),
                    DefaultButton = ContentDialogButton.Primary
                };
                var choice = await dialog.ShowAsync();
                if (choice != ContentDialogResult.Primary)
                {
                    if (required) CloseApp();
                    else if (recommended)
                    {
                        settings.DeferredUpdateVersion = update.Version.ToString(3);
                        settings.DeferredUpdateUntil = DateTimeOffset.UtcNow.AddHours(24);
                        App.Settings.Save();
                    }
                    else if (choice == ContentDialogResult.Secondary)
                    {
                        settings.SkippedUpdateVersion = update.Version.ToString(3);
                        App.Settings.Save();
                    }
                    return;
                }

                string? installer = null;
                bool launched = false;
                try
                {
                    installer = await DownloadAsync(root, update);
                    App.SaveAndExit();
                    using var process = Process.Start(new ProcessStartInfo { FileName = installer, UseShellExecute = true });
                    if (process is null) throw new InvalidOperationException("Installer did not start.");
                    launched = true;
                    App.Current.MainWindow?.Close();
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    if (installer is not null && !launched)
                    {
                        try { Directory.Delete(Path.GetDirectoryName(installer)!, recursive: true); }
                        catch (IOException cleanup) { Debug.WriteLine(cleanup); }
                        catch (UnauthorizedAccessException cleanup) { Debug.WriteLine(cleanup); }
                    }

                    if (required)
                    {
                        if (await RetryRequiredAsync(root, T("settings.updateDownloadError"))) continue;
                        CloseApp();
                    }
                    else if (ex is not OperationCanceledException)
                        await MessageAsync(root, "dialog.error", T("settings.updateDownloadError"));
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            // A known mandatory update cannot be bypassed by a failed modal dialog.
            if (required) CloseApp();
        }
        finally { _checking = false; }
    }

    private static void SetGate(bool blocked)
    {
        if (App.Current.MainWindow is MainWindow window) window.SetUpdateGate(blocked);
    }

    private static void CloseApp()
    {
        App.SaveAndExit();
        App.Current.MainWindow?.Close();
    }

    private async Task<string> DownloadAsync(XamlRoot root, ReleaseUpdate update)
    {
        using var cancellation = new CancellationTokenSource();
        var status = new TextBlock { Text = T("settings.updatePreparing"), TextWrapping = TextWrapping.Wrap };
        var progressBar = new ProgressBar { Minimum = 0, Maximum = 100 };
        var panel = new StackPanel { Spacing = 12, MinWidth = 280 };
        panel.Children.Add(status);
        panel.Children.Add(progressBar);
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = T("settings.updateDownloadingInstalling"),
            Content = panel,
            CloseButtonText = T("dialog.cancel")
        };
        bool closed = false;
        dialog.Closed += (_, _) => { closed = true; cancellation.Cancel(); };
        var showing = dialog.ShowAsync();
        try
        {
            var progress = new Progress<int>(percent =>
            {
                progressBar.Value = percent;
                status.Text = $"{T("settings.updateDownloading")} {percent}%";
            });
            return await _service.DownloadVerifiedAsync(update, progress, cancellation.Token);
        }
        finally
        {
            if (!closed) dialog.Hide();
            await showing;
        }
    }

    private static async Task<bool> RetryRequiredAsync(XamlRoot root, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = T("update.requiredTitle"),
            Content = message + "\n\n" + T("update.requiredRetry"),
            PrimaryButtonText = T("update.retry"),
            CloseButtonText = T("update.exit")
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static async Task MessageAsync(XamlRoot root, string title, string message)
    {
        var dialog = new ContentDialog { XamlRoot = root, Title = T(title), Content = message, CloseButtonText = T("dialog.ok") };
        await dialog.ShowAsync();
    }
}
