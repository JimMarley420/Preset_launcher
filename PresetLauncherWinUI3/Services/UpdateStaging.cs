using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PresetLauncher.Services;

internal static class UpdateStaging
{
    internal const string HelperName = "PresetLauncher.Update.exe";
    private const string Prefix = "PresetLauncher-handoff-";
    private const string Finished = "finished";
    private const string Lease = "active";

    internal static DirectoryInfo Create() => Directory.CreateTempSubdirectory(Prefix);

    internal static string Find(string name)
    {
        if (!name.StartsWith(Prefix, StringComparison.Ordinal) || name.Length <= Prefix.Length || name.Length > 64 ||
            name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '.'))
            throw new InvalidDataException("Invalid update workspace.");
        return Path.Combine(Path.GetTempPath(), name);
    }

    internal static IDisposable PinDirectory(string path) => new DirectoryPins(path);

    internal static FileStream Use(string directory) =>
        new(Path.Combine(directory, Lease), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

    internal static FileStream PinFile(string path, FileMode mode = FileMode.Open)
    {
        var handle = CreateFile(path, 0x80000000, FileShare.Read, IntPtr.Zero, mode,
            0x00200000, IntPtr.Zero); // GENERIC_READ, OPEN_REPARSE_POINT
        try
        {
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (File.GetAttributes(handle).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("Update files cannot be reparse points.");
            return new FileStream(handle, FileAccess.Read);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static string InstallerPath(FileStream pinned, string expected)
    {
        var name = new StringBuilder(32_768);
        var length = GetFinalPathNameByHandle(pinned.SafeFileHandle, name, name.Capacity, 0);
        if (length == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (length >= name.Capacity ||
            !name.ToString().Equals(@"\\?\" + Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The installer path must resolve to the pinned local file.");
        return name.ToString(4, (int)length - 4);
    }

    internal static void Clean(string directory)
    {
        try
        {
            using (PinDirectory(directory))
            {
                var lease = Path.Combine(directory, Lease);
                using (new FileStream(lease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    foreach (var file in Directory.EnumerateFiles(directory, "PresetLauncher_Setup_*.exe"))
                        File.Delete(file);
                    File.Delete(Path.Combine(directory, HelperName));
                    File.Delete(Path.Combine(directory, Finished));
                }
                File.Delete(lease);
            }
            Directory.Delete(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            Debug.WriteLine(ex);
        }
    }

    internal static async Task CleanOldAsync(string[] args)
    {
        if (args is [_, "--update-completed", var name, var processId] &&
            int.TryParse(processId, out var pid) && pid > 0)
        {
            try
            {
                var directory = Find(name);
                using var worker = Process.GetProcessById(pid);
                if (string.Equals(worker.MainModule?.FileName, Path.Combine(directory, HelperName), StringComparison.OrdinalIgnoreCase))
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    await worker.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException or Win32Exception or OperationCanceledException)
            {
                Debug.WriteLine(ex);
            }
        }

        try
        {
            var abandonedBefore = DateTime.UtcNow.AddDays(-1);
            foreach (var directory in Directory.EnumerateDirectories(Path.GetTempPath(), Prefix + "*").Take(100))
            {
                if (File.Exists(Path.Combine(directory, Finished)) ||
                    Directory.GetCreationTimeUtc(directory) < abandonedBefore)
                    Clean(Find(Path.GetFileName(directory)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine(ex);
        }
    }

    private sealed class DirectoryPins : IDisposable
    {
        private readonly List<SafeFileHandle> _handles = [];

        internal DirectoryPins(string path)
        {
            var full = Path.GetFullPath(path);
            var volume = Path.GetPathRoot(full);
            if (volume is null || volume.Length != 3 || volume[1] != ':')
                throw new InvalidDataException("Updates require a local staging volume.");
            var current = volume;
            try
            {
                // Lock each name before opening its child so the installer path cannot be redirected.
                foreach (var part in full[volume.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
                {
                    current = Path.Combine(current, part);
                    var handle = CreateFile(current, 1, FileShare.Read, IntPtr.Zero, FileMode.Open,
                        0x02000000 | 0x00200000, IntPtr.Zero); // BACKUP_SEMANTICS | OPEN_REPARSE_POINT
                    _handles.Add(handle);
                    if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
                    if (File.GetAttributes(handle).HasFlag(FileAttributes.ReparsePoint))
                        throw new InvalidDataException("Update paths cannot traverse reparse points.");
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            foreach (var handle in _handles.AsEnumerable().Reverse()) handle.Dispose();
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string fileName, uint access, FileShare shareMode,
        IntPtr security, FileMode disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder name, int capacity, uint flags);


}
