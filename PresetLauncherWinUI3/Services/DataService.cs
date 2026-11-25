using System.Diagnostics;
using System.Text.Json;
using Newtonsoft.Json;
using PresetLauncher.Models;

namespace PresetLauncher.Services;

public class DataService
{
    private readonly object _sync = new();
    private bool _loaded;
    public AppData Data { get; private set; } = new();
    public bool LoadFailed { get; private set; }
    public string StoragePath { get; }

    public DataService(string? dataPath = null)
    {
        StoragePath = Path.GetFullPath(dataPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PresetLauncher", "data.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
    }

    public void Load()
    {
        lock (_sync)
        {
            try
            {
                try { Data = ReadBackup(StoragePath); }
                catch (FileNotFoundException)
                {
                    var empty = new AppData();
                    AtomicJson.Write(StoragePath, empty);
                    Data = empty;
                }
                LoadFailed = false;
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                LoadFailed = true;
                Debug.WriteLine(ex.GetType().Name);
            }
            _loaded = true;
        }
    }

    public static bool IsFileError(Exception ex) =>
        ex is IOException or InvalidDataException or UnauthorizedAccessException or Newtonsoft.Json.JsonException or System.Text.Json.JsonException or ArgumentException or OverflowException;

    public static AppData ReadBackup(string path)
    {
        using var document = DataBackup.Read(path);
        return DataBackup.Parse(document.RootElement);
    }

    public void Save()
    {
        lock (_sync)
        {
            EnsureWritable();
            Commit(Data);
        }
    }

    public void AddPreset(Preset preset) => Change(next => next.Presets.Add(preset));

    public void RemovePreset(Preset preset) => Change(next =>
    {
        var current = next.Presets.FirstOrDefault(p => p.Id == preset.Id);
        if (current is not null)
            next.Presets.Remove(current);
    });

    public void SaveDraft(PresetDraft draft) => Change(next =>
    {
        var current = next.Presets.FirstOrDefault(p => p.Id == draft.PresetId);
        var edited = draft.Apply(current);
        if (current is null)
            next.Presets.Add(edited);
        else
            next.Presets[next.Presets.IndexOf(current)] = edited;
    });

    internal void RecordLaunch(Preset launched, IEnumerable<string> startedPaths, ExecutionHistory? history, int historyLimit)
    {
        Change(next =>
        {
            var preset = next.Presets.First(p => p.Id == launched.Id);
            next.Presets[next.Presets.IndexOf(preset)] = launched;
            foreach (var path in startedPaths)
                IncrementUsage(next, path);
            if (history is not null)
            {
                next.History.Insert(0, history);
                var maximum = Math.Max(0, historyLimit);
                if (next.History.Count > maximum)
                    next.History.RemoveRange(maximum, next.History.Count - maximum);
            }
        });
    }

    public void ClearHistory() => Change(next => next.History.Clear());

    public void UpdateUsageStats(string exePath) => Change(next => IncrementUsage(next, exePath));

    private static void IncrementUsage(AppData next, string path)
    {
        var count = next.UsageStats.GetValueOrDefault(path);
        next.UsageStats[path] = count == int.MaxValue ? count : count + 1;
    }

    public string ExportPreset(Preset preset) => JsonConvert.SerializeObject(preset, Formatting.Indented);

    public Preset? ImportPresetFile(string path)
    {
        try
        {
            using var document = DataBackup.Read(path);
            return PreparePreset(document.RootElement);
        }
        catch (Exception ex) when (IsFileError(ex)) { return null; }
    }

    public Preset? ImportPreset(string json)
    {
        try
        {
            using var document = DataBackup.ReadText(json);
            return PreparePreset(document.RootElement);
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            return null;
        }
    }

    private static Preset PreparePreset(JsonElement root)
    {
        DataBackup.ValidatePreset(root);
        var preset = root.Deserialize<Preset>()!;
        // Program IDs are scoped to a preset. A single-preset import gets a new preset identity.
        preset.Id = Guid.NewGuid().ToString();
        preset.CreatedAt = DateTime.Now;
        preset.ValidateAllPaths();
        return preset;
    }

    public sealed record MissingReference(string PresetId, string PresetName, string ProgramId, string ProgramName, string Path);

    public List<MissingReference> FindMissingExecutables()
    {
        lock (_sync)
            return (from preset in Data.Presets
                    from program in preset.Executables
                    where !File.Exists(program.Path)
                    select new MissingReference(preset.Id, preset.Name, program.Id, program.Name, program.Path)).ToList();
    }

    public int CleanupInvalidExecutables() => CleanupInvalidExecutables(FindMissingExecutables());

    public int CleanupInvalidExecutables(IEnumerable<MissingReference> reviewed)
    {
        lock (_sync)
        {
            EnsureWritable();
            var matches = reviewed.Select(entry => (entry.PresetId, entry.ProgramId, entry.Path)).ToHashSet();
            var next = Snapshot();
            int removed = 0;
            for (int i = 0; i < next.Presets.Count; i++)
            {
                var source = next.Presets[i];
                var copy = PresetDraft.Copy(source);
                foreach (var item in copy.Executables.ToArray())
                {
                    item.ValidatePath();
                    if (!item.IsValid && matches.Contains((source.Id, item.Id, item.Path)))
                    {
                        copy.Executables.Remove(item);
                        removed++;
                    }
                }
                next.Presets[i] = copy;
            }
            if (removed > 0)
                Commit(next);
            return removed;
        }
    }

    public void ExportData(string path)
    {
        lock (_sync)
        {
            EnsureWritable();
            path = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(path)!;
            var storageDirectory = Path.GetDirectoryName(StoragePath)!;
            if (ExportPath.IsProtected(directory, path, storageDirectory, StoragePath))
                throw new IOException("The destination is reserved for application storage.");
            AtomicJson.Write(path, new { DataBackup.Format, DataBackup.Version, Data }, maxBytes: DataBackup.MaxBytes);
        }
    }

    public bool ExportDataTo(string path)
    {
        try { ExportData(path); return true; }
        catch (Exception ex) when (IsFileError(ex)) { return false; }
    }

    public string? RestoreBackup(string source) => RestoreBackup(ReadBackup(source));

    internal string? RestoreBackup(AppData reviewed)
    {
        lock (_sync)
        {
            var previous = File.Exists(StoragePath) ? $"{StoragePath}.before-restore-{Guid.NewGuid():N}.json" : null;
            Commit(reviewed, previous);
            LoadFailed = false;
            return previous;
        }
    }

    public void ClearAllData()
    {
        lock (_sync)
        {
            EnsureWritable();
            Commit(new AppData());
        }
    }

    public void ResetApplication(SettingsService settings)
    {
        lock (_sync)
        {
            EnsureWritable();
            settings.ResetWith(() => Commit(new AppData()));
        }
    }

    private AppData Snapshot() => new()
    {
        Presets = new(Data.Presets),
        History = new(Data.History),
        UsageStats = new(Data.UsageStats, Data.UsageStats.Comparer)
    };

    private void Change(Action<AppData> change)
    {
        lock (_sync)
        {
            EnsureWritable();
            var next = Snapshot();
            change(next);
            Commit(next);
        }
    }

    private void EnsureWritable()
    {
        if (!_loaded && File.Exists(StoragePath))
            Load();
        if (LoadFailed)
            throw new InvalidDataException("Restore a backup before writing to an unreadable store.");
    }

    private void Commit(AppData next, string? previous = null)
    {
        AtomicJson.Write(StoragePath, next, previous, DataBackup.MaxBytes);
        Data = next;
        _loaded = true;
    }
}
