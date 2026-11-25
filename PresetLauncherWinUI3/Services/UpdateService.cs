using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PresetLauncher.Services;

public enum UpdatePolicy { Optional, Recommended, Required }

public sealed record ReleaseUpdate(Version Version, Version MinimumSupportedVersion, Uri DownloadUri, long Size, string Sha256, UpdatePolicy Policy = UpdatePolicy.Optional);

public sealed class UpdateAssetException : IOException;

public sealed class UpdateService
{
    private const string Repository = "JimMarley420/Preset_launcher";
    private const string LatestUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";
    private const long MaxInstallerSize = 1024L * 1024 * 1024;
    private static readonly Regex StableVersion = new(@"^v?(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\z", RegexOptions.CultureInvariant);
    private static readonly Regex Policy = new(@"^<!-- presetlauncher:min-supported=([0-9.]+) -->\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex PolicyLevel = new(@"^<!-- presetlauncher:update-policy=(optional|recommended|required) -->\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly HttpClient SharedClient = CreateClient();
    private readonly HttpClient _client;

    public UpdateService() : this(SharedClient) { }

    public UpdateService(HttpClient client) => _client = client;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 2 * 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PresetLauncher/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    public string GetInstalledVersion() =>
        typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public static bool TryVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (value is null || !StableVersion.IsMatch(value) ||
            !Version.TryParse(value.TrimStart('v'), out var parsed) ||
            parsed.Major > 65534 || parsed.Minor > 65534 || parsed.Build > 65534)
            return false;
        version = parsed;
        return true;
    }

    public bool IsUpdateAvailable(string installed, string latest) =>
        TryVersion(installed, out var current) && TryVersion(latest, out var next) && next > current;

    public static bool IsRequired(string installed, ReleaseUpdate update) =>
        TryVersion(installed, out var current) && current < update.MinimumSupportedVersion;

    public static bool ShouldPrompt(string installed, ReleaseUpdate update, string? skippedVersion, bool manual,
        string? deferredVersion = null, DateTimeOffset? deferredUntil = null, DateTimeOffset? now = null)
    {
        if (!TryVersion(installed, out var current) || current >= update.Version) return false;
        if (manual || IsRequired(installed, update)) return true;
        var version = update.Version.ToString(3);
        if (update.Policy == UpdatePolicy.Optional) return skippedVersion != version;
        return deferredVersion != version || deferredUntil is null || deferredUntil <= (now ?? DateTimeOffset.UtcNow);
    }

    public async Task<ReleaseUpdate?> GetLatestUpdateAsync(string installed, Action<Version, Version> observePolicy, CancellationToken ct = default)
    {
        if (!TryVersion(installed, out var current) || current == new Version(0, 0, 0))
            return null; // Unversioned source build; never install a production release over it.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await _client.GetAsync(LatestUrl, timeout.Token);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(timeout.Token);
        return ParseArchitectureRelease(json, current, UpdatePolicySignature.PublicKey(),
            RuntimeInformation.ProcessArchitecture, observePolicy);
    }

    public static ReleaseUpdate? ParseArchitectureRelease(string json, Version installed, string publicKey,
        Architecture architecture, Action<Version, Version> observePolicy)
    {
        using var document = JsonDocument.Parse(json);
        var release = document.RootElement;
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean())
            throw new InvalidDataException("Expected a stable published release.");
        var tag = release.GetProperty("tag_name").GetString();
        if (!TryVersion(tag, out var latest))
            throw new InvalidDataException("Invalid release tag.");
        if (latest <= installed) return null;

