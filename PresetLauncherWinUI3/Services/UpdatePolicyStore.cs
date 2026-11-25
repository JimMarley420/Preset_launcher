using System.Security.Cryptography;
using System.Text.Json;

namespace PresetLauncher.Services;

public sealed record KnownUpdatePolicy(Version Minimum, Version Latest);

public sealed class UpdatePolicyStore
{
    private readonly string _path;
    private readonly string _lockPath;
    public UpdatePolicyStore(string? directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PresetLauncher", "Security");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "update-policy.dat");
        _lockPath = Path.Combine(directory, "update-policy.lock");
    }

    // An exclusive file handle serializes read/modify/write across app instances.
    private FileStream Lock() => new(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    public KnownUpdatePolicy Load(string legacyMinimum)
    {
        using var guard = Lock();
        if (File.Exists(_path)) return Read();
        var floor = UpdateService.TryVersion(legacyMinimum, out var parsed) ? parsed : new Version(0, 0, 0);
        var state = new KnownUpdatePolicy(floor, floor);
        Write(state);
        return state;
    }

    public KnownUpdatePolicy Observe(ReleaseUpdate update)
    {
        using var guard = Lock();
        // A missing file after initialization is not silently accepted.
        var state = Read();
        if (update.Version < state.Latest || update.MinimumSupportedVersion < state.Minimum)
            throw new InvalidDataException("Update policy rollback detected.");
        var next = new KnownUpdatePolicy(update.MinimumSupportedVersion, update.Version);
        if (next != state) Write(next);
        return next;
    }

    private KnownUpdatePolicy Read()
    {
        var file = new FileInfo(_path);
        if (!file.Exists || file.Length is <= 0 or > 16384)
            throw new InvalidDataException("Missing or invalid update policy state.");
        var bytes = ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.CurrentUser);
        var state = JsonSerializer.Deserialize<KnownUpdatePolicy>(bytes)
            ?? throw new InvalidDataException("Invalid update policy state.");
        if (state.Minimum is null || state.Latest is null ||
            !UpdateService.TryVersion(state.Minimum.ToString(3), out _) ||
            !UpdateService.TryVersion(state.Latest.ToString(3), out _) || state.Minimum > state.Latest)
            throw new InvalidDataException("Invalid update policy versions.");
        return state;
    }

    private void Write(KnownUpdatePolicy state)
    {
        var bytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(state), null, DataProtectionScope.CurrentUser);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
