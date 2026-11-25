namespace PresetLauncher.Models;

public enum HistoryFilter
{
    All,
    Successful,
    Failed,
    Today
}

public static class HistoryQuery
{
    public static IEnumerable<ExecutionHistory> Apply(IEnumerable<ExecutionHistory> history, HistoryFilter filter, DateTime today) =>
        filter switch
        {
            HistoryFilter.Successful => history.Where(item => item.FailedCount == 0),
            HistoryFilter.Failed => history.Where(item => item.FailedCount > 0),
            HistoryFilter.Today => history.Where(item => item.ExecutedAt.ToLocalTime().Date == today.Date),
            _ => history
        };
}
