using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PresetLauncher.Models;
using PresetLauncher.Services;

namespace PresetLauncher.Tests;

[TestClass]
public sealed class DesktopInteractionTests
{
    [TestMethod]
    public void TranslationsAreNonemptyAndUseCompatibleFormatArguments()
    {
        var english = LocalizationService.GetTranslations("en");
        var french = LocalizationService.GetTranslations("fr");
        CollectionAssert.AreEquivalent(english.Keys.ToArray(), french.Keys.ToArray());
        foreach (var key in english.Keys)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(english[key]), key);
            Assert.IsFalse(string.IsNullOrWhiteSpace(french[key]), key);
            CollectionAssert.AreEquivalent(Arguments(english[key]), Arguments(french[key]), key);
            _ = string.Format(english[key], 1, 2, 3);
            _ = string.Format(french[key], 1, 2, 3);
        }

        string[] required = ["settings.behavior", "settings.export.error", "data.importSummary", "data.restoreFailed",
            "cleanup.confirm", "settings.reset.confirm", "editor.discardMessage", "editor.delayError", "process.error",
            "history.noMatches", "execution.resultsFailed", "preset.launchMissing", "nav.settings"];
        foreach (var key in required)
            Assert.IsTrue(english.ContainsKey(key), key);
    }

    [TestMethod]
    public void ComputedLabelsReadTheCurrentLanguageAndCulture()
    {
        var local = new LocalizationService();
        var preset = new Preset();
        preset.Executables.Add(new ExecutableItem("example.exe"));
        var run = new ExecutionHistory { SuccessCount = 1, ExecutablesCount = 2, FailedCount = 1 };
        local.SetLanguage("en");
        Assert.AreEqual("1 program", preset.ProgramsText);
        StringAssert.Contains(run.SuccessText, "Failed");
        local.SetLanguage("fr-CH");
        Assert.AreEqual("1 programme", preset.ProgramsText);
        StringAssert.Contains(run.SuccessText, "Échecs");
        StringAssert.Contains(local.Get("history.timing", "08:00", 2.5), "2,5");
        local.SetLanguage("---");
        Assert.AreEqual("Launch", local.Get("preset.launch"));
    }

    [TestMethod]
    public void FiltersRespectPartialFailuresAndLocalCalendarBoundaries()
    {
        var today = new DateTime(2025, 4, 5, 0, 0, 0, DateTimeKind.Local);
        var success = new ExecutionHistory { ExecutedAt = today.AddTicks(-1), ExecutablesCount = 1, SuccessCount = 1 };
        var partial = new ExecutionHistory { ExecutedAt = today.ToUniversalTime(), ExecutablesCount = 2, SuccessCount = 1, FailedCount = 1 };
        var failure = new ExecutionHistory { ExecutedAt = today.AddDays(1), ExecutablesCount = 1, FailedCount = 1 };
        ExecutionHistory[] history = [success, partial, failure];

        CollectionAssert.AreEqual(history, HistoryQuery.Apply(history, HistoryFilter.All, today).ToArray());
        CollectionAssert.AreEqual(new[] { success }, HistoryQuery.Apply(history, HistoryFilter.Successful, today).ToArray());
        CollectionAssert.AreEqual(new[] { failure }, HistoryQuery.Apply(history, HistoryFilter.Failed, today).ToArray());
        CollectionAssert.AreEqual(new[] { partial }, HistoryQuery.Apply(history, HistoryFilter.PartialFailure, today).ToArray());
        CollectionAssert.AreEqual(new[] { partial }, HistoryQuery.Apply(history, HistoryFilter.Today, today).ToArray());
        Assert.IsEmpty(HistoryQuery.Apply([success], HistoryFilter.Failed, today));
        Assert.IsEmpty(HistoryQuery.Apply([], HistoryFilter.All, today));
    }

    [TestMethod]
    [DataRow(LaunchMode.All, "preset.launchAll")]
    [DataRow(LaunchMode.MissingOnly, "preset.launchMissing")]
    public void LaunchConfirmationUsesTheSelectedModeOnlyWhenEnabled(LaunchMode mode, string action)
    {
        var preferences = new AppSettings { ConfirmBeforeLaunch = true };
        Assert.AreEqual(action, LaunchInteraction.ConfirmationAction(preferences, mode));
        preferences.ConfirmBeforeLaunch = false;
        Assert.IsNull(LaunchInteraction.ConfirmationAction(preferences, mode));
    }

    private static string[] Arguments(string template) => Regex.Matches(template, @"\{(\d+)[^}]*\}")
        .Select(match => match.Groups[1].Value).ToArray();
}
