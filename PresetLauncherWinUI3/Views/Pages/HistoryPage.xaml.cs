using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Models;
using static PresetLauncher.Views.DesktopUi;

namespace PresetLauncher.Views.Pages;

public sealed class HistoryPage : DesktopPage
{
    private readonly ComboBox _filter = new();
    private readonly TextBlock _title;
    private readonly Button _clear;

    public HistoryPage()
    {
        _title = Heading("history.title", page: true);
        _clear = Button("history.clear", ClearHistory);
        Header.Children.Add(_title);
        Header.Children.Add(Actions(_filter, _clear));
        _filter.SelectionChanged += (_, _) => ShowHistory();
    }

    internal override void Refresh()
    {
        base.Refresh();
        _title.Text = T("history.title");
        _clear.Content = T("history.clear");
        Label(_filter, T("history.filter"));
        int selection = Math.Max(0, _filter.SelectedIndex);
        _filter.ItemsSource = new[] { T("history.filter.all"), T("history.filter.success"), T("history.filter.failures"), T("history.filter.today") };
        _filter.SelectedIndex = selection;
        ShowHistory();
    }

    private void ShowHistory()
    {
        Body.Children.Clear();
        var history = App.Data.Data.History;
        _clear.IsEnabled = history.Count > 0;
        var entries = HistoryQuery.Apply(history, (HistoryFilter)Math.Max(0, _filter.SelectedIndex), DateTime.Today).ToList();
        if (entries.Count == 0)
            Body.Children.Add(Text(T(history.Count == 0 ? "history.empty" : "history.noMatches"), "DesktopSecondaryText"));

        var culture = App.Localization.Culture;
        foreach (var item in entries)
        {
            var row = new StackPanel { Spacing = 3 };
            row.Children.Add(Text(item.PresetName, "DesktopSectionTitle"));
            var time = item.ExecutedAt.ToLocalTime().ToString("g", culture);
            row.Children.Add(Text(T("history.timing", time, item.Duration.TotalSeconds), "DesktopSecondaryText"));
            row.Children.Add(Text(item.SuccessText, item.FailedCount > 0 ? "DesktopWarningText" : "DesktopSecondaryText"));
            var failedPrograms = item.Details.Where(detail => !detail.Success).ToArray();
            if (failedPrograms.Length > 0)
            {
                var details = new StackPanel { Spacing = 4 };
                foreach (var failure in failedPrograms)
                    details.Children.Add(PathText(string.IsNullOrWhiteSpace(failure.ExecutableName) ? failure.ExecutablePath : failure.ExecutableName));
                row.Children.Add(new Expander { Header = T("history.failedPrograms"), Content = details });
            }
            Body.Children.Add(Row(row));
        }
    }

    private async void ClearHistory(object sender, RoutedEventArgs args) =>
        await Shell.RunOperationAsync(async () =>
        {
            if (!await Confirm(XamlRoot, "history.clear", T("history.clearConfirm"), "history.clear"))
                return;
            App.Data.ClearHistory();
            ShowHistory();
            Report("history.cleared", InfoBarSeverity.Success);
        }, "data.writeFailed");
}
