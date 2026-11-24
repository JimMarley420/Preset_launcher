using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PresetLauncher.Services;

public sealed record ReleaseUpdate(Version Version, Version MinimumSupportedVersion, Uri DownloadUri, long Size, string Sha256);

public sealed class UpdateService
{
    private const string Repository = "JimMarley420/Preset_launcher";
    private const string LatestUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";
    private const long MaxInstallerSize = 1024L * 1024 * 1024;
    private static readonly Regex StableVersion = new(@"^v?(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})$", RegexOptions.CultureInvariant);
    private static readonly Regex Policy = new(@"^<!-- presetlauncher:min-supported=([0-9.]+) -->\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
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

    public static bool ShouldPrompt(string installed, ReleaseUpdate update, string? skippedVersion, bool manual) =>
        TryVersion(installed, out var current) && current < update.Version &&
        (manual || IsRequired(installed, update) || skippedVersion != update.Version.ToString(3));

    public async Task<ReleaseUpdate?> GetLatestUpdateAsync(string installed, CancellationToken ct = default)
    {
        if (!TryVersion(installed, out var current) || current == new Version(0, 0, 0))
            return null; // Unversioned source build; never install a production release over it.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await _client.GetAsync(LatestUrl, timeout.Token);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(timeout.Token);
        return ParseRelease(json, current);
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
            digest is null || !Regex.IsMatch(digest, @"^sha256:[0-9a-fA-F]{64}$"))
            throw new InvalidDataException("Invalid installer metadata.");

        return new ReleaseUpdate(latest, minimum, new Uri(expectedUrl), size, digest[7..]);
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
