using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PresetLauncher.Services;

namespace PresetLauncher.Tests;

[TestClass]
public sealed class UpdateExperienceTests
{
    private const string Begin = "<!-- presetlauncher-app-notes:v1:begin -->";
    private const string End = "<!-- presetlauncher-app-notes:v1:end -->";

    [TestMethod]
    [DataRow("")]
    [DataRow(Begin)]
    [DataRow(End + "\nNotes\n" + Begin)]
    [DataRow(Begin + "\n" + Begin + "\nNotes\n" + End + "\n" + End)]
    [DataRow(Begin + "\nNotes\n" + End + "\n" + End)]
    [DataRow(Begin + "\nNotes\n" + End + "\n<!-- presetlauncher-app-notes:v2:begin -->")]
    [DataRow(" " + Begin + "\nNotes\n" + End)]
    [DataRow(Begin + "\nNotes\n<!-- presetlauncher-app-notes:v1:end")]
    [DataRow("<!-- presetlauncher-app-notes:v2:begin -->\nNotes\n<!-- presetlauncher-app-notes:v2:end -->")]
    public void InvalidOrLegacyNotesNeverReturnTheReleaseBody(string body) =>
        Assert.IsNull(UpdateReleaseNotes.Extract("Full release notes\n" + body));

    [TestMethod]
    public void ExtractsOnlyBoundedVisiblePresentationAndFormatsNativeBlocks()
    {
        var notes = UpdateReleaseNotes.Extract("Full GitHub notes\r\n" + Begin + "\r\n" +
            "## Launcher\r\n\r\nFast **startup** uses `config.json`.\r\n\r\n" +
            "- [View changes](javascript:alert)\r\n1. Open Settings\r\n" +
            "![Preview](https://tracking.invalid/image)\r\n" +
            "<!-- presetlauncher:signature-v1=hidden -->\r\n" +
            "<details><summary>Debugging</summary>Internal transcript</details>\r\n" +
            "<script>unsafe script</script>\r\n" +
            "```text\r\npreset.json\r\n```\r\n" + End + "\r\nFooter");
        Assert.IsNotNull(notes);
        foreach (var hidden in new[] { "GitHub notes", "signature", "Internal transcript", "unsafe script", "Footer" })
            Assert.DoesNotContain(hidden, notes);
        var blocks = UpdateReleaseNotes.Format(notes);
        CollectionAssert.AreEqual(new[] { ReleaseNoteKind.Heading, ReleaseNoteKind.Paragraph, ReleaseNoteKind.ListItem,
            ReleaseNoteKind.ListItem, ReleaseNoteKind.Paragraph, ReleaseNoteKind.Code }, blocks.Select(block => block.Kind).ToArray());
        Assert.IsTrue(blocks[1].Spans.Contains(new("startup", ReleaseNoteStyle.Bold)));
        Assert.IsTrue(blocks[1].Spans.Contains(new("config.json", ReleaseNoteStyle.Code)));
        Assert.AreEqual("• View changes", string.Concat(blocks[2].Spans.Select(span => span.Text)));
        Assert.AreEqual("Preview", blocks[4].Spans.Single().Text);
        Assert.AreEqual(new ReleaseNoteSpan("preset.json", ReleaseNoteStyle.Code), blocks[5].Spans.Single());
        Assert.IsNull(UpdateReleaseNotes.Extract(Begin + "\n" + new string('x', 48_001) + "\n" + End));
        Assert.IsEmpty(UpdateReleaseNotes.Format(string.Concat(Enumerable.Repeat("**a** ", 801))));
    }

