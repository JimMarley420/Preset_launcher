using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PresetLauncher.Models;
using PresetLauncher.Services;

namespace PresetLauncher.Tests;

[TestClass]
public sealed class DesktopWorkflowTests
{
    private string _folder = null!;
    private string _dataPath = null!;
    private string _backupPath = null!;
    private DataService _data = null!;

    [TestInitialize]
    public void Initialize()
    {
        _folder = Directory.CreateTempSubdirectory("presetlauncher-desktop-").FullName;
        _dataPath = Path.Combine(_folder, "data.json");
        _backupPath = Path.Combine(_folder, "transfer.json");
        _data = new DataService(_dataPath);
        _data.Load();
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_folder, recursive: true);

    [TestMethod]
    public void DraftIsIsolatedAndSavingUsesTheCurrentCountersAndIdentity()
    {
        var original = PresetWithPrograms();
        _data.AddPreset(original);
        var abandoned = new PresetDraft(original);
        Assert.IsFalse(abandoned.IsDirty);
        abandoned.Programs[0].RunAsAdmin = true;
        abandoned.Programs[0].DelaySeconds = 12;
        Assert.IsTrue(abandoned.IsDirty);
        Assert.IsFalse(original.Executables[0].RunAsAdmin);
        Assert.AreEqual(0, ReadActive().Data.Presets.Single().Executables[0].DelaySeconds);

        var draft = new PresetDraft(original) { Name = "Revised" };
        draft.Programs.Move(0, 1);
        draft.Programs[1].RunAsAdmin = true;
        draft.Programs[1].DelaySeconds = 12;
        original.Executables[0].LaunchCount = 7;
        original.LaunchCount = 3;
        _data.Save();
        _data.SaveDraft(draft);

        var saved = ReadActive().Data.Presets.Single();
        Assert.AreEqual("Revised", saved.Name);
        Assert.AreEqual(original.Id, saved.Id);
        Assert.AreEqual(original.CreatedAt, saved.CreatedAt);
        Assert.AreEqual(original.PasswordHash, saved.PasswordHash);
        Assert.IsTrue(saved.IsLocked);
        Assert.AreEqual(3, saved.LaunchCount);
        CollectionAssert.AreEqual(original.Executables.Reverse().Select(p => p.Id).ToArray(), saved.Executables.Select(p => p.Id).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, saved.Executables.Select(p => p.Order).ToArray());
        Assert.AreEqual("--example", saved.Executables[1].Arguments);
        Assert.AreEqual(7, saved.Executables[1].LaunchCount);
        Assert.AreEqual(12, saved.Executables[1].DelaySeconds);
        Assert.IsTrue(saved.Executables[1].RunAsAdmin);
    }

