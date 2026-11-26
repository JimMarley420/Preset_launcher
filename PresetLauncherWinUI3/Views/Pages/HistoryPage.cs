using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Models;
using Windows.ApplicationModel.DataTransfer;
using static PresetLauncher.Views.DesktopUi;

namespace PresetLauncher.Views.Pages;

public sealed class HistoryPage : DesktopPage
{
    private static readonly (HistoryFilter Filter, string Key)[] Filters =
    [
        (HistoryFilter.All, "history.filter.all"),
        (HistoryFilter.Successful, "history.filter.success"),
        (HistoryFilter.PartialFailure, "history.filter.partial"),
        (HistoryFilter.Failed, "history.filter.failures"),
        (HistoryFilter.Today, "history.filter.today")
    ];

    private readonly TextBox _search = new();
    private readonly ComboBox _filter = new() { MinWidth = 148 };
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.None };
    private readonly TextBlock _empty = Text("", "DesktopSecondaryText");
    private readonly TextBlock _title;
    private readonly Button _clear;
    private bool _refreshing;

    public HistoryPage() : this(new Grid()) { }

    private HistoryPage(Grid content) : base(content)
    {
        _title = Heading("history.title", page: true);
        _clear = Button("history.clear", ClearHistory, glyph: "\uE74D");
        Header.Children.Add(_title);
        Header.Children.Add(AdaptivePair(_search, Actions(_filter, _clear), 520));

        _list.ItemTemplate = (DataTemplate)Application.Current.Resources["HistoryRowTemplate"];
        var containerStyle = new Style(typeof(ListViewItem));
        containerStyle.Setters.Add(new Setter(ListViewItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        containerStyle.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(0)));
        _list.ItemContainerStyle = containerStyle;
        ScrollViewer.SetHorizontalScrollMode(_list, ScrollMode.Disabled);
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        _empty.Margin = new Thickness(0, 20, 0, 0);
        content.Children.Add(_list);
        content.Children.Add(_empty);

        _search.TextChanged += (_, _) => ShowHistory();
        _filter.SelectionChanged += (_, _) => ShowHistory();
        SizeChanged += (_, _) => PositionTitle();
    }

    internal override void Refresh()
    {
        base.Refresh();
        _title.Text = T("history.title");
        _search.PlaceholderText = T("history.search");
        Label(_search, T("history.search"));
        Label(_filter, T("history.filter"));
        Label(_clear, T("history.clear"));
        AutomationProperties.SetName(_list, T("history.title"));
        var selected = Math.Max(0, _filter.SelectedIndex);
        _refreshing = true;
        _filter.ItemsSource = Filters.Select(option => T(option.Key)).ToArray();
        _filter.SelectedIndex = selected;
        _refreshing = false;
        PositionTitle();
        ShowHistory();
    }

    private void PositionTitle() => _title.Margin = new Thickness(Shell.UsesMinimalNavigation ? 36 : 0, 0, 0, 0);

    private void ShowHistory()
    {
        if (_refreshing) return;

        var history = App.Data.Data.History;
        _clear.IsEnabled = history.Count > 0;
        var filter = Filters[Math.Max(0, _filter.SelectedIndex)].Filter;
        var today = DateTime.Today;
        var matches = HistoryQuery.Apply(history, filter, today, _search.Text, App.Localization.Culture);
        var rows = HistoryQuery.GroupByLocalDate(matches)
            .SelectMany(group => group.Select((entry, index) => new HistoryRowItem(this, entry,
                index == 0 ? HistoryQuery.DateHeading(group.Key, today, App.Localization) : null)))
            .ToList();

        _list.ItemsSource = rows;
        _list.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (rows.Count == 0)
            _empty.Text = T(history.Count == 0 ? "history.empty" :
                string.IsNullOrWhiteSpace(_search.Text) ? "history.noMatches" : "history.noSearchMatches");
    }

    internal void OpenPreset(ExecutionHistory entry)
    {
        if (App.Data.FindHistoryPreset(entry) is { } preset)
            Shell.EditPreset(preset);
    }

    internal async void CopyDiagnostics(string contents) =>
        await Shell.RunOperationAsync(() =>
        {
            var package = new DataPackage();
            package.SetText(contents);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            Report("history.copied", InfoBarSeverity.Success);
            return Task.CompletedTask;
        }, "history.copyFailed", pageEnabled: true);

    internal async Task DeleteEntryAsync(ExecutionHistory entry, string name)
    {
        var deleted = false;
        await Shell.RunOperationAsync(async () =>
        {
            if (!await Confirm(XamlRoot, "history.delete", T("history.deleteConfirm", name), "history.delete"))
                return;
            App.Data.RemoveHistoryEntry(entry.Id);
            ShowHistory();
            Report("history.deleted", InfoBarSeverity.Success);
            deleted = true;
        }, "data.writeFailed");
        if (deleted) _search.Focus(FocusState.Programmatic);
    }

    private async void ClearHistory(object sender, RoutedEventArgs args)
    {
        var cleared = false;
        await Shell.RunOperationAsync(async () =>
        {
            if (!await Confirm(XamlRoot, "history.clear", T("history.clearConfirm"), "history.clear"))
                return;
            App.Data.ClearHistory();
            ShowHistory();
            Report("history.cleared", InfoBarSeverity.Success);
            cleared = true;
        }, "data.writeFailed");
        if (cleared) _search.Focus(FocusState.Programmatic);
    }
}

internal sealed record HistoryRowItem(HistoryPage Page, ExecutionHistory Entry, string? DateHeading)
{
    public string PresetName => string.IsNullOrWhiteSpace(Entry.PresetName) ? T("history.unnamedPreset") : Entry.PresetName;

    public override string ToString() => PresetName;
}
