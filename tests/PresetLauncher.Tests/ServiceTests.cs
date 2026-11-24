using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PresetLauncher.Services;

[assembly: DoNotParallelize]

namespace PresetLauncher.Tests;

[TestClass]
public sealed class ServiceTests
{
    [TestMethod]
    public void PasswordHashUsesRandomSaltAndRejectsWrongPassword()
    {
        var first = PasswordService.HashPassword("example password");
        var second = PasswordService.HashPassword("example password");
        Assert.AreNotEqual(first, second);
        Assert.IsTrue(PasswordService.VerifyPassword("example password", first));
        Assert.IsTrue(PasswordService.VerifyPassword("example password", second));
        Assert.IsFalse(PasswordService.VerifyPassword("wrong password", first));
    }

    [TestMethod]
    public void LegacySha256HashStillVerifies()
    {
        var legacyHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("legacy")));
        Assert.IsTrue(PasswordService.VerifyPassword("legacy", legacyHash));
        Assert.IsFalse(PasswordService.VerifyPassword("incorrect", legacyHash));
        Assert.IsFalse(PasswordService.VerifyPassword("legacy", "not valid base64!"));
    }

    [TestMethod]
    [DataRow("1.2.6", "1.2.7", true)]
    [DataRow("1.2.9", "1.2.10", true)]
    [DataRow("1.2.6", "1.3.0", true)]
    [DataRow("1.2.6", "2.0.0", true)]
    [DataRow("1.2.6", "1.2.6", false)]
    [DataRow("1.2.6", "1.2.5", false)]
    [DataRow("1.2.6", "invalid", false)]
    public void ExistingUpdaterComparesNumericVersions(string installed, string latest, bool expected)
    {
        Assert.AreEqual(expected, new UpdateService().IsUpdateAvailable(installed, latest));
    }

    [TestMethod]
    public void LanguageSwitchAndFallback()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("fr-CH");
        Assert.AreEqual("Lancer", localization.Get("preset.launch"));
        localization.SetLanguage("en-US");
        Assert.AreEqual("Launch", localization.Get("preset.launch"));
        localization.SetLanguage("unknown");
        Assert.AreEqual("Launch", localization.Get("preset.launch"));
        Assert.AreEqual("test.nonexistent.key", localization.Get("test.nonexistent.key"));
    }

    [TestMethod]
    public void RunningProcessDiscoveryCanResolveCurrentTestHost()
    {
        using var current = Process.GetCurrentProcess();
        var launcher = new LauncherService();
        Assert.IsTrue(launcher.GetRunningProcessSummaries().Any(p => p.Id == current.Id));
        Assert.IsTrue(launcher.TryGetExecutablePath(current.Id, out var path));
        Assert.IsTrue(File.Exists(path));
        Assert.IsTrue(string.Equals(current.MainModule!.FileName, path, StringComparison.OrdinalIgnoreCase));
    }
}