        var body = release.GetProperty("body").GetString() ?? "";
        var markers = Regex.Matches(body, @"^<!-- presetlauncher:manifest-v2=([A-Za-z0-9+/=]+)\.([A-Za-z0-9+/=]+) -->\r?$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        if (markers.Count != 1)
            throw new InvalidDataException("Missing or ambiguous architecture manifest.");
        var message = Convert.FromBase64String(markers[0].Groups[1].Value);
        var domain = "presetlauncher-update-v2\n"u8;
        if (!message.AsSpan().StartsWith(domain))
            throw new InvalidDataException("Invalid architecture manifest protocol.");
        UpdatePolicySignature.VerifyMessage(message, markers[0].Groups[2].Value, publicKey);
        using var manifest = JsonDocument.Parse(message.AsMemory(domain.Length));
        var data = manifest.RootElement;
        if (data.GetProperty("version").GetString() != latest.ToString(3) ||
            !TryVersion(data.GetProperty("minimum_supported_version").GetString(), out var minimum) ||
            minimum > latest)
            throw new InvalidDataException("Invalid signed release versions.");
        var policy = data.GetProperty("policy").GetString() switch
        {
            "optional" => UpdatePolicy.Optional,
            "recommended" => UpdatePolicy.Recommended,
            "required" when minimum == latest => UpdatePolicy.Required,
            _ => throw new InvalidDataException("Invalid signed update policy.")
        };
        observePolicy(latest, minimum);

        using var mappingStream = typeof(UpdateService).Assembly.GetManifestResourceStream("PresetLauncher.Architectures")
            ?? throw new InvalidDataException("Missing architecture mapping.");
        using var mapping = JsonDocument.Parse(mappingStream);
        var target = architecture.ToString().ToLowerInvariant();
        if (!mapping.RootElement.TryGetProperty(target, out var entry))
            throw new UpdateAssetException();
        var name = $"PresetLauncher_Setup_{latest.ToString(3)}{entry.GetProperty("suffix").GetString()}.exe";
        if (!release.TryGetProperty("assets", out var releaseAssets) || releaseAssets.ValueKind != JsonValueKind.Array)
            throw new UpdateAssetException();
        var signedAssets = data.GetProperty("installers").EnumerateArray()
            .Where(asset => asset.GetProperty("architecture").GetString() == target).ToArray();
        var assets = releaseAssets.EnumerateArray()
            .Where(asset => asset.GetProperty("name").GetString() == name).ToArray();
        if (signedAssets.Length != 1 || assets.Length != 1)
            throw new UpdateAssetException();

        var metadata = signedAssets[0];
        var size = metadata.GetProperty("size").GetInt64();
        var digest = metadata.GetProperty("sha256").GetString();
        var item = assets[0];
        var expectedUrl = $"https://github.com/{Repository}/releases/download/{tag}/{name}";
        if (metadata.GetProperty("name").GetString() != name ||
            size <= 0 || size > MaxInstallerSize || digest is null || !Regex.IsMatch(digest, @"^[0-9a-f]{64}\z") ||
            item.GetProperty("state").GetString() != "uploaded" || item.GetProperty("size").GetInt64() != size ||
            item.GetProperty("browser_download_url").GetString() != expectedUrl ||
            !string.Equals(item.GetProperty("digest").GetString(), $"sha256:{digest}", StringComparison.OrdinalIgnoreCase))
            throw new UpdateAssetException();
        return new ReleaseUpdate(latest, minimum, new Uri(expectedUrl), size, digest, policy);
    }

    public static ReleaseUpdate? ParseSignedRelease(string json, Version installed, string publicKey)
    {
        using var document = JsonDocument.Parse(json);
        var release = document.RootElement;
        if (!TryVersion(release.GetProperty("tag_name").GetString(), out var latest))
            throw new InvalidDataException("Invalid release tag.");
        // Installed versions need not download or validate an older release.
        if (latest <= installed) return ParseRelease(json, installed);
        var update = ParseRelease(json, installed)
            ?? throw new InvalidDataException("Missing update.");
        UpdatePolicySignature.Verify(update, release.GetProperty("body").GetString() ?? "", publicKey);
        return update;
    }

    public static ReleaseUpdate? ParseRelease(string json, Version installed)
    {
        using var document = JsonDocument.Parse(json);
        var release = document.RootElement;
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean())
            throw new InvalidDataException("Expected a stable published release.");

        var tag = release.GetProperty("tag_name").GetString();
        if (!TryVersion(tag, out var latest))
            throw new InvalidDataException("Invalid release tag.");
        if (latest <= installed)
            return null;

