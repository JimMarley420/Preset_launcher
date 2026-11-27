using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace PresetLauncher.Views;

internal static class NativeWindow
{
    public static void ResizeClient(Window window, int width, int height)
    {
        var dpi = GetDpiForWindow(WindowNative.GetWindowHandle(window));
        var scale = dpi == 0 ? 1 : dpi / 96.0;
        var workArea = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var clientWidth = Math.Min((int)(width * scale), (int)(workArea.Width * 0.9));
        var clientHeight = Math.Min((int)(height * scale), (int)(workArea.Height * 0.9));
        window.AppWindow.ResizeClient(new SizeInt32(clientWidth, clientHeight));
    }

    public static void ActivateExisting(Window window)
    {
        var handle = WindowNative.GetWindowHandle(window);
        if (IsIconic(handle))
            ShowWindow(handle, 9); // SW_RESTORE
        window.Activate();

        if (GetForegroundWindow() == handle || SetForegroundWindow(handle))
            return;

        var flash = new FlashInfo
        {
            Size = (uint)Marshal.SizeOf<FlashInfo>(),
            Window = handle,
            Flags = 2, // FLASHW_TRAY
            Count = 3
        };
        FlashWindowEx(ref flash);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FlashInfo info);
}
