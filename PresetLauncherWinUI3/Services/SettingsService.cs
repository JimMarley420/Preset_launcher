using Newtonsoft.Json;
using PresetLauncher.Models;

namespace PresetLauncher.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    public AppSettings CurrentSettings { get; private set; } = new();

    public SettingsService(string? settingsPath = null)
    {
        _settingsPath = Path.GetFullPath(settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PresetLauncher", "settings.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
                CurrentSettings = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(_settingsPath)) ?? new();
        }
        catch (Exception ex) when (DataService.IsFileError(ex))
        {
            System.Diagnostics.Debug.WriteLine(ex.GetType().Name);
            CurrentSettings = new();
        }
    }

    public void Save() => AtomicJson.Write(_settingsPath, CurrentSettings);

    public void Update(Action<AppSettings> change)
    {
        var next = CurrentSettings.Copy();
        change(next);
        AtomicJson.Write(_settingsPath, next);
        CurrentSettings = next;
    }

    internal void ResetWith(Action? commitData = null)
    {
        var defaults = new AppSettings { MinimumSupportedVersion = CurrentSettings.MinimumSupportedVersion };
        if (commitData is null)
        {
            AtomicJson.Write(_settingsPath, defaults);
        }
        else
        {
            var previous = File.Exists(_settingsPath) ? $"{_settingsPath}.before-reset-{Guid.NewGuid():N}" : null;
            AtomicJson.Write(_settingsPath, defaults, previous);
            try { commitData(); }
            catch (Exception original)
            {
                try
                {
                    if (previous is null)
                        File.Delete(_settingsPath);
                    else
                        File.Replace(previous, _settingsPath, null);
                }
                catch (Exception rollback) when (DataService.IsFileError(rollback))
                {
                    CurrentSettings = defaults;
                    throw new IOException("Reset failed and preferences could not be rolled back.", new AggregateException(original, rollback));
                }
                throw;
            }
            if (previous is not null)
            {
                try { File.Delete(previous); }
                catch (Exception ex) when (DataService.IsFileError(ex)) { System.Diagnostics.Debug.WriteLine(ex.GetType().Name); }
            }
        }
        CurrentSettings = defaults;
    }

    public void Reset() => ResetWith();
}
