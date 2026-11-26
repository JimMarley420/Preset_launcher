using System.Globalization;
using PresetLauncher.Services;

namespace PresetLauncher.Models;

public enum HistoryFilter
{
    All,
    Successful,
    Failed,
    Today,
    PartialFailure,
    Incomplete
}

internal enum ExecutionStatus { Unavailable, Successful, PartialFailure, Failed, Incomplete }

internal readonly record struct ExecutionResult(int Accepted, int Failed, int Unrecorded, bool CountsConflict)
{
    public ExecutionStatus Status => (Accepted, Failed, Unrecorded, CountsConflict) switch
    {
        (_, _, _, true) => ExecutionStatus.Incomplete,
        (0, 0, _, _) => ExecutionStatus.Unavailable,
        (_, _, > 0, _) => ExecutionStatus.Incomplete,
        (_, 0, _, _) => ExecutionStatus.Successful,
        (0, _, _, _) => ExecutionStatus.Failed,
        _ => ExecutionStatus.PartialFailure
    };

    public static ExecutionResult From(ExecutionHistory entry)
    {
        int detailAccepted = 0, detailFailed = 0;
        foreach (var detail in entry.Details)
        {
            if (detail.RecordedResult is true) detailAccepted++;
            if (detail.RecordedResult is false) detailFailed++;
        }

        int accepted = Math.Max(0, entry.SuccessCount), failed = Math.Max(0, entry.FailedCount);
        // Legacy records can omit both totals. Never add individual results to a reported total.
        if (accepted == 0 && failed == 0)
        {
            accepted = detailAccepted;
            failed = detailFailed;
        }

        long recorded = (long)accepted + failed;
        int expected = Math.Max(entry.ExecutablesCount, entry.Details.Count);
        bool conflict = entry.SuccessCount < 0 || entry.FailedCount < 0 || entry.ExecutablesCount < 0 ||
            detailAccepted > accepted || detailFailed > failed ||
            (entry.ExecutablesCount > 0 && (recorded > entry.ExecutablesCount || entry.Details.Count > entry.ExecutablesCount));
        return new(accepted, failed, (int)Math.Max(0, expected - recorded), conflict);
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
            HistoryFilter.Incomplete => ExecutionResult.From(item).Status == ExecutionStatus.Incomplete,
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
