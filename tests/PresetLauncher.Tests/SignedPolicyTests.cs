using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PresetLauncher.Services;

namespace PresetLauncher.Tests;

[TestClass]
public sealed class SignedPolicyTests
{
    private static ReleaseUpdate Update(string version = "1.4.0", string minimum = "1.4.0",
        UpdatePolicy policy = UpdatePolicy.Required) => new(Version.Parse(version), Version.Parse(minimum),
        new Uri($"https://github.com/JimMarley420/Preset_launcher/releases/download/v{version}/PresetLauncher_Setup_{version}.exe"),
        4, new string('a', 64), policy);

    [TestMethod]
    public void SignatureBindsPolicyVersionAndInstaller()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var update = Update();
        var signature = Convert.ToBase64String(key.SignData(UpdatePolicySignature.Payload(update),
            HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
        var marker = $"<!-- presetlauncher:signature-v1={signature} -->";
        var publicKey = key.ExportSubjectPublicKeyInfoPem();
        UpdatePolicySignature.Verify(update, marker, publicKey);
        foreach (var changed in new[] {
            update with { MinimumSupportedVersion = new Version(1, 3, 0) },
            update with { Policy = UpdatePolicy.Optional },
            update with { Version = new Version(1, 4, 1) },
            update with { Size = 5 },
            update with { Sha256 = new string('b', 64) }
        })
            Assert.ThrowsExactly<InvalidDataException>(() => UpdatePolicySignature.Verify(changed, marker, publicKey));
        Assert.ThrowsExactly<InvalidDataException>(() => UpdatePolicySignature.Verify(update, "", publicKey));
        Assert.ThrowsExactly<InvalidDataException>(() => UpdatePolicySignature.Verify(update, marker + "\n" + marker, publicKey));
        using var wrong = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.ThrowsExactly<InvalidDataException>(() => UpdatePolicySignature.Verify(update, marker, wrong.ExportSubjectPublicKeyInfoPem()));
    }

    [TestMethod]
    public void SignedReleaseParserRejectsUnsignedAndTamperedRelease()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var update = Update();
        var signature = Convert.ToBase64String(key.SignData(UpdatePolicySignature.Payload(update),
            HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
        string Release(string policy, string marker) => JsonSerializer.Serialize(new {
            tag_name = "v1.4.0", draft = false, prerelease = false,
            body = $"<!-- presetlauncher:min-supported=1.4.0 -->\n<!-- presetlauncher:update-policy={policy} -->\n{marker}",
            assets = new[] { new { name = "PresetLauncher_Setup_1.4.0.exe", state = "uploaded",
                browser_download_url = update.DownloadUri.ToString(), size = 4, digest = "sha256:" + update.Sha256 } }
        });
        var marker = $"<!-- presetlauncher:signature-v1={signature} -->";
        var publicKey = key.ExportSubjectPublicKeyInfoPem();
        Assert.AreEqual(update, UpdateService.ParseSignedRelease(Release("required", marker), new Version(1, 3, 0), publicKey));
        Assert.ThrowsExactly<InvalidDataException>(() => UpdateService.ParseSignedRelease(Release("required", ""), new Version(1, 3, 0), publicKey));
        Assert.ThrowsExactly<InvalidDataException>(() => UpdateService.ParseSignedRelease(Release("optional", marker), new Version(1, 3, 0), publicKey));
    }

    [TestMethod]
    public void ProtectedStoreSurvivesSettingsResetAndRejectsRollback()
    {
        using var directory = new TemporaryDirectory();
        var store = new UpdatePolicyStore(directory.Path);
        Assert.AreEqual(new Version(1, 3, 0), store.Load("1.3.0").Minimum);
        store.Observe(Update());
        var reopened = new UpdatePolicyStore(directory.Path);
        Assert.AreEqual(new Version(1, 4, 0), reopened.Load("0.0.0").Minimum);
        Assert.ThrowsExactly<InvalidDataException>(() => reopened.Observe(Update("1.3.0", "1.3.0")));
        Assert.ThrowsExactly<InvalidDataException>(() => reopened.Observe(Update("1.5.0", "1.3.0", UpdatePolicy.Optional)));
        reopened.Observe(Update("1.5.0", "1.4.0", UpdatePolicy.Optional));
        Assert.AreEqual(new Version(1, 5, 0), reopened.Load("0.0.0").Latest);
    }

    [TestMethod]
    public void OpenSslSignatureInteroperatesWithDotNet()
    {
        // Test-only public key and signature generated with openssl dgst -sha256.
        const string key = """
            -----BEGIN PUBLIC KEY-----
            MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE2Xb12sIuiRlWa05xgwm/jzoD4Xtk
            towmNTY/WSvikJ97SbnJo6UgeL/ZqrbTG3OeitIBEKEGSTJkDZ2lBQegCg==
            -----END PUBLIC KEY-----
            """;
        const string marker = "<!-- presetlauncher:signature-v1=MEUCIHnVMdH/hTmCKLTGAfhh2BQ3MTB6K1eDL6fyhvcb8XDHAiEAtwHFNqVcVkfCyHH/J/yO9pNvRA7iiUGz+HVcbO3X8xg= -->";
        UpdatePolicySignature.Verify(Update(), marker, key);
    }

    [TestMethod]
    public void DeletingPolicyAloneDoesNotReinitializeFromSettings()
    {
        using var directory = new TemporaryDirectory();
        var store = new UpdatePolicyStore(directory.Path);
        store.Load("0.0.0");
        store.Observe(Update());
        File.Delete(System.IO.Path.Combine(directory.Path, "update-policy.dat"));
        Assert.ThrowsExactly<InvalidDataException>(() => new UpdatePolicyStore(directory.Path).Load("0.0.0"));
    }

    [TestMethod]
    public void CorruptedProtectedStateIsNotReplacedWithDefaults()
    {
        using var directory = new TemporaryDirectory();
        var store = new UpdatePolicyStore(directory.Path);
        store.Load("0.0.0");
        var file = System.IO.Path.Combine(directory.Path, "update-policy.dat");
        File.WriteAllBytes(file, [1, 2, 3]);
        Assert.ThrowsExactly<CryptographicException>(() => store.Load("0.0.0"));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(file));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("presetlauncher-policy-").FullName;
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
