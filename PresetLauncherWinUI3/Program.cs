using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using PresetLauncher.Views;

namespace PresetLauncher;

internal static class Program
{
    private const string InstanceKey = "PresetLauncher.Main";
    private static DispatcherQueue? _dispatcher;
    private static MainWindow? _window;
    private static int _activationPending;

    [STAThread]
    private static int Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        var current = AppInstance.GetCurrent();

        // Subscribe before publishing the key so early redirects cannot be lost.
        current.Activated += OnActivated;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var instance = AppInstance.FindOrRegisterForKey(InstanceKey);
            if (instance is null)
                continue;

            if (instance.IsCurrent)
            {
                try
                {
                    Application.Start(initialization =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                        _ = new App();
                    });
                    return 0;
                }
                finally
                {
                    current.Activated -= OnActivated;
                }
            }

            if (TryRedirect(current, instance))
                return 0;

            // Only retry election if the previous owner is gone.
            if (IsRunning(instance.ProcessId))
                return 1;
        }

        return 1;
    }

    private static bool TryRedirect(AppInstance current, AppInstance target)
    {
        try
        {
            var args = current.GetActivatedEventArgs();
            AllowSetForegroundWindow(target.ProcessId);
            var redirect = Task.Run(async () => await target.RedirectActivationToAsync(args));

            // The STA must keep servicing COM calls while the SDK redirects activation.
            var handle = ((IAsyncResult)redirect).AsyncWaitHandle.SafeWaitHandle;
            var result = CoWaitForMultipleObjects(0, 10_000, 1, [handle.DangerousGetHandle()], out _);
            GC.KeepAlive(redirect);
            if (result != 0)
            {
                Debug.WriteLine($"Activation redirection wait failed: 0x{result:X8}");
                return false;
            }

            redirect.GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }

    private static bool IsRunning(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        Interlocked.Exchange(ref _activationPending, 1);
        Volatile.Read(ref _dispatcher)?.TryEnqueue(ActivatePendingWindow);
    }

    internal static void AttachWindow(MainWindow window)
    {
        _window = window;
        window.Closed += (_, _) =>
        {
            Volatile.Write(ref _dispatcher, null);
            _window = null;
        };
        Volatile.Write(ref _dispatcher, window.DispatcherQueue);
        ActivatePendingWindow();
    }

    private static void ActivatePendingWindow()
    {
        if (_window is { } window && Interlocked.Exchange(ref _activationPending, 0) != 0)
            NativeWindow.ActivateExisting(window);
    }

    [DllImport("ole32.dll")]
    private static extern int CoWaitForMultipleObjects(uint flags, uint milliseconds, uint count,
        IntPtr[] handles, out uint index);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