    [TestMethod]
    public void BlockedWritePreservesDiskAndLiveDraftTarget()
    {
        _data.AddPreset(PresetWithPrograms());
        var preset = _data.Data.Presets.Single();
        var draft = new PresetDraft(preset) { Name = "Unsaved" };
        var originalJson = File.ReadAllText(_dataPath);
        using (File.Open(_dataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsExactly<IOException>(() => _data.SaveDraft(draft));
        Assert.AreEqual(originalJson, File.ReadAllText(_dataPath));
        Assert.AreEqual("Workspace", _data.Data.Presets.Single().Name);
        Assert.IsTrue(draft.IsDirty);
    }

    [TestMethod]
    public void VersionedRoundTripRestoresDataAndKeepsRecoverablePreviousState()
    {
        var original = PresetWithPrograms();
        _data.AddPreset(original);
        _data.AddPreset(new Preset("Other"));
        var history = new ExecutionHistory
        {
            PresetId = Guid.NewGuid().ToString(), // A deleted preset.
            PresetName = "Deleted",
            ExecutablesCount = 1, FailedCount = 1, Duration = TimeSpan.FromSeconds(2)
        };
        history.Details.Add(new ExecutionDetail { ExecutableName = "Program", ExecutablePath = "example.exe", ErrorMessage = "Stopped" });
        _data.Data.History.Add(history);
        _data.UpdateUsageStats("example.exe");
        _data.ExportData(_backupPath);
        _data.ClearAllData();
        _data.AddPreset(new Preset("Current"));

        var recovery = _data.RestoreBackup(_backupPath);
        Assert.IsNotNull(recovery);
        Assert.AreEqual("Current", DataService.ReadBackup(recovery).Presets.Single().Name);
        var restored = ReadActive().Data;
        Assert.AreEqual(2, restored.Presets.Count);
        Assert.AreEqual(original.Id, restored.Presets[0].Id);
        Assert.AreEqual(original.Executables[0].Id, restored.Presets[0].Executables[0].Id);
        Assert.IsFalse(restored.Presets[0].Executables[0].IsValid);
        Assert.AreEqual(history.Id, restored.History.Single().Id);
        Assert.AreEqual(history.PresetId, restored.History.Single().PresetId);
        Assert.AreEqual(history.Duration, restored.History.Single().Duration);
        Assert.AreEqual("Stopped", restored.History.Single().Details.Single().ErrorMessage);
        Assert.AreEqual(1, restored.UsageStats["example.exe"]);
        Assert.AreEqual(_dataPath, _data.StoragePath);
    }

    [TestMethod]
    public void LegacyRawBackupRemainsReadable()
    {
        _data.AddPreset(PresetWithPrograms());
        File.Copy(_dataPath, _backupPath);
        _data.ClearAllData();
        _data.RestoreBackup(_backupPath);
        Assert.AreEqual("Workspace", ReadActive().Data.Presets.Single().Name);
    }

    [TestMethod]
    [DataRow("{malformed")]
    [DataRow("{'Presets':[], 'History':[], 'UsageStats':{}}")]
    [DataRow("{\"Presets\":[],\"Presets\":[],\"History\":[],\"UsageStats\":{}}")]
    [DataRow("{}")]
    [DataRow("{\"Name\":\"A preset\",\"Executables\":[]}")]
    [DataRow("{\"Format\":\"PresetLauncher.AppData\",\"Version\":2,\"Data\":{}}")]
    [DataRow("{\"Presets\":null,\"History\":[],\"UsageStats\":{}}")]
    [DataRow("{\"Presets\":[null],\"History\":[],\"UsageStats\":{}}")]
    [DataRow("{\"Presets\":[],\"History\":[],\"UsageStats\":{\"app.exe\":-1}}")]
    [DataRow("{\"Presets\":[],\"History\":[],\"UsageStats\":{\"app.exe\":\"1\"}}")]
    public void InvalidBackupLeavesLiveAndPersistedDataUntouched(string json)
    {
        _data.AddPreset(new Preset("Keep"));
        File.WriteAllText(_backupPath, json);
        var original = File.ReadAllText(_dataPath);
        Assert.IsTrue(DataService.IsFileError(Assert.Throws<Exception>(() => _data.RestoreBackup(_backupPath))));
        Assert.AreEqual(original, File.ReadAllText(_dataPath));
        Assert.AreEqual("Keep", _data.Data.Presets.Single().Name);
    }

    [TestMethod]
    public void DuplicateIdentitiesAndOutOfRangeDelaysAreRejected()
    {
        var preset = PresetWithPrograms();
        _data.AddPreset(preset);
        _data.AddPreset(preset);
        Assert.ThrowsExactly<InvalidDataException>(() => DataService.ReadBackup(_dataPath));
        _data.RemovePreset(preset);
        preset.Executables[0].DelaySeconds = 301;
        _data.Save();
        Assert.ThrowsExactly<InvalidDataException>(() => DataService.ReadBackup(_dataPath));
    }

    [TestMethod]
    public void TooLargeInputAndUnreadableActiveFileArePreserved()
    {
        using (var file = File.Create(_backupPath))
            file.SetLength(32 * 1024 * 1024 + 1);
        Assert.ThrowsExactly<InvalidDataException>(() => DataService.ReadBackup(_backupPath));

        File.WriteAllText(_dataPath, "{}");
        _data.Load();
        Assert.IsTrue(_data.LoadFailed);
        Assert.ThrowsExactly<InvalidDataException>(() => _data.AddPreset(new Preset("Do not write")));
        Assert.AreEqual("{}", File.ReadAllText(_dataPath));
    }

    [TestMethod]
    public void BlockedRestoreAndExportPreserveActiveData()
    {
        _data.ExportData(_backupPath);
        _data.AddPreset(new Preset("Keep"));
        var activeJson = File.ReadAllText(_dataPath);
        var backupJson = File.ReadAllText(_backupPath);
        using (File.Open(_dataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsExactly<IOException>(() => _data.RestoreBackup(_backupPath));
        using (File.Open(_backupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.IsFalse(_data.ExportDataTo(_backupPath));
        Assert.IsFalse(_data.ExportDataTo(_dataPath));
        var settings = Path.Combine(_folder, "settings.json");
        Assert.IsFalse(_data.ExportDataTo(settings));
        var protectedDirectory = Directory.CreateDirectory(Path.Combine(_folder, "Security"));
        Assert.IsFalse(_data.ExportDataTo(Path.Combine(protectedDirectory.FullName, "policy.json")));
        Assert.AreEqual(activeJson, File.ReadAllText(_dataPath));
        Assert.AreEqual(backupJson, File.ReadAllText(_backupPath));
        Assert.AreEqual("Keep", _data.Data.Presets.Single().Name);
    }

    [TestMethod]
    public void ExportRefusesAnAliasOfTheActiveStore()
    {
        var alias = Path.Combine(_folder, "alias.json");
        Assert.IsTrue(CreateHardLink(alias, _dataPath, IntPtr.Zero));
        Assert.IsFalse(_data.ExportDataTo(alias));
        Assert.IsEmpty(ReadActive().Data.Presets);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string alias, string existing, IntPtr reserved);

    [TestMethod]
    public void CleanupRechecksReviewedEntriesAndKeepsUnreviewedEntriesAndHistory()
    {
        var preset = PresetWithPrograms();
        _data.AddPreset(preset);
        _data.Data.History.Add(new ExecutionHistory { PresetId = preset.Id });
        _data.UpdateUsageStats("example.exe");
        var reviewed = _data.FindMissingExecutables();
        File.WriteAllText(preset.Executables[0].Path, "fixture");
        preset.Executables.Add(new ExecutableItem(Path.Combine(_folder, "unreviewed.exe")) { Order = 2 });
        _data.Save();

        using (File.Open(_dataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsExactly<IOException>(() => _data.CleanupInvalidExecutables(reviewed));
        Assert.HasCount(3, _data.Data.Presets.Single().Executables);
        Assert.AreEqual(1, _data.CleanupInvalidExecutables(reviewed));
        Assert.AreEqual(0, _data.CleanupInvalidExecutables(reviewed));
        var restored = ReadActive().Data;
        CollectionAssert.AreEqual(new[] { "first.exe", "unreviewed.exe" },
            restored.Presets.Single().Executables.Select(item => Path.GetFileName(item.Path)).ToArray());
        Assert.HasCount(1, restored.History);
        Assert.AreEqual(1, restored.UsageStats["example.exe"]);
    }

    [TestMethod]
    public void ResetRollsBackOnWriteFailureAndRestartsWithCanonicalDefaults()
    {
        var settingsPath = Path.Combine(_folder, "settings.json");
        var settings = new SettingsService(settingsPath);
        settings.Update(value =>
        {
            value.Language = "en";
            value.Theme = "dark";
            value.Backdrop = "none";
            value.ConfirmBeforeLaunch = false;
            value.MinimumSupportedVersion = "1.4.0";
        });
        _data.AddPreset(PresetWithPrograms());
        _data.Data.History.Add(new ExecutionHistory());
        _data.UpdateUsageStats("example.exe");
        var initial = File.ReadAllText(settingsPath);
        using (File.Open(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsExactly<IOException>(() => _data.ResetApplication(settings));
        using (File.Open(_dataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsExactly<IOException>(() => _data.ResetApplication(settings));
        Assert.AreEqual(initial, File.ReadAllText(settingsPath));
        Assert.AreEqual("en", settings.CurrentSettings.Language);
        Assert.HasCount(1, ReadActive().Data.Presets);

        var security = Directory.CreateDirectory(Path.Combine(_folder, "Security"));
        var policy = Path.Combine(security.FullName, "state");
        File.WriteAllText(policy, "protected policy fixture");
        _data.ResetApplication(settings);

        var reloaded = new SettingsService(settingsPath);
        reloaded.Load();
        var defaults = new AppSettings { MinimumSupportedVersion = "1.4.0" };
        Assert.AreEqual(JsonSerializer.Serialize(defaults), JsonSerializer.Serialize(reloaded.CurrentSettings));
        Assert.AreEqual("protected policy fixture", File.ReadAllText(policy));
        var data = ReadActive().Data;
        Assert.IsEmpty(data.Presets);
        Assert.IsEmpty(data.History);
        Assert.IsEmpty(data.UsageStats);
    }

    [TestMethod]
    public async Task SimultaneousUpdatesOnOneStoreAreSerialized()
    {
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => _data.UpdateUsageStats("example.exe"))));
        Assert.AreEqual(20, ReadActive().Data.UsageStats["example.exe"]);
    }

    private Preset PresetWithPrograms()
    {
        var preset = new Preset("Workspace") { IsLocked = true, PasswordHash = "hash", Description = "Example" };
        preset.Executables.Add(new ExecutableItem(Path.Combine(_folder, "first.exe")) { Arguments = "--example" });
        preset.Executables.Add(new ExecutableItem(Path.Combine(_folder, "second.exe")) { Order = 1 });
        return preset;
    }

    private DataService ReadActive()
    {
        var reloaded = new DataService(_dataPath);
        reloaded.Load();
        Assert.IsFalse(reloaded.LoadFailed);
        return reloaded;
    }
}
