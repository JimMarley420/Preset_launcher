using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PresetLauncher.Services;

namespace PresetLauncher;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        // Refuse startup before WinUI initializes, so setup receives a normal exit code.
        try { UpdateStartup.EnterApplication(AppContext.BaseDirectory); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 32)
        {
            return UpdateStartup.InstallationBusyExitCode;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            Debug.WriteLine(ex);
            return 1;
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(initialization =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
        return 0;
    }
}
