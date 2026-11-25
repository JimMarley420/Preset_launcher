using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PresetLauncher.Services;

internal static class ExportPath
{
    public static bool IsProtected(string directory, string path, string storageDirectory, string storage)
    {
        var activeDirectory = Resolve(storageDirectory);
        var targetDirectory = Resolve(directory);
        var root = activeDirectory.TrimEnd('\\') + "\\";
        var security = root + "Security";
        var securityDirectory = Path.Combine(storageDirectory, "Security");
        var resolvedSecurity = Directory.Exists(securityDirectory) ? Resolve(securityDirectory) : security;
        if (IsWithin(targetDirectory, security) || IsWithin(targetDirectory, resolvedSecurity) ||
            Path.GetFileName(path).Contains(':'))
            return true;

        if (targetDirectory.Equals(activeDirectory, StringComparison.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(path).TrimEnd('.', ' ');
            if (name.Equals("settings.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals(Path.GetFileName(storage), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (!File.Exists(path))
            return false;
        using var file = Open(path);
        var finalPath = Resolve(file);
        if (!GetFileInformationByHandle(file, out var information))
            throw Error();
        return information.NumberOfLinks > 1 ||
            IsSameFile(finalPath, storage) ||
            IsSameFile(finalPath, Path.Combine(storageDirectory, "settings.json")) ||
            IsWithin(finalPath, security) || IsWithin(finalPath, resolvedSecurity);
    }

    private static bool IsSameFile(string destination, string protectedPath) =>
        File.Exists(protectedPath) && destination.Equals(Resolve(protectedPath), StringComparison.OrdinalIgnoreCase);

    private static bool IsWithin(string path, string directory) =>
        path.Equals(directory, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(directory + "\\", StringComparison.OrdinalIgnoreCase);

    private static string Resolve(string path)
    {
        using var handle = Open(path);
        return Resolve(handle);
    }

    private static string Resolve(SafeFileHandle handle)
    {
        var buffer = new StringBuilder(512);
        var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        if (length >= buffer.Capacity)
        {
            buffer.EnsureCapacity(checked((int)length + 1));
            length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        }
        if (length == 0 || length >= buffer.Capacity)
            throw Error();
        return buffer.ToString();
    }

    private static SafeFileHandle Open(string path)
    {
        var handle = CreateFile(path, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (!handle.IsInvalid)
            return handle;
        handle.Dispose();
        throw Error();
    }

    private static IOException Error() => new("Could not verify backup destination.", new Win32Exception());

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
        public uint VolumeSerial, SizeHigh, SizeLow, NumberOfLinks, IndexHigh, IndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint length, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
}
