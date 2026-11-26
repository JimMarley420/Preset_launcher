using System.Globalization;
using PresetLauncher.Services;

namespace PresetLauncher.Models;

public enum HistoryFilter
{
    All,
    Successful,
    Failed,
    Today,
    PartialFailure
}

internal enum ExecutionStatus { Unavailable, Successful, PartialFailure, Failed }

internal readonly record struct ExecutionResult(int Accepted, int Failed, int Unrecorded)
{
    public ExecutionStatus Status => (Accepted, Failed) switch
    {
        (> 0, 0) => ExecutionStatus.Successful,
        (> 0, > 0) => ExecutionStatus.PartialFailure,
        (0, > 0) => ExecutionStatus.Failed,
        _ => ExecutionStatus.Unavailable
    };

    public static ExecutionResult From(ExecutionHistory entry)
    {
        int accepted = entry.SuccessCount, failed = entry.FailedCount;
        if (accepted == 0 && failed == 0)
        {
            foreach (var detail in entry.Details)
            {
                if (detail.RecordedResult is true) accepted++;
                if (detail.RecordedResult is false) failed++;
            }
        }

        int expected = Math.Max(entry.ExecutablesCount, entry.Details.Count);
        return new(accepted, failed, (int)Math.Max(0, expected - (long)accepted - failed));
    }
}

public static class HistoryQuery
{
    public static IEnumerable<ExecutionHistory> Apply(IEnumerable<ExecutionHistory> history, HistoryFilter filter,
        DateTime today, string? search = null, CultureInfo? culture = null)
    {
        var term = search?.Trim();
        var compare = (culture ?? CultureInfo.CurrentCulture).CompareInfo;
        bool Matches(string text) => compare.IndexOf(text, term!, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

        return history.Where(item => (filter switch
        {
            HistoryFilter.Successful => ExecutionResult.From(item).Status == ExecutionStatus.Successful,
            HistoryFilter.PartialFailure => ExecutionResult.From(item).Status == ExecutionStatus.PartialFailure,
            HistoryFilter.Failed => ExecutionResult.From(item).Status == ExecutionStatus.Failed,
            HistoryFilter.Today => LocalDate(item.ExecutedAt) == today.Date,
            _ => true
        }) && (string.IsNullOrEmpty(term) || Matches(item.PresetName) ||
               item.Details.Any(detail => Matches(detail.ExecutableName))));
    }

    internal static DateTime? LocalTime(DateTime timestamp) =>
        timestamp == default ? null : timestamp.Kind == DateTimeKind.Utc ? timestamp.ToLocalTime() : timestamp;

    internal static DateTime? LocalDate(DateTime timestamp) => LocalTime(timestamp)?.Date;

    internal static IEnumerable<IGrouping<DateTime?, ExecutionHistory>> GroupByLocalDate(IEnumerable<ExecutionHistory> entries) =>
        entries.OrderByDescending(entry => LocalDate(entry.ExecutedAt))
            .ThenByDescending(entry => entry.ExecutedAt.ToUniversalTime())
            .GroupBy(entry => LocalDate(entry.ExecutedAt));

    internal static string DateHeading(DateTime? date, DateTime today, LocalizationService localization) =>
        date is null ? localization.Get("history.dateUnknown") :
        date == today.Date ? localization.Get("history.filter.today") :
        today.Date - date == TimeSpan.FromDays(1) ? localization.Get("history.yesterday") :
        date.Value.ToString("D", localization.Culture);
}
