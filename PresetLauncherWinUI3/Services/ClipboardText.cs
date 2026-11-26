using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;

namespace PresetLauncher.Services;

internal static class ClipboardText
{
    private const int ClipboardCannotOpen = unchecked((int)0x800401D0);

    public static async Task<bool> CopyAsync(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        if (!await TrySetAsync(() => Clipboard.SetContentWithOptions(package, null)))
            return false;

        try
        {
            Clipboard.Flush();
        }
        catch (Exception ex) when (ex.HResult == ClipboardCannotOpen)
        {
            // The package was already accepted. Flush only persists it after the app closes.
            Debug.WriteLine(ex.HResult);
        }
        return true;
    }

    internal static async Task<bool> TrySetAsync(Func<bool> trySet)
    {
        for (int delay = 40; delay <= 320; delay *= 2)
        {
            if (trySet())
                return true;
            await Task.Delay(delay);
        }
        return trySet();
    }
}
