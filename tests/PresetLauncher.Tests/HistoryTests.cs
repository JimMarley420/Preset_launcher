using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PresetLauncher.Models;
using PresetLauncher.Services;

namespace PresetLauncher.Tests;

[TestClass]
public sealed class HistoryTests
{
    [TestMethod]
    [DataRow(3, 0, "Successful")]
    [DataRow(1, 2, "PartialFailure")]
    [DataRow(0, 3, "Failed")]
    [DataRow(0, 0, "Unavailable")]
    [DataRow(1, 0, "Successful")]
    public void ClassifiesRecordedRequests(int accepted, int failed, string status)
    {
        var entry = new ExecutionHistory { SuccessCount = accepted, FailedCount = failed, ExecutablesCount = 3 };
        var result = ExecutionResult.From(entry);

        Assert.AreEqual(status, result.Status.ToString());
        Assert.AreEqual(accepted, result.Accepted);
        Assert.AreEqual(failed, result.Failed);
        Assert.AreEqual(3 - accepted - failed, result.Unrecorded);
    }

    [TestMethod]
    [DataRow(2, "FileNotFound")]
    [DataRow(3, "PathNotFound")]
    [DataRow(5, "AccessDenied")]
    [DataRow(193, "InvalidFormat")]
    [DataRow(216, "Incompatible")]
    [DataRow(1155, "NoAssociation")]
    [DataRow(1223, "Cancelled")]
    [DataRow(99999, "Unknown")]
    public void UsesTheNativeCodeAndPreservesTheOriginalMessage(int code, string category)
    {
        var detail = new ExecutionDetail();
        detail.RecordFailure(new Win32Exception(code, "Original technical text"));

        Assert.AreEqual(code, detail.NativeErrorCode);
        Assert.AreEqual(category, detail.Failure.ToString());
        Assert.AreEqual("Original technical text", detail.ErrorMessage);
        Assert.AreEqual(false, detail.RecordedResult);
    }

    [TestMethod]
    public void NeverClassifiesAnExceptionFromItsMessage()
    {
        var detail = new ExecutionDetail();
        detail.RecordFailure(new InvalidOperationException("Access is denied."));
        Assert.AreEqual(LaunchFailure.Unknown, detail.Failure);
        Assert.IsNull(detail.NativeErrorCode);
        Assert.AreEqual(false, detail.RecordedResult);

        var legacy = new ExecutionDetail { ErrorMessage = "File not found." };
        Assert.AreEqual(LaunchFailure.Unknown, legacy.Failure);
        Assert.AreEqual("File not found.", legacy.ErrorMessage);
        Assert.AreEqual(false, legacy.RecordedResult);
        Assert.IsNull(new ExecutionDetail().RecordedResult);
    }

    [TestMethod]
    public void WindowsShellReportsMissingExecutableAsAWin32Failure()
    {
        var folder = Directory.CreateTempSubdirectory("presetlauncher-history-");
        try
        {
            var exception = Assert.ThrowsExactly<Win32Exception>(() =>
                Process.Start(new ProcessStartInfo(Path.Combine(folder.FullName, "missing.exe")) { UseShellExecute = true }));
            var detail = new ExecutionDetail();
            detail.RecordFailure(exception);
            Assert.AreEqual(2, detail.NativeErrorCode);
            Assert.AreEqual(LaunchFailure.FileNotFound, detail.Failure);
            Assert.AreEqual(exception.Message, detail.ErrorMessage);
        }
        finally { folder.Delete(recursive: true); }
    }

    [TestMethod]
    public void SearchMatchesHistoricNamesAndComposesWithEveryFilter()
    {
        var today = new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Local);
        var successful = new ExecutionHistory { PresetName = "Édition", ExecutedAt = today.AddDays(-1), SuccessCount = 1 };
        var partial = new ExecutionHistory
        {
            PresetName = "Workspace", ExecutedAt = today.ToUniversalTime(), SuccessCount = 1, FailedCount = 1,
            Details = [new() { ExecutableName = "Édition sonore" }]
        };
        var failed = new ExecutionHistory { PresetName = "Édition vidéo", ExecutedAt = today, FailedCount = 1 };
        var incomplete = new ExecutionHistory { PresetName = "Édition" };
        ExecutionHistory[] history = [successful, partial, failed, incomplete];
        ExecutionHistory[] Results(HistoryFilter filter) =>
            HistoryQuery.Apply(history, filter, today, "  EDITION  ", CultureInfo.GetCultureInfo("fr-FR")).ToArray();

