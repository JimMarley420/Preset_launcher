using System.ComponentModel;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PresetLauncher.Services;

namespace PresetLauncher.Tests;

[TestClass]
public sealed class UpdateStartupTests
{
    [TestMethod]
    public void ApplicationLeasesShareAccessAndExcludeTheInstallerInBothDirections()
    {
        var workspace = UpdateStaging.Create();
        var path = Path.Combine(workspace.FullName, UpdateStartup.LockFileName);
        try
        {
            using (UpdateStartup.OpenApplicationLease(workspace.FullName))
            using (UpdateStartup.OpenApplicationLease(workspace.FullName))
                Assert.ThrowsExactly<IOException>(() => File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None));

            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var sharing = Assert.ThrowsExactly<Win32Exception>(() => UpdateStartup.OpenApplicationLease(workspace.FullName));
                Assert.AreEqual(32, sharing.NativeErrorCode);
            }

            using var afterSetup = UpdateStartup.OpenApplicationLease(workspace.FullName);
            Assert.IsTrue(afterSetup.CanRead);
        }
        finally
        {
            File.Delete(path);
            workspace.Delete();
        }
    }

    [TestMethod]
    public void StartupNeedsReadinessAndRecognizesAnExistingReadyInstanceAfterSuccessfulRedirect()
    {
        using var current = Process.GetCurrentProcess();
        var path = current.MainModule!.FileName;
        using var ready = UpdateStartup.OpenReadyEvent(current);
        Assert.AreEqual(UpdateLaunchResult.Unconfirmed, UpdateStartup.WaitForLaunch(current, path, TimeSpan.Zero));
        ready.Set();
        Assert.AreEqual(UpdateLaunchResult.Started, UpdateStartup.WaitForLaunch(current, path, TimeSpan.Zero));

        using var redirected = ExitProcess(0);
        Assert.AreEqual(UpdateLaunchResult.AlreadyRunning, UpdateStartup.WaitForLaunch(redirected, path, TimeSpan.FromSeconds(5)));
        using var failed = ExitProcess(1);
        Assert.AreEqual(UpdateLaunchResult.Unconfirmed, UpdateStartup.WaitForLaunch(failed, path, TimeSpan.FromSeconds(5)));
        ready.Reset();
        Assert.AreEqual(UpdateLaunchResult.Unconfirmed, UpdateStartup.WaitForLaunch(redirected, path, TimeSpan.Zero));
    }

    private static Process ExitProcess(int exitCode)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("/d");
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add($"exit {exitCode}");
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start test process.");
    }

}