        var matches = Policy.Matches(release.GetProperty("body").GetString() ?? "");
        Version minimum;
        if (matches.Count == 0 && tag == "1.2.6")
            minimum = new Version(0, 0, 0);
        else if (matches.Count != 1 || !TryVersion(matches[0].Groups[1].Value, out minimum) || minimum > latest)
            throw new InvalidDataException("Invalid update policy.");

        var levels = PolicyLevel.Matches(release.GetProperty("body").GetString() ?? "");
        var policy = UpdatePolicy.Optional;
        if (levels.Count == 0 && tag == "1.2.6") { }
        else if (levels.Count != 1)
            throw new InvalidDataException("Missing or ambiguous update policy level.");
        else
            policy = levels[0].Groups[1].Value switch
            {
                "optional" => UpdatePolicy.Optional,
                "recommended" => UpdatePolicy.Recommended,
                "required" => UpdatePolicy.Required,
                _ => throw new InvalidDataException("Invalid update policy level.")
            };
        if (policy == UpdatePolicy.Required && minimum != latest)
            throw new InvalidDataException("Required release must advance the minimum version.");

        var name = $"PresetLauncher_Setup_{latest.ToString(3)}.exe";
        var assets = release.GetProperty("assets").EnumerateArray()
            .Where(asset => asset.GetProperty("name").GetString() == name).ToArray();
        if (assets.Length != 1)
            throw new InvalidDataException("Expected exactly one x64 installer.");

        var item = assets[0];
        var expectedUrl = $"https://github.com/{Repository}/releases/download/{tag}/{name}";
        var size = item.GetProperty("size").GetInt64();
        var digest = item.GetProperty("digest").GetString();
        if (item.GetProperty("state").GetString() != "uploaded" ||
            item.GetProperty("browser_download_url").GetString() != expectedUrl ||
            size <= 0 || size > MaxInstallerSize ||
            digest is null || !Regex.IsMatch(digest, @"^sha256:[0-9a-fA-F]{64}\z"))
            throw new InvalidDataException("Invalid installer metadata.");

        return new ReleaseUpdate(latest, minimum, new Uri(expectedUrl), size, digest[7..], policy);
    }

    public async Task<string> DownloadVerifiedAsync(ReleaseUpdate update, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        var token = timeout.Token;
        var directory = Directory.CreateTempSubdirectory("PresetLauncher-update-");
        var path = Path.Combine(directory.FullName, $"PresetLauncher_Setup_{update.Version.ToString(3)}.exe");
        try
        {
            using var response = await _client.GetAsync(update.DownloadUri, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            var finalUri = response.RequestMessage?.RequestUri;
            if (finalUri is null || finalUri.Scheme != Uri.UriSchemeHttps ||
                (finalUri.Host != "github.com" && finalUri.Host != "release-assets.githubusercontent.com" &&
                 finalUri.Host != "objects.githubusercontent.com"))
                throw new InvalidDataException("Unexpected installer download host.");
            if (response.Content.Headers.ContentLength is long length && length != update.Size)
                throw new InvalidDataException("Installer size does not match GitHub metadata.");

            await using var input = await response.Content.ReadAsStreamAsync(token);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total = 0;
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true))
            {
                var buffer = new byte[65536];
                int read;
                while ((read = await input.ReadAsync(buffer, token)) > 0)
                {
                    total += read;
                    if (total > update.Size || total > MaxInstallerSize)
                        throw new InvalidDataException("Installer exceeds expected size.");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), token);
                    progress?.Report((int)(total * 100 / update.Size));
                }
            }

            if (total != update.Size ||
                !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(update.Sha256)))
                throw new InvalidDataException("Installer integrity verification failed.");
            token.ThrowIfCancellationRequested();
            return path;
        }
        catch
        {
            try { directory.Delete(recursive: true); }
            catch (IOException ex) { System.Diagnostics.Debug.WriteLine(ex); }
            catch (UnauthorizedAccessException ex) { System.Diagnostics.Debug.WriteLine(ex); }
            throw;
        }
    }
}
