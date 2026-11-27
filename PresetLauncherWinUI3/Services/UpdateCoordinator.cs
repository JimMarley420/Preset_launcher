using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Views.Pages;

namespace PresetLauncher.Services;

public sealed class UpdateCoordinator
{
    private readonly UpdateService _service = new();
    private bool _checking;
    private static string T(string key) => App.Localization.Get(key);

    public async Task CheckAsync(bool manual, Action<string, InfoBarSeverity>? report = null)
    {
        var window = App.Current.MainWindow;
        if (_checking || window?.Content?.XamlRoot is null)
            return;
        _checking = true;
        var installed = _service.GetInstalledVersion();
        var required = false;
        var closing = false;
        var policyUnavailable = false;
        UpdatePage? page = null;
        try
        {
            if (installed == "0.0.0")
            {
                report?.Invoke("update.sourceBuild", InfoBarSeverity.Informational);
                return;
            }

            var settings = App.Settings.CurrentSettings;
            required = true;
            window.SetUpdateGate(true);
            var store = new UpdatePolicyStore();
            KnownUpdatePolicy known;
            try { known = store.Load(settings.MinimumSupportedVersion); }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                policyUnavailable = true;
                await ExitRecoveryAsync("update.policyErrorMessage");
                return;
            }
            required = _service.IsUpdateAvailable(installed, known.Minimum.ToString(3));
            window.SetUpdateGate(required);

            while (true)
            {
                ReleaseUpdate? offer;
                try
                {
                    offer = await _service.GetLatestUpdateAsync(installed, (version, minimum) =>
                    {
                        window.SetUpdateGate(true);
                        policyUnavailable = true;
                        known = store.Observe(version, minimum);
                        policyUnavailable = false;
                        required = _service.IsUpdateAvailable(installed, known.Minimum.ToString(3));
                        window.SetUpdateGate(required);
                    });
                    if (required && (offer is null || offer.Version < known.Minimum))
                        throw new InvalidDataException("No installer meets the protected minimum version.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    var key = ex is UpdateAssetException ? "update.architectureAssetUnavailable" : "settings.updateCheckFailed";
                    if (policyUnavailable)
                        await ExitRecoveryAsync("update.policyErrorMessage");
                    else if (required)
                    {
                        var recovery = ShowPage(null);
                        var action = await recovery.PromptAsync(T(key) + "\n\n" + T("update.requiredRetry"),
                            ("update.retry", UpdateAction.Retry), ("update.exit", UpdateAction.Exit));
                        if (action == UpdateAction.Retry || !await TryExitAsync())
                            continue;
                    }
                    else report?.Invoke(key, InfoBarSeverity.Error);
                    return;
                }

                if (offer is null)
                {
                    report?.Invoke("settings.updateUpToDate", InfoBarSeverity.Success);
                    return;
                }
                if (!required && !UpdateService.ShouldPrompt(installed, offer, settings.SkippedUpdateVersion, manual,
                    settings.DeferredUpdateVersion, settings.DeferredUpdateUntil))
                    return;

                var screen = ShowPage(offer);
                var recommended = !required && offer.Policy == UpdatePolicy.Recommended;
                var secondary = required ? ("update.exit", UpdateAction.Exit) :
                    recommended ? ("update.remindTomorrow", UpdateAction.Postpone) : ("update.later", UpdateAction.Postpone);
                while (true)
                {
                    var choice = await screen.PromptAsync(null, ("update.install", UpdateAction.Install), secondary,
                        skip: !required && !recommended);
                    if (choice == UpdateAction.Install)
                        break;
                    if (choice == UpdateAction.Exit)
                    {
                        if (await TryExitAsync()) return;
                        continue;
                    }
                    if (choice == UpdateAction.Skip || recommended)
                    {
                        try { SaveDismissal(offer, choice); }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                            report?.Invoke("settings.saveFailed", InfoBarSeverity.Error);
                        }
                    }
                    return;
                }

                while (true)
                {
                    var error = await DownloadAndInstallAsync(window, screen, offer);
                    if (error is null)
                    {
                        closing = true;
                        window.CloseForUpdate();
                        return;
                    }
                    while (true)
                    {
                        var next = await screen.PromptAsync(T(error), ("update.retry", UpdateAction.Retry),
                            required ? ("update.exit", UpdateAction.Exit) : ("update.later", UpdateAction.Postpone));
                        if (next == UpdateAction.Retry) break;
                        if (next == UpdateAction.Postpone || await TryExitAsync()) return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            if (required || policyUnavailable)
            {
                window.SetUpdateGate(true);
                try { await ExitRecoveryAsync("update.policyErrorMessage"); }
                catch (Exception failure)
                {
                    Debug.WriteLine(failure);
                    closing = true;
                    window.CloseForUpdate();
                }
            }
            else report?.Invoke("settings.updateCheckFailed", InfoBarSeverity.Error);
        }
        finally
        {
            if (!closing && !required && !policyUnavailable) window.ShowUpdate(null);
            _checking = false;
        }

        UpdatePage ShowPage(ReleaseUpdate? offer)
        {
            page ??= new UpdatePage();
            page.ShowOffer(installed, offer, required || policyUnavailable);
            window.ShowUpdate(page);
            return page;
        }

        async Task<bool> TryExitAsync()
        {
            if (!await window.CanCloseForUpdateAsync())
                return false;
            closing = true;
            window.CloseForUpdate();
            return true;
        }

        async Task ExitRecoveryAsync(string key)
        {
            var recovery = ShowPage(null);
            do
            {
                await recovery.PromptAsync(T(key), null, ("update.exit", UpdateAction.Exit));
            } while (!await TryExitAsync());
        }
    }

    private static void SaveDismissal(ReleaseUpdate offer, UpdateAction action)
    {
        var version = offer.Version.ToString(3);
        App.Settings.Update(settings =>
        {
            if (action == UpdateAction.Skip)
                settings.SkippedUpdateVersion = version;
            else
            {
                settings.DeferredUpdateVersion = version;
                settings.DeferredUpdateUntil = DateTimeOffset.UtcNow.AddHours(24);
            }
        });
    }

    private async Task<string?> DownloadAndInstallAsync(MainWindow window, UpdatePage screen, ReleaseUpdate offer)
    {
        DirectoryInfo? directory = null;
        var handedOff = false;
        using var cancel = new CancellationTokenSource();
        var error = "settings.updateDownloadError";
        try
        {
            if (!await window.CanCloseForUpdateAsync())
                return "update.shutdownPostponed";
            error = "update.installedCopyRequired";
            UpdateHandoff.CheckInstallation();
            error = "settings.updateDownloadError";
            screen.ShowProgress("settings.updatePreparing", Cancel);
            directory = UpdateStaging.Create();
            using var pins = UpdateStaging.PinDirectory(directory.FullName);
            using var lease = UpdateStaging.Use(directory.FullName);
            var lastPercent = -1;
            await _service.DownloadVerifiedAsync(offer, directory, progress =>
            {
                if (cancel.IsCancellationRequested) return;
                var percent = (int)(progress.Transferred * 100 / progress.Total);
                if (progress.Verifying)
                    screen.ShowProgress("update.verifying", Cancel);
                else if (percent != lastPercent)
                {
                    lastPercent = percent;
                    screen.ShowProgress("update.downloading", Cancel, progress);
                }
            }, cancel.Token);
            cancel.Token.ThrowIfCancellationRequested();
            error = "update.handoffFailed";
            screen.ShowProgress("update.preparingInstallation");
            await UpdateHandoff.StartAsync(offer, directory.FullName);
            handedOff = true;
            try { screen.ShowProgress("update.handoff"); }
            catch (Exception ex) { Debug.WriteLine(ex); }
            return null;
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            return "update.downloadCancelled";
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return error;
        }
        finally
        {
            if (!handedOff && directory is not null) UpdateStaging.Clean(directory.FullName);
        }

        void Cancel()
        {
            cancel.Cancel();
            screen.ShowProgress("update.cancelling");
        }
    }
}
