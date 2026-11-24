using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PresetLauncher.Services;

namespace PresetLauncher.Tests;

[TestClass]
public sealed class ReleaseUpdateTests
{
    private const string Url = "https://github.com/JimMarley420/Preset_launcher/releases/download/v1.3.0/PresetLauncher_Setup_1.3.0.exe";
    private static readonly byte[] Payload = [1, 2, 3, 4];
    private static readonly string Digest = Convert.ToHexString(SHA256.HashData(Payload)).ToLowerInvariant();

    private static string Release(string floor = "1.2.6", string? digest = null, string url = Url,
        string tag = "v1.3.0", bool draft = false, bool prerelease = false, int size = 4, bool includeAsset = true)
    {
        var asset = new { name = "PresetLauncher_Setup_1.3.0.exe", state = "uploaded",
            browser_download_url = url, size, digest = digest ?? $"sha256:{Digest}" };
        return JsonSerializer.Serialize(new { tag_name = tag, body = $"<!-- presetlauncher:min-supported={floor} -->",
            draft, prerelease, assets = new[] { asset }.Where(_ => includeAsset).ToArray() });
    }

    [TestMethod]
    public void ValidReleaseRespectsMinimumVersionAndSkipPolicy()
    {
        var update = UpdateService.ParseRelease(Release(), new Version(1, 2, 5))!;
        Assert.AreEqual(new Version(1, 3, 0), update.Version);
        Assert.AreEqual(new Version(1, 2, 6), update.MinimumSupportedVersion);
        Assert.AreEqual(new Uri(Url), update.DownloadUri);
        Assert.AreEqual(Digest, update.Sha256);
        Assert.IsTrue(UpdateService.ShouldPrompt("1.2.5", update, "1.3.0", false), "Required release cannot be skipped");
        Assert.IsFalse(UpdateService.ShouldPrompt("1.2.6", update, "1.3.0", false), "Optional skipped release is suppressed");
        Assert.IsTrue(UpdateService.ShouldPrompt("1.2.6", update, "1.3.0", true), "Manual check ignores skip");
        Assert.IsTrue(UpdateService.ShouldPrompt("1.2.6", update, "1.2.7", false), "A later version is not skipped");
    }

    [TestMethod]
    public void CurrentAndNewerVersionsNeverDowngrade()
    {
        Assert.IsNull(UpdateService.ParseRelease(Release(), new Version(1, 3, 0)));
        Assert.IsNull(UpdateService.ParseRelease(Release(), new Version(2, 0, 0)));
    }

    [TestMethod]
    public void RejectsInvalidMetadata()
    {
        foreach (var json in new[]
        {
            Release(floor: "2.0.0"),
            Release(floor: "not-a-version"),
            Release(digest: "sha256:bad"),
            Release(url: "https://example.org/setup.exe"),
            Release(tag: "v1.3.0-beta"),
            Release(draft: true),
            Release(prerelease: true),
            Release(size: 0),
            Release(includeAsset: false)
        })
        {
            Assert.ThrowsExactly<InvalidDataException>(() => UpdateService.ParseRelease(json, new Version(1, 2, 6)));
        }
    }

    [TestMethod]
    public async Task DownloadReturnsVerifiedBytes()
    {
        using var client = new HttpClient(new PayloadHandler(Payload));
        var update = UpdateService.ParseRelease(Release(), new Version(1, 2, 6))!;
        string? path = null;
        try
        {
            path = await new UpdateService(client).DownloadVerifiedAsync(update);
            CollectionAssert.AreEqual(Payload, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (path is not null) Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [TestMethod]
    public async Task DownloadRejectsWrongDigestAndLength()
    {
        var update = UpdateService.ParseRelease(Release(), new Version(1, 2, 6))!;
        foreach (var bytes in new[] { new byte[] { 0, 2, 3, 4 }, new byte[] { 1, 2, 3 } })
        {
            using var client = new HttpClient(new PayloadHandler(bytes));
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new UpdateService(client).DownloadVerifiedAsync(update));
        }
    }

    [TestMethod]
    public async Task DownloadRejectsUnexpectedRedirectHost()
    {
        using var client = new HttpClient(new PayloadHandler(Payload, new Uri("https://example.org/setup.exe")));
        var update = UpdateService.ParseRelease(Release(), new Version(1, 2, 6))!;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new UpdateService(client).DownloadVerifiedAsync(update));
    }

    private sealed class PayloadHandler(byte[] bytes, Uri? finalUri = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = finalUri is null ? request : new HttpRequestMessage(HttpMethod.Get, finalUri),
                Content = new ByteArrayContent(bytes)
            });
        }
    }
}
