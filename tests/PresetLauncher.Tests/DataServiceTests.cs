using Microsoft.VisualStudio.TestTools.UnitTesting;
using PresetLauncher.Models;
using PresetLauncher.Services;

namespace PresetLauncher.Tests;

[TestClass]
public sealed class DataServiceTests
{
    private string directory = null!;
    private string dataPath = null!;

    [TestInitialize]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"PresetLauncher.Tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(directory);
        dataPath = Path.Combine(directory, "data.json");
    }

    [TestCleanup]
    public void CleanUp() => Directory.Delete(directory, recursive: true);

    [TestMethod]
    public void NewStoreCreatesEmptyDataFile()
    {
        var service = new DataService(dataPath);
        service.Load();
        Assert.IsTrue(File.Exists(dataPath));
        Assert.AreEqual(0, service.Data.Presets.Count);
        Assert.AreEqual(0, service.Data.History.Count);
        Assert.AreEqual(0, service.Data.UsageStats.Count);
    }

    [TestMethod]
    public void PresetAndUsageSurviveReload()
    {
        var file = Path.Combine(directory, "example.exe");
        File.WriteAllText(file, "fixture; never executed");
        var first = new DataService(dataPath);
        first.Load();
        var preset = new Preset("Work") { Description = "Daily tools" };
        preset.Executables.Add(new ExecutableItem(file) { DelaySeconds = 3, Arguments = "--example", RunAsAdmin = true });
        first.AddPreset(preset);
        first.UpdateUsageStats(file);
        first.UpdateUsageStats(file);

        var second = new DataService(dataPath);
        second.Load();
        var restored = second.Data.Presets.Single();
        Assert.AreEqual(preset.Id, restored.Id);
        Assert.AreEqual("Work", restored.Name);
        Assert.AreEqual("Daily tools", restored.Description);
        var item = restored.Executables.Single();
        Assert.AreEqual(file, item.Path);
        Assert.AreEqual(3, item.DelaySeconds);
        Assert.AreEqual("--example", item.Arguments);
        Assert.IsTrue(item.RunAsAdmin);
        Assert.IsTrue(item.IsValid);
        Assert.AreEqual(2, second.Data.UsageStats[file]);
    }

    [TestMethod]
    public void ImportAssignsNewIdentityAndValidatesPaths()
    {
        var service = new DataService(dataPath);
        var source = new Preset("Imported");
        source.Executables.Add(new ExecutableItem(Path.Combine(directory, "missing.exe")));
        var imported = service.ImportPreset(service.ExportPreset(source));
        Assert.IsNotNull(imported);
        Assert.AreNotEqual(source.Id, imported.Id);
        Assert.AreEqual("Imported", imported.Name);
        Assert.IsFalse(imported.Executables.Single().IsValid);
        Assert.AreEqual(0, service.Data.Presets.Count);
    }

    [TestMethod]
    public void MalformedImportIsRejected()
    {
        var service = new DataService(dataPath);
        Assert.IsNull(service.ImportPreset("{not valid json"));
        Assert.AreEqual(0, service.Data.Presets.Count);
    }

    [TestMethod]
    public void CleanupRemovesOnlyInvalidEntriesAndPersists()
    {
        var validPath = Path.Combine(directory, "existing.exe");
        File.WriteAllText(validPath, "fixture");
        var valid = new ExecutableItem(validPath);
        var invalid = new ExecutableItem(Path.Combine(directory, "missing.exe"));
        valid.ValidatePath();
        invalid.ValidatePath();
        var preset = new Preset("Mixed");
        preset.Executables.Add(valid);
        preset.Executables.Add(invalid);
        var service = new DataService(dataPath);
        service.AddPreset(preset);
        service.CleanupInvalidExecutables();

        var reloaded = new DataService(dataPath);
        reloaded.Load();
        Assert.AreEqual(validPath, reloaded.Data.Presets.Single().Executables.Single().Path);
    }

    [TestMethod]
    public void ClearHistoryPreservesPresetsAndUsage()
    {
        var service = new DataService(dataPath);
        service.AddPreset(new Preset("Keep"));
        service.UpdateUsageStats("example.exe");
        service.Data.History.Add(new ExecutionHistory { PresetName = "Keep", SuccessCount = 1 });
        service.Save();

        var reloaded = new DataService(dataPath);
        reloaded.Load();
        Assert.AreEqual(1, reloaded.Data.History.Count);
        reloaded.ClearHistory();
        service.Load();
        Assert.AreEqual(0, service.Data.History.Count);
        Assert.AreEqual("Keep", service.Data.Presets.Single().Name);
        Assert.AreEqual(1, service.Data.UsageStats["example.exe"]);
    }

    [TestMethod]
    public void RemoveAndResetPersist()
    {
        var service = new DataService(dataPath);
        var preset = new Preset("Temporary");
        service.AddPreset(preset);
        service.RemovePreset(preset);
        var reloaded = new DataService(dataPath);
        reloaded.Load();
        Assert.AreEqual(0, reloaded.Data.Presets.Count);

        reloaded.AddPreset(new Preset("Reset me"));
        reloaded.UpdateUsageStats("example.exe");
        reloaded.ClearAllData();
        service.Load();
        Assert.AreEqual(0, service.Data.Presets.Count);
        Assert.AreEqual(0, service.Data.UsageStats.Count);
        Assert.AreEqual(0, service.Data.History.Count);
    }
}
