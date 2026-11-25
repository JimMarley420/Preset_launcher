using System.Collections.ObjectModel;

namespace PresetLauncher.Models;

public sealed class PresetDraft
{
    public const int MaxDelay = 300;
    private readonly string _name;
    private readonly string _description;
    private readonly (string Id, bool Admin, int Delay)[] _programs;

    public string? PresetId { get; }
    public string Name { get; set; }
    public string Description { get; set; }
    public ObservableCollection<ExecutableItem> Programs { get; }

    public PresetDraft(Preset? source)
    {
        PresetId = source?.Id;
        Name = _name = source?.Name ?? "";
        Description = _description = source?.Description ?? "";
        Programs = new(source?.Executables.OrderBy(program => program.Order).Select(Copy) ?? []);
        _programs = Programs.Select(Options).ToArray();
    }

    public bool IsDirty => Name != _name || Description != _description ||
        !_programs.SequenceEqual(Programs.Select(Options));

    public bool HasValidDelays => Programs.All(program => program.DelaySeconds is >= 0 and <= MaxDelay);

    internal Preset Apply(Preset? current)
    {
        if (string.IsNullOrWhiteSpace(Name) || !HasValidDelays)
            throw new ArgumentException("Invalid preset draft.");
        if (PresetId != current?.Id)
            throw new InvalidOperationException("The original preset no longer exists.");

        var edited = current is null ? new Preset() : Copy(current);
        if (current is not null && !IsDirty)
            return edited;
        edited.Name = Name.Trim();
        edited.Description = Description;
        if (current is not null)
            edited.LastModified = DateTime.Now;

        var livePrograms = edited.Executables.ToDictionary(item => item.Id);
        edited.Executables = new(Programs.Select((draft, index) =>
        {
            var item = livePrograms.TryGetValue(draft.Id, out var existing) ? existing : Copy(draft);
            item.RunAsAdmin = draft.RunAsAdmin;
            item.DelaySeconds = draft.DelaySeconds;
            item.Order = index;
            item.ValidatePath();
            return item;
        }));
        return edited;
    }

    private static (string, bool, int) Options(ExecutableItem item) => (item.Id, item.RunAsAdmin, item.DelaySeconds);

    internal static Preset Copy(Preset source) => new()
    {
        Id = source.Id, Name = source.Name, Description = source.Description, IconPath = source.IconPath,
        IsLocked = source.IsLocked, PasswordHash = source.PasswordHash, CreatedAt = source.CreatedAt,
        LastModified = source.LastModified, LaunchCount = source.LaunchCount, LastLaunched = source.LastLaunched,
        Executables = new(source.Executables.Select(Copy))
    };

    private static ExecutableItem Copy(ExecutableItem source) => new()
    {
        Id = source.Id, Name = source.Name, Path = source.Path, Arguments = source.Arguments,
        RunAsAdmin = source.RunAsAdmin, DelaySeconds = source.DelaySeconds, Order = source.Order,
        IsValid = source.IsValid, LaunchCount = source.LaunchCount, LastLaunched = source.LastLaunched
    };
}
