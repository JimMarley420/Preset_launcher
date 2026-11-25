using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PresetLauncher.Services;

public static class UpdatePolicySignature
{
    private static readonly Regex Marker = new(
        @"^<!-- presetlauncher:signature-v1=([A-Za-z0-9+/=]+) -->\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public static string PublicKey()
    {
        using var stream = typeof(UpdatePolicySignature).Assembly.GetManifestResourceStream("PresetLauncher.UpdatePolicyPublicKey")
            ?? throw new InvalidDataException("Missing update public key.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static byte[] Payload(ReleaseUpdate update) => Encoding.ASCII.GetBytes(
        $"presetlauncher-update-v1\n{update.Version.ToString(3)}\n{update.MinimumSupportedVersion.ToString(3)}\n" +
        $"{update.Policy.ToString().ToLowerInvariant()}\n{update.Size.ToString(CultureInfo.InvariantCulture)}\n{update.Sha256.ToLowerInvariant()}\n");

    public static void Verify(ReleaseUpdate update, string body, string publicKey)
    {
        var matches = Marker.Matches(body);
        if (matches.Count != 1)
            throw new InvalidDataException("Missing or ambiguous update signature.");
        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(publicKey);
            if (key.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7" ||
                !key.VerifyData(Payload(update), Convert.FromBase64String(matches[0].Groups[1].Value),
                    HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
                throw new InvalidDataException("Invalid update signature.");
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException or FormatException)
        {
            throw new InvalidDataException("Invalid update signature or public key.", ex);
        }
    }
}
