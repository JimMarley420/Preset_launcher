using System.Globalization;
using System.Text.Json;
using PresetLauncher.Models;

namespace PresetLauncher.Services;

internal static class DataBackup
{
    public const int MaxBytes = 32 * 1024 * 1024;
    public const string Format = "PresetLauncher.AppData";
    public const int Version = 1;

    private static readonly JsonDocumentOptions Options = new() { MaxDepth = 32, AllowDuplicateProperties = false };

    public static JsonDocument Read(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length > MaxBytes)
            throw new InvalidDataException("File exceeds the backup limit.");

        // Legacy files may include a UTF BOM. Decode it before enforcing strict JSON syntax.
        using var reader = new StreamReader(file);
        return JsonDocument.Parse(reader.ReadToEnd(), Options);
    }

    public static JsonDocument ReadText(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes)
            throw new InvalidDataException("File exceeds the backup limit.");
        return JsonDocument.Parse(json, Options);
    }

    public static AppData Parse(JsonElement root)
    {
        RequireObject(root);
        if (root.TryGetProperty("Format", out var format))
        {
            if (format.ValueKind != JsonValueKind.String || format.GetString() != Format ||
                !root.TryGetProperty("Version", out var version) || version.ValueKind != JsonValueKind.Number ||
                !version.TryGetInt32(out var value) || value != Version ||
                !root.TryGetProperty("Data", out var data))
                throw new InvalidDataException("Unsupported backup.");
            RequireObject(data);
            root = data;
        }

        ValidateEntries(root, "Presets", preset => ValidatePreset(preset));
        ValidateEntries(root, "History", ValidateHistory);
        var stats = Field(root, "UsageStats", JsonValueKind.Object, required: true);
        foreach (var stat in stats.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(stat.Name))
                throw new InvalidDataException("Invalid usage path.");
            ValidateNumber(stat.Value, int.MaxValue);
        }

        var result = root.Deserialize<AppData>()!;
        foreach (var preset in result.Presets)
            preset.ValidateAllPaths();
        return result;
    }

    public static void ValidatePreset(JsonElement preset)
    {
        RequireObject(preset);
        Field(preset, "Name", JsonValueKind.String, required: true);
        Field(preset, "Description", JsonValueKind.String);
        Field(preset, "IconPath", JsonValueKind.String);
        Field(preset, "PasswordHash", JsonValueKind.String);
        Boolean(preset, "IsLocked");
        Date(preset, "CreatedAt");
        Date(preset, "LastModified", nullable: true);
        Date(preset, "LastLaunched", nullable: true);
        Number(preset, "LaunchCount");

        ValidateEntries(preset, "Executables", item =>
        {
            Field(item, "Path", JsonValueKind.String, required: true);
            Field(item, "Name", JsonValueKind.String);
            Field(item, "Arguments", JsonValueKind.String);
            Boolean(item, "RunAsAdmin");
            Boolean(item, "IsValid");
            Date(item, "LastLaunched", nullable: true);
            Number(item, "LaunchCount");
            Number(item, "Order");
            Number(item, "DelaySeconds", PresetDraft.MaxDelay);
        });
    }

    private static void ValidateHistory(JsonElement history)
    {
        Field(history, "PresetId", JsonValueKind.String);
        Field(history, "PresetName", JsonValueKind.String);
        Date(history, "ExecutedAt");
        Number(history, "ExecutablesCount");
        Number(history, "SuccessCount");
        Number(history, "FailedCount");
        var duration = Field(history, "Duration", JsonValueKind.String);
        if (duration.ValueKind != JsonValueKind.Undefined &&
            (!TimeSpan.TryParseExact(duration.GetString(), "c", CultureInfo.InvariantCulture, out var value) || value < TimeSpan.Zero))
            throw new InvalidDataException("Invalid launch duration.");

        ValidateEntries(history, "Details", detail =>
        {
            Field(detail, "ExecutableName", JsonValueKind.String);
            Field(detail, "ExecutablePath", JsonValueKind.String);
            Boolean(detail, "Success");
            Field(detail, "ErrorMessage", JsonValueKind.String, nullable: true);
            Date(detail, "StartedAt");
        }, uniqueIds: false);
    }

    private static void ValidateEntries(JsonElement parent, string field, Action<JsonElement> validate, bool uniqueIds = true)
    {
        var entries = Field(parent, field, JsonValueKind.Array, required: true);
        var ids = new HashSet<Guid>();
        foreach (var item in entries.EnumerateArray())
        {
            RequireObject(item);
            if (uniqueIds)
            {
                var identity = Field(item, "Id", JsonValueKind.String, required: true);
                if (!Guid.TryParse(identity.GetString(), out var id) || id == Guid.Empty || !ids.Add(id))
                    throw new InvalidDataException("Invalid or duplicate identity.");
            }
            validate(item);
        }
    }

    private static void RequireObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Expected an object.");
    }

    private static JsonElement Field(JsonElement parent, string name, JsonValueKind kind, bool required = false, bool nullable = false)
    {
        if (parent.TryGetProperty(name, out var value)
            ? value.ValueKind != kind && !(nullable && value.ValueKind == JsonValueKind.Null)
            : required)
            throw new InvalidDataException("Invalid field type.");
        return value;
    }

    private static void Boolean(JsonElement parent, string name)
    {
        if (parent.TryGetProperty(name, out var value) && value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException("Invalid boolean value.");
    }

    private static void Date(JsonElement parent, string name, bool nullable = false)
    {
        var value = Field(parent, name, JsonValueKind.String, nullable: nullable);
        if (value.ValueKind == JsonValueKind.String && !value.TryGetDateTime(out _))
            throw new InvalidDataException("Invalid timestamp.");
    }

    private static void Number(JsonElement parent, string name, int maximum = int.MaxValue)
    {
        var value = Field(parent, name, JsonValueKind.Number);
        if (value.ValueKind != JsonValueKind.Undefined)
            ValidateNumber(value, maximum);
    }

    private static void ValidateNumber(JsonElement token, int maximum)
    {
        if (token.ValueKind != JsonValueKind.Number || !token.TryGetInt32(out var number) ||
            number < 0 || number > maximum)
            throw new InvalidDataException("Invalid numeric value.");
    }
}
