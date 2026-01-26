using System;
using System.Runtime.InteropServices;
using Windows.System;

namespace PresetLauncher.Services;

// Helper from Microsoft samples to ensure DispatcherQueue for SystemBackdrops
internal class WindowsSystemDispatcherQueueHelper
{
    [StructLayout(LayoutKind.Sequential)]
    struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, out IntPtr dispatcherQueueController);

    private IntPtr _dispatcherQueueControllerPtr;

    public void EnsureWindowsSystemDispatcherQueueController()
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
        {
            return;
        }

        var options = new DispatcherQueueOptions
        {
            dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions)),
            threadType = 2, // DQTYPE_THREAD_CURRENT
            apartmentType = 2 // DQTAT_COM_STA
        };

        _ = CreateDispatcherQueueController(options, out _dispatcherQueueControllerPtr);
    }
}
