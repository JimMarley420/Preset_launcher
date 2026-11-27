using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace PresetLauncher.Services;

internal enum UpdateLaunchResult { Unconfirmed, Started, AlreadyRunning }

internal static class UpdateStartup
{
    // Same file and sharing contract as Installer/PresetLauncher.iss. The setup
    // process holds the exclusive handle throughout file replacement.
    internal const string LockFileName = ".presetlauncher-install.lock";
    internal const int InstallationBusyExitCode = 1618;
    private static FileStream? _applicationLease;
    private static EventWaitHandle? _ready;

    internal static FileStream OpenApplicationLease(string directory)
    {
        var path = Path.Combine(directory, LockFileName);
        try { return UpdateStaging.PinFile(path); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            return UpdateStaging.PinFile(path, FileMode.OpenOrCreate);
        }
    }

    internal static void EnterApplication(string directory) => _applicationLease = OpenApplicationLease(directory);

    internal static void SignalReady()
    {
        if (_applicationLease is null)
            throw new InvalidOperationException("Application startup requires an installation lease.");
        using var current = Process.GetCurrentProcess();
        (_ready ??= OpenReadyEvent(current)).Set();
    }

    internal static UpdateLaunchResult WaitForLaunch(Process launched, string launcherPath, TimeSpan timeout)
    {
        var elapsed = Stopwatch.StartNew();
        if (WaitForReady(launched, timeout))
            return UpdateLaunchResult.Started;
        if (!launched.HasExited || launched.ExitCode != 0)
            return UpdateLaunchResult.Unconfirmed;

        // AppInstance can redirect activation and exit successfully without creating
        // a window. Require a ready process of the expected installation in this session.
        using var helper = Process.GetCurrentProcess();
        var candidates = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(launcherPath));
        try
        {
            foreach (var candidate in candidates)
            {
                try
                {
                    if (candidate.Id != launched.Id && candidate.SessionId == helper.SessionId &&
                        string.Equals(candidate.MainModule?.FileName, launcherPath, StringComparison.OrdinalIgnoreCase) &&
                        WaitForReady(candidate, timeout - elapsed.Elapsed))
                        return UpdateLaunchResult.AlreadyRunning;
                }
                catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or Win32Exception)
                {
                    Debug.WriteLine(ex);
                }
            }
        }
        finally
        {
            foreach (var candidate in candidates) candidate.Dispose();
        }
        return UpdateLaunchResult.Unconfirmed;
    }

    private static bool WaitForReady(Process process, TimeSpan timeout)
    {
        if (process.HasExited || timeout < TimeSpan.Zero)
            return false;
        try
        {
            var processHandle = process.SafeHandle;
            using var exited = new ProcessExitHandle(processHandle);
            using var ready = OpenReadyEvent(process);
            var signaled = WaitHandle.WaitAny([ready, exited], timeout) == 0;
            GC.KeepAlive(processHandle);
            return signaled && !process.HasExited;
        }
        catch (Exception ex) when ((ex is InvalidOperationException or Win32Exception) && process.HasExited)
        {
            return false;
        }
    }

    internal static EventWaitHandle OpenReadyEvent(Process process) =>
        new(false, EventResetMode.ManualReset, FormattableString.Invariant(
            $@"Local\PresetLauncher.Startup.v1.{process.Id}.{process.StartTime.ToFileTimeUtc()}"));

    private sealed class ProcessExitHandle : WaitHandle
    {
        internal ProcessExitHandle(SafeProcessHandle process) =>
            SafeWaitHandle = new SafeWaitHandle(process.DangerousGetHandle(), ownsHandle: false);
    }
}