    [TestMethod]
    public void SignedArchitectureOfferTreatsNotesAsPresentationAndBuildsOfficialLinks()
    {
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "signed-update.json")))!;
        var release = fixture["release"]!;
        var body = release["body"]!.GetValue<string>();
        var installed = new Version(0, 0, 1);
        var key = fixture["public_key"]!.GetValue<string>();
        var legacy = UpdateService.ParseArchitectureRelease(release.ToJsonString(), installed, key, Architecture.X64, (_, _) => { })!;
        Assert.IsNull(legacy.Notes);
        release["body"] = body + "\n\n" + Begin + "\n- Visible summary\n" + End;
        release["html_url"] = "https://untrusted.invalid/release";
        var presented = UpdateService.ParseArchitectureRelease(release.ToJsonString(), installed, key, Architecture.X64, (_, _) => { })!;
        Assert.AreEqual("- Visible summary", presented.Notes);
        Assert.AreEqual(legacy.Sha256, presented.Sha256);
        Assert.AreEqual(legacy.MinimumSupportedVersion, presented.MinimumSupportedVersion);
        Assert.AreEqual("https://github.com/JimMarley420/Preset_launcher/releases/tag/" + release["tag_name"]!.GetValue<string>(),
            presented.ReleaseUri!.AbsoluteUri);
        Assert.AreEqual("https://jimmarley420.github.io/Preset_launcher/changelog.html", UpdateService.VersionHistoryUri("en").AbsoluteUri);
        Assert.AreEqual("https://jimmarley420.github.io/Preset_launcher/fr/changelog.html", UpdateService.VersionHistoryUri("fr").AbsoluteUri);
    }

    [TestMethod]
    [DataRow(0, null)]
    [DataRow(1, "update.setupStartFailed")]
    [DataRow(2, "update.setupCancelled")]
    [DataRow(4, "update.setupFailed")]
    [DataRow(5, "update.setupCancelled")]
    [DataRow(7, "update.setupFailed")]
    [DataRow(8, "update.rebootRequired")]
    [DataRow(3010, "update.rebootRequired")]
    [DataRow(-1, "update.setupUnexpected")]
    public void ClassifiesInstallerExitCodes(int code, string? expected) =>
        Assert.AreEqual(expected, UpdateInstallation.ResultKey(code));

    [TestMethod]
    [DataRow(true, "/ALLUSERS")]
    [DataRow(false, "/CURRENTUSER")]
    public void SilentArgumentsPreserveScopeWithoutGivingSetupAnAlternateDirectory(bool allUsers, string scope)
    {
        var installation = new UpdateInstallation(@"C:\Program Files\PresetLauncher", allUsers, new Version(1, 6, 0));
        ProcessStartInfo start = installation.InstallerStartInfo(@"C:\Temp\PresetLauncher Setup.exe");
        Assert.AreEqual(@"C:\Temp\PresetLauncher Setup.exe", start.FileName);
        Assert.AreEqual(allUsers ? "runas" : "", start.Verb);
        CollectionAssert.AreEqual(new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-",
            "/RESTARTEXITCODE=3010", "/NOCLOSEAPPLICATIONS", "/NORESTARTAPPLICATIONS", scope }, start.ArgumentList.ToArray());
    }

    [TestMethod]
    public void StagingPinsRejectWritesAndRenamesUntilReleased()
    {
        var directory = UpdateStaging.Create();
        var original = Path.Combine(directory.FullName, "PresetLauncher_Setup_1.7.0.exe");
        var renamed = original + ".renamed";
        try
        {
            using (UpdateStaging.PinDirectory(directory.FullName))
            {
                using var lease = UpdateStaging.Use(directory.FullName);
                File.WriteAllBytes(original, [1, 2, 3]);
                UpdateStaging.Clean(directory.FullName);
                Assert.IsTrue(File.Exists(original), "An active update workspace cannot be cleaned.");
                using var installer = UpdateStaging.PinFile(original);
                Assert.AreEqual(original, UpdateStaging.InstallerPath(installer, original), ignoreCase: true);
                Assert.ThrowsExactly<InvalidDataException>(() => UpdateStaging.InstallerPath(installer, renamed));
                Assert.ThrowsExactly<IOException>(() => File.WriteAllBytes(original, [4]));
                Assert.ThrowsExactly<IOException>(() => File.Move(original, renamed));
                Assert.ThrowsExactly<IOException>(() => Directory.Move(directory.FullName, directory.FullName + ".renamed"));
                Assert.AreEqual(1, installer.ReadByte());
            }
            File.Move(original, renamed);
            File.Delete(renamed);
        }
        finally { UpdateStaging.Clean(directory.FullName); }
        Assert.IsFalse(Directory.Exists(directory.FullName));
    }
}
