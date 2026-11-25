using System.Diagnostics;
using Newtonsoft.Json;

namespace PresetLauncher.Services;

internal static class AtomicJson
{
    public static void Write(string path, object value, string? previousPath = null, long maxBytes = long.MaxValue)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, Path.GetRandomFileName());
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, leaveOpen: true))
                    JsonSerializer.CreateDefault().Serialize(writer, value);
                if (stream.Length > maxBytes)
                    throw new InvalidDataException("The JSON file exceeds the storage limit.");
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
                File.Replace(temporary, path, previousPath);
            else
                File.Move(temporary, path);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException ex) { Debug.WriteLine(ex.GetType().Name); }
            catch (UnauthorizedAccessException ex) { Debug.WriteLine(ex.GetType().Name); }
        }
    }
}
