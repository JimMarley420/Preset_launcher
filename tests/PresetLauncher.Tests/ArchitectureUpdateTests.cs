using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PresetLauncher.Services;

namespace PresetLauncher.Tests;

[TestClass]
public sealed class ArchitectureUpdateTests
{
    private const string VersionText = "2.0.0";
    private const string BaseUrl = "https://github.com/JimMarley420/Preset_launcher/releases/download/v2.0.0/";
    private static readonly Version Installed = new(1, 4, 0);
    private static readonly string Digest = new('a', 64);

    private static (string Name, string Architecture)[] Installers =>
        [("PresetLauncher_Setup_2.0.0.exe", "x64"), ("PresetLauncher_Setup_2.0.0_x86.exe", "x86"),
            ("PresetLauncher_Setup_2.0.0_arm64.exe", "arm64")];

    private static string Release(ECDsa key, Func<string, string>? changeMessage = null,
        Func<string[], IEnumerable<string>>? selectAssets = null, bool legacyOnly = false)
    {
        var installers = Installers.Select(item => new
        {
            architecture = item.Architecture, name = item.Name, size = 42, sha256 = Digest
        }).ToArray();
        var message = "presetlauncher-update-v2\n" + JsonSerializer.Serialize(new
        {
            version = VersionText, minimum_supported_version = VersionText, policy = "required", installers
        });
        var bytes = Encoding.ASCII.GetBytes(message);
        var v2 = key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        // This fixture spells out the deployed v1 bytes independently of the new serializer.
        var v1Message = Encoding.ASCII.GetBytes($"presetlauncher-update-v1\n2.0.0\n2.0.0\nrequired\n42\n{Digest}\n");
        var v1 = key.SignData(v1Message, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        var body = $"<!-- presetlauncher:min-supported=2.0.0 -->\n<!-- presetlauncher:update-policy=required -->\n" +
            $"<!-- presetlauncher:signature-v1={Convert.ToBase64String(v1)} -->\n";
        if (!legacyOnly)
        {
            var encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes(changeMessage?.Invoke(message) ?? message));
            body += $"<!-- presetlauncher:manifest-v2={encoded}.{Convert.ToBase64String(v2)} -->";
        }

        var names = installers.Select(item => item.name).ToArray();
        return JsonSerializer.Serialize(new
        {
            tag_name = "v2.0.0", draft = false, prerelease = false, body,
            assets = (selectAssets?.Invoke(names) ?? names).Select(name => new
            {
                name, size = 42, state = "uploaded", digest = $"sha256:{Digest}", browser_download_url = BaseUrl + name
            })
        });
    }

    [TestMethod]
    [DataRow(Architecture.X64, 0)]
    [DataRow(Architecture.X86, 1)]
    [DataRow(Architecture.Arm64, 2)]
    public void SelectsProcessArchitectureAndPreservesV1(Architecture architecture, int index)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var json = Release(key);
        var publicKey = key.ExportSubjectPublicKeyInfoPem();
        var version = new Version(2, 0, 0);
        var legacy = UpdateService.ParseSignedRelease(json, Installed, publicKey);
        Assert.AreEqual(BaseUrl + Installers[0].Name, legacy!.DownloadUri.AbsoluteUri);
        var update = UpdateService.ParseArchitectureRelease(json, Installed, publicKey, architecture,
            (latest, minimum) => { Assert.AreEqual(version, latest); Assert.AreEqual(version, minimum); });
        Assert.AreEqual(BaseUrl + Installers[index].Name, update!.DownloadUri.AbsoluteUri);
    }

    [TestMethod]
    [DataRow(Architecture.Arm64, false)]
    [DataRow(Architecture.X64, true)]
    [DataRow(Architecture.Wasm, false)]
    public void UnavailableAssetStillObservesMandatoryPolicy(Architecture architecture, bool duplicate)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var json = Release(key, selectAssets: names => duplicate ? [names[0], names[0]] : [names[0]]);
        Version? floor = null;
        Assert.ThrowsExactly<UpdateAssetException>(() => UpdateService.ParseArchitectureRelease(
            json, Installed, key.ExportSubjectPublicKeyInfoPem(), architecture, (_, minimum) => floor = minimum));
        Assert.AreEqual(new Version(2, 0, 0), floor);
    }

    [TestMethod]
    [DataRow("arm64", "x64")]
    [DataRow("_x86.exe", "_x64.exe")]
    [DataRow("2.0.0", "1.5.0")]
    [DataRow("required", "optional")]
    [DataRow("42", "43")]
    [DataRow("aaaa", "bbbb")]
    public void RejectsModifiedSignedFields(string original, string replacement)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var json = Release(key, message => message.Replace(original, replacement, StringComparison.Ordinal));
        Assert.ThrowsExactly<InvalidDataException>(() => UpdateService.ParseArchitectureRelease(
            json, Installed, key.ExportSubjectPublicKeyInfoPem(), Architecture.X64,
            (_, _) => Assert.Fail("Unverified policy reached the store")));
    }

    [TestMethod]
    [DataRow("\"size\":42", "\"size\":43")]
    [DataRow("sha256:aaaa", "sha256:bbbb")]
    [DataRow("/releases/download/", "/invalid/download/")]
    public void RejectsGitHubAssetDisagreement(string original, string replacement)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var json = Release(key).Replace(original, replacement, StringComparison.Ordinal);
        Assert.ThrowsExactly<UpdateAssetException>(() => UpdateService.ParseArchitectureRelease(
            json, Installed, key.ExportSubjectPublicKeyInfoPem(), Architecture.X64, (_, _) => { }));
    }

    [TestMethod]
    public void RejectsDowngradeToValidV1()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var json = Release(key, legacyOnly: true);
        Assert.ThrowsExactly<InvalidDataException>(() => UpdateService.ParseArchitectureRelease(
            json, Installed, key.ExportSubjectPublicKeyInfoPem(), Architecture.X64,
            (_, _) => Assert.Fail("Unverified policy reached the store")));
    }
}