        CollectionAssert.AreEqual(history, Results(HistoryFilter.All));
        CollectionAssert.AreEqual(new[] { successful }, Results(HistoryFilter.Successful));
        CollectionAssert.AreEqual(new[] { partial }, Results(HistoryFilter.PartialFailure));
        CollectionAssert.AreEqual(new[] { failed }, Results(HistoryFilter.Failed));
        CollectionAssert.AreEqual(new[] { partial, failed }, Results(HistoryFilter.Today));
        Assert.IsEmpty(HistoryQuery.Apply(history, HistoryFilter.All, today, "missing"));
    }

    [TestMethod]
    public void GroupsByLocalCalendarAndLocalizesHeadings()
    {
        var today = new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Local);
        var missing = new ExecutionHistory();
        var older = new ExecutionHistory { ExecutedAt = today.AddDays(-2) };
        var yesterday = new ExecutionHistory { ExecutedAt = today.AddTicks(-1).ToUniversalTime() };
        var earlierToday = new ExecutionHistory { ExecutedAt = today.ToUniversalTime() };
        var laterToday = new ExecutionHistory { ExecutedAt = today.AddHours(1) };
        var groups = HistoryQuery.GroupByLocalDate([missing, older, yesterday, earlierToday, laterToday]).ToArray();

        CollectionAssert.AreEqual(new[] { laterToday, earlierToday }, groups[0].ToArray());
        var localization = new LocalizationService();
        localization.SetLanguage("en");
        CollectionAssert.AreEqual(new[] { "Today", "Yesterday", "Thursday, April 9, 2026", "Date unavailable" },
            groups.Select(group => HistoryQuery.DateHeading(group.Key, today, localization)).ToArray());
        localization.SetLanguage("fr");
        CollectionAssert.AreEqual(new[] { "Aujourd’hui", "Hier", "jeudi 9 avril 2026", "Date indisponible" },
            groups.Select(group => HistoryQuery.DateHeading(group.Key, today, localization)).ToArray());
    }

    [TestMethod]
    public void NewAndLegacyDiagnosticsRoundTripThroughStorageAndBackup()
    {
        var folder = Directory.CreateTempSubdirectory("presetlauncher-history-");
        try
        {
            var activePath = Path.Combine(folder.FullName, "data.json");
            var backupPath = Path.Combine(folder.FullName, "backup.json");
            File.WriteAllText(activePath, """
                {
                  "Presets": [],
                  "History": [{
                    "Id": "b351bea1-4160-45fa-a708-cc53c923827f",
                    "Details": [{ "ExecutableName": "Legacy", "ErrorMessage": "Original message" }, {}]
                  }],
                  "UsageStats": {}
                }
                """);
            var data = new DataService(activePath);
            data.Load();
            Assert.IsFalse(data.LoadFailed);
            var legacy = data.Data.History.Single();
            Assert.AreEqual(ExecutionStatus.Failed, ExecutionResult.From(legacy).Status);
            Assert.IsNull(HistoryQuery.LocalDate(legacy.ExecutedAt));
            Assert.IsNull(legacy.Duration);
            Assert.IsNull(legacy.Details[1].RecordedResult);

            var preset = new Preset("Workspace");
            data.AddPreset(preset);
            var detail = new ExecutionDetail();
            detail.RecordFailure(new Win32Exception(1223, "Original cancellation"));
            var entry = new ExecutionHistory { PresetId = preset.Id, Details = [detail], FailedCount = 1 };
            data.RecordLaunch(preset, [], entry, 100);
            data.Load();
            Assert.IsFalse(data.LoadFailed);
            Assert.AreEqual(1223, data.Data.History[0].Details.Single().NativeErrorCode);

            data.ExportData(backupPath);
            data.ClearHistory();
            data.RestoreBackup(backupPath);
            var reloaded = new DataService(activePath);
            reloaded.Load();
            Assert.IsFalse(reloaded.LoadFailed);
            var restoredDetail = reloaded.Data.History[0].Details.Single();
            Assert.AreEqual(1223, restoredDetail.NativeErrorCode);
            Assert.AreEqual(LaunchFailure.Cancelled, restoredDetail.Failure);
            Assert.AreEqual("Original cancellation", restoredDetail.ErrorMessage);
            var legacyDetail = reloaded.Data.History[1].Details[0];
            Assert.IsNull(legacyDetail.NativeErrorCode);
            Assert.AreEqual("Original message", legacyDetail.ErrorMessage);
        }
        finally { folder.Delete(recursive: true); }
    }

    [TestMethod]
    public void ResolvesCurrentPresetIdentityAndDeletesOnlyTheConfirmedRecord()
    {
        var folder = Directory.CreateTempSubdirectory("presetlauncher-history-");
        try
        {
            var path = Path.Combine(folder.FullName, "data.json");
            var data = new DataService(path);
            var preset = new Preset("Original") { LaunchCount = 2 };
            var program = new ExecutableItem(@"C:\Examples\Fixture.exe") { LaunchCount = 1 };
            preset.Executables.Add(program);
            data.AddPreset(preset);
            var entry = new ExecutionHistory { PresetId = preset.Id.ToUpperInvariant(), PresetName = preset.Name };
            var otherEntry = new ExecutionHistory { PresetId = preset.Id, PresetName = preset.Name };
            data.RecordLaunch(preset, [program.Path], entry, 100);
            data.RecordLaunch(preset, [], otherEntry, 100);
            data.SaveDraft(new PresetDraft(preset) { Name = "Renamed" });

            Assert.AreEqual("Renamed", data.FindHistoryPreset(entry)?.Name);
            Assert.AreEqual("Original", entry.PresetName);
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.ThrowsExactly<IOException>(() => data.RemoveHistoryEntry(entry.Id));
            Assert.HasCount(2, data.Data.History);

            data.RemoveHistoryEntry(entry.Id);
            data.RemoveHistoryEntry(entry.Id);
            var saved = DataService.ReadBackup(path);
            Assert.AreEqual(otherEntry.Id, saved.History.Single().Id);
            Assert.AreEqual(2, saved.Presets.Single().LaunchCount);
            Assert.AreEqual(1, saved.Presets.Single().Executables.Single().LaunchCount);
            Assert.AreEqual(1, saved.UsageStats[program.Path]);

            data.RemovePreset(preset);
            data.AddPreset(new Preset(entry.PresetName));
            Assert.IsNull(data.FindHistoryPreset(otherEntry));
            Assert.AreEqual("Original", DataService.ReadBackup(path).History.Single().PresetName);
        }
        finally { folder.Delete(recursive: true); }
    }
}
