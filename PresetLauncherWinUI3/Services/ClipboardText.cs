using System.Runtime.InteropServices;

namespace PresetLauncher.Services;

internal enum ClipboardCopyResult { Persisted, Busy, Changed, Failed }

internal static class ClipboardText
{
    private const uint UnicodeText = 13; // CF_UNICODETEXT
    private const uint MoveableMemory = 0x0002; // GMEM_MOVEABLE

    public static async Task<ClipboardCopyResult> CopyAsync(nint window, string text)
    {
        var sequence = GetClipboardSequenceNumber();
        var owner = GetClipboardOwner();
        var characters = (text + '\0').ToCharArray();
        var memory = GlobalAlloc(MoveableMemory, checked((nuint)characters.Length * sizeof(char)));
        if (memory == 0)
            return ClipboardCopyResult.Failed;

        try
        {
            var buffer = GlobalLock(memory);
            if (buffer == 0)
                return ClipboardCopyResult.Failed;
            try { Marshal.Copy(characters, 0, buffer, characters.Length); }
            finally { GlobalUnlock(memory); }

            var result = await TrySetAsync(() => TryPublish(window, sequence, owner, memory));
            if (result == ClipboardCopyResult.Persisted)
                memory = 0; // SetClipboardData transferred ownership to Windows.
            return result;
        }
        finally
        {
            if (memory != 0)
                GlobalFree(memory);
        }
    }

    private static ClipboardCopyResult TryPublish(nint window, uint sequence, nint owner, nint memory)
    {
        if (!OpenClipboard(window))
            return ClipboardCopyResult.Busy;

        try
        {
            // Check under the clipboard lock so another app cannot copy between the check and write.
            if (GetClipboardSequenceNumber() != sequence || GetClipboardOwner() != owner)
                return ClipboardCopyResult.Changed;

            // Supply the text now, so Windows can keep it after this process exits without a Flush call.
            return EmptyClipboard() && SetClipboardData(UnicodeText, memory) != 0
                ? ClipboardCopyResult.Persisted : ClipboardCopyResult.Failed;
        }
        finally { CloseClipboard(); }
    }

    internal static async Task<ClipboardCopyResult> TrySetAsync(Func<ClipboardCopyResult> trySet)
    {
        for (int delay = 40; delay <= 320; delay *= 2)
        {
            var result = trySet();
            if (result != ClipboardCopyResult.Busy)
                return result;
            await Task.Delay(delay);
        }
        return trySet();
    }

    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(nint window);
    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();
    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();
    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")]
    private static extern nint GetClipboardOwner();
    [DllImport("user32.dll")]
    private static extern nint SetClipboardData(uint format, nint memory);
    [DllImport("kernel32.dll")]
    private static extern nint GlobalAlloc(uint flags, nuint bytes);
    [DllImport("kernel32.dll")]
    private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(nint memory);
    [DllImport("kernel32.dll")]
    private static extern nint GlobalFree(nint memory);
}
