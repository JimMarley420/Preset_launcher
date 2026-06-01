using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PresetLauncher.Models;

namespace PresetLauncher.Services;

public class DataService
{
    private readonly string _dataPath;
    public AppData Data { get; private set; } = new();

    public string StoragePath => _dataPath;

    public DataService(string? dataPath = null)
    {
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PresetLauncher"
            );
            Directory.CreateDirectory(appDataPath);
            _dataPath = Path.Combine(appDataPath, "data.json");
        }
        else
        {
            _dataPath = dataPath;
            var directory = Path.GetDirectoryName(_dataPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_dataPath))
            {
                var json = File.ReadAllText(_dataPath);
                Data = JsonConvert.DeserializeObject<AppData>(json) ?? new AppData();

                foreach (var preset in Data.Presets)
                {
                    preset.ValidateAllPaths();
                }
            }
            else
            {
                Data = new AppData();
                Save();
            }
        }
        catch
        {
            Data = new AppData();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(Data, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });

            // Write atomically to avoid data loss when user selects another file
            var temp = _dataPath + ".tmp";
            File.WriteAllText(temp, json);
            File.Copy(temp, _dataPath, true);
            File.Delete(temp);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving data: {ex.Message}");
        }
    }

    public void AddPreset(Preset preset)
    {
        Data.Presets.Add(preset);
        Save();
    }

    public void RemovePreset(Preset preset)
    {
        Data.Presets.Remove(preset);
        Save();
    }

    public void AddHistory(ExecutionHistory history)
    {
        Data.History.Insert(0, history);

        var maxEntries = App.Settings.CurrentSettings.MaxHistoryEntries;
        while (Data.History.Count > maxEntries)
        {
            Data.History.RemoveAt(Data.History.Count - 1);
        }
        Save();
    }

    public void ClearHistory()
    {
        Data.History.Clear();
        Save();
    }

    public void UpdateUsageStats(string exePath)
    {
        if (Data.UsageStats.ContainsKey(exePath))
        {
            Data.UsageStats[exePath]++;
        }
        else
        {
            Data.UsageStats[exePath] = 1;
        }
        Save();
    }

    public string ExportPreset(Preset preset)
    {
        return JsonConvert.SerializeObject(preset, Formatting.Indented);
    }

    public Preset? ImportPreset(string json)
    {
        try
        {
            var preset = JsonConvert.DeserializeObject<Preset>(json);
            if (preset != null)
            {
                preset.Id = Guid.NewGuid().ToString();
                preset.CreatedAt = DateTime.Now;
                preset.ValidateAllPaths();
            }
            return preset;
        }
        catch
        {
            return null;
        }
    }

    public void CleanupInvalidExecutables()
    {
        foreach (var preset in Data.Presets)
        {
            var invalidExes = preset.Executables.Where(e => !e.IsValid).ToList();
            foreach (var exe in invalidExes)
            {
                preset.Executables.Remove(exe);
            }
        }
        Save();
    }

    public bool ExportDataTo(string path)
    {
        try
        {
            var json = JsonConvert.SerializeObject(Data, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error exporting data: {ex.Message}");
            return false;
        }
    }

    public void ClearAllData()
    {
        Data = new AppData();
        Save();
    }
}
