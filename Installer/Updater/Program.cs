using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using PresetLauncher.Services;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is not [var pipe, var parent, ("en" or "fr") and var language] ||
            !Guid.TryParseExact(pipe, "N", out _) || !int.TryParse(parent, out var parentId) || parentId <= 0)
            return 1;

        var localization = new LocalizationService();
        localization.SetLanguage(language);
        var committed = false;
        string? installer = null;
        string? directory = null;
        try
        {
            UpdateInstallation.RequireNonElevated();
            directory = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            if (!directory.Equals(UpdateStaging.Find(Path.GetFileName(directory)), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unexpected helper location.");
            using var worker = new Mutex(false, @"Local\PresetLauncher.Update.Installation");
            try
            {
                if (!worker.WaitOne(0)) return 1;
            }
            catch (AbandonedMutexException)
            {
                // An interrupted update left no live owner.
            }
            try
            {
                return Execute(directory, pipe, parentId, ref committed, ref installer, localization);
            }
            finally { worker.ReleaseMutex(); }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            if (committed) Notify(localization, "update.setupFailed");
            return 1;
        }
        finally
        {
            if (committed && directory is not null)
            {
                try
                {
                    using var pins = UpdateStaging.PinDirectory(directory);
                    if (installer is not null) File.Delete(installer);
                    UpdateStaging.Complete(directory);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
                {
                    Debug.WriteLine(ex);
                }
            }
        }
    }

    private static int Execute(string directory, string pipeId, int parentId, ref bool committed, ref string? installer,
        LocalizationService localization)
    {
        using var deadline = new CancellationTokenSource(UpdateHandoffProtocol.Timeout);
        using var pins = UpdateStaging.PinDirectory(directory);
        using var lease = UpdateStaging.Use(directory);
        using var parent = Process.GetProcessById(parentId);
        _ = parent.SafeHandle;
        var launcher = parent.MainModule?.FileName ?? throw new InvalidDataException("Missing parent image.");
        var architecture = RuntimeInformation.ProcessArchitecture;
        var installation = UpdateInstallation.Find(launcher, architecture);

        using var pipe = new NamedPipeClientStream(".", UpdateStaging.PipePrefix + pipeId, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        pipe.ConnectAsync(10_000, deadline.Token).GetAwaiter().GetResult();
        if (!UpdateStaging.IsServer(pipe, parentId))
            throw new InvalidDataException("Unexpected handoff owner.");
        var bytes = UpdateHandoffProtocol.ReadReleaseAsync(pipe, deadline.Token).GetAwaiter().GetResult();
        var update = UpdateService.ParseArchitectureRelease(Encoding.UTF8.GetString(bytes), installation.Version,
                     UpdatePolicySignature.PublicKey(), architecture, static (_, _) => { })
                 ?? throw new InvalidDataException("Installer is not an upgrade.");

        installer = Path.Combine(directory, UpdateService.InstallerFileName(update));
        using (var verified = UpdateStaging.PinFile(installer))
        {
            if (verified.Length != update.Size ||
                !CryptographicOperations.FixedTimeEquals(SHA256.HashDataAsync(verified, deadline.Token).AsTask().GetAwaiter().GetResult(),
                    Convert.FromHexString(update.Sha256)))
                throw new InvalidDataException("Installer changed after download.");
            var executable = UpdateStaging.InstallerPath(verified, installer);
            UpdateHandoffProtocol.SendAsync(pipe, UpdateHandoffProtocol.Ready, deadline.Token).GetAwaiter().GetResult();
            UpdateHandoffProtocol.ExpectAsync(pipe, UpdateHandoffProtocol.Commit, deadline.Token).GetAwaiter().GetResult();
            UpdateHandoffProtocol.SendAsync(pipe, UpdateHandoffProtocol.Accepted, deadline.Token).GetAwaiter().GetResult();
            committed = true;
            pipe.Dispose();
            if (!parent.WaitForExit(120_000))
            {
                Notify(localization, "update.shutdownFailed");
                return 1;
            }
            if (UpdateInstallation.Find(launcher, architecture) != installation)
                throw new InvalidDataException("Installation registration changed during handoff.");

            int code;
            try
            {
                using var setup = Process.Start(installation.InstallerStartInfo(executable))
                    ?? throw new InvalidOperationException("Setup did not start.");
                setup.WaitForExit();
                code = setup.ExitCode;
            }
            catch (Win32Exception ex)
            {
                Notify(localization, ex.NativeErrorCode == 1223 ? "update.setupCancelled" : "update.setupStartFailed");
                return 1;
            }
            if (UpdateInstallation.ResultKey(code) is { } error)
            {
                Notify(localization, error, code);
                return 1;
            }
        }

        try
        {
            var installed = UpdateInstallation.Find(launcher, architecture);
            if (installed.Version != update.Version || installed.AllUsers != installation.AllUsers)
                throw new InvalidDataException("Setup did not install the expected upgrade.");
            var start = new ProcessStartInfo(installed.LauncherPath)
            {
                WorkingDirectory = installed.Directory,
                UseShellExecute = false
            };
            start.ArgumentList.Add("--update-completed");
            start.ArgumentList.Add(Path.GetFileName(directory));
            start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            using var restarted = Process.Start(start) ?? throw new InvalidOperationException("Launcher did not start.");
            var outcome = UpdateStartup.WaitForLaunch(restarted, installed.LauncherPath, TimeSpan.FromSeconds(30));
            if (outcome is UpdateLaunchResult.Started or UpdateLaunchResult.AlreadyRunning)
                return 0;
            Notify(localization, "update.relaunchUnconfirmed");
            return 1;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            Notify(localization, "update.relaunchFailed");
            return 1;
        }
    }

    private static void Notify(LocalizationService localization, string key, params object[] arguments) =>
        MessageBox(IntPtr.Zero, localization.Get(key, arguments), localization.Get("update.recoveryTitle"), 0x00010030);

    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr window, string text, string caption, uint type);
}
