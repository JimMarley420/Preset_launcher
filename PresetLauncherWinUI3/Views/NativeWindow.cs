using System.Runtime.InteropServices;
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
        window.AppWindow.ResizeClient(new SizeInt32((int)(width * scale), (int)(height * scale)));
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);
}
