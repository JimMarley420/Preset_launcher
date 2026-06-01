using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PresetLauncher.Services;

public class UpdateService
{
    private const string VersionUrl = "https://version.multichat.cloud/PresetLauncher/version.txt";
    private const string SetupUrlTemplate = "https://version.multichat.cloud/PresetLauncher/PresetLauncher_Setup_{0}.exe";
    private const string InnoKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C4A7B8D2-E3F5-4901-A2B3-C4D5E6F70819}_is1";

    public string GetInstalledVersion()
    {
        using var hkcu = Registry.CurrentUser.OpenSubKey(InnoKey);
        if (hkcu?.GetValue("DisplayVersion") is string v1 && !string.IsNullOrWhiteSpace(v1))
            return v1.Trim();

        using var hklm = Registry.LocalMachine.OpenSubKey(InnoKey);
        if (hklm?.GetValue("DisplayVersion") is string v2 && !string.IsNullOrWhiteSpace(v2))
            return v2.Trim();

        // Fallback for dev builds not installed via InnoSetup
#if DEBUG
        return System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";
#else
        return "0.0.0";
#endif
    }

    public async Task<string> GetLatestVersionAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var text = await client.GetStringAsync(VersionUrl);
        return text.Trim();
    }

    public bool IsUpdateAvailable(string installed, string latest)
    {
        return TryNormalize(installed, out var v1)
            && TryNormalize(latest, out var v2)
            && v2 > v1;
    }

    public async Task DownloadAndInstallAsync(string version, IProgress<int>? progress, CancellationToken ct = default)
    {
        var url = string.Format(SetupUrlTemplate, version);
        var tempPath = Path.Combine(Path.GetTempPath(), $"PresetLauncher_Setup_{version}.exe");

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[65536];
        long downloaded = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;
            if (total > 0) progress?.Report((int)(downloaded * 100 / total));
        }

        file.Close();
        Process.Start(new ProcessStartInfo { FileName = tempPath, UseShellExecute = true });
        Environment.Exit(0);
    }

    private static bool TryNormalize(string input, out Version version)
    {
        var parts = input.Trim().Split('.');
        var normalized = parts.Length switch
        {
            1 => input + ".0.0.0",
            2 => input + ".0.0",
            3 => input + ".0",
            _ => input
        };
        return Version.TryParse(normalized, out version!);
    }
}
