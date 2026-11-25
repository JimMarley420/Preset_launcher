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

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);
}
