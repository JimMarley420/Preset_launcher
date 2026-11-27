using System.Diagnostics;
using System.Security.Cryptography;

namespace PresetLauncher.Services;

internal static class InteractiveUpdateInstaller
{
    internal static ProcessStartInfo StartInfo(string installer) => new(installer)
    {
        UseShellExecute = true,
        WorkingDirectory = Environment.SystemDirectory
    };

    internal static async Task OpenVerifiedAsync(string installer, ReleaseUpdate offer)
    {
        using var pinned = UpdateStaging.PinFile(installer);
        if (pinned.Length != offer.Size ||
            !CryptographicOperations.FixedTimeEquals(await SHA256.HashDataAsync(pinned),
                Convert.FromHexString(offer.Sha256)))
            throw new InvalidDataException("Installer changed after download.");

        var executable = UpdateStaging.InstallerPath(pinned, installer);
        // Setup uses its normal wizard and previous installation settings.
        using var process = Process.Start(StartInfo(executable))
            ?? throw new InvalidOperationException("Setup did not start.");
    }
}
