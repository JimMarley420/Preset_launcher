using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Models;
using static PresetLauncher.Views.DesktopUi;

namespace PresetLauncher.Views.Pages;

public sealed class HistoryRow : UserControl
{
    private const int ProgramsPerPage = 30;
    private readonly StackPanel _layout = new() { Spacing = 6, Margin = new Thickness(0, 3, 8, 3) };
    private readonly TextBlock _date = Text("", "DesktopSectionTitle");
    private readonly Expander _expander = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Stretch
    };
    private HistoryRowItem? _row;

    public HistoryRow()
    {
        AutomationProperties.SetHeadingLevel(_date, AutomationHeadingLevel.Level2);
        _date.Margin = new Thickness(0, 8, 0, 2);
        _layout.Children.Add(_date);
        _layout.Children.Add(_expander);
        Content = _layout;

        DataContextChanged += (_, args) => Bind(args.NewValue as HistoryRowItem);
        _expander.Expanding += (_, _) => ShowDetails();
        _expander.Collapsed += (_, _) => _expander.Content = null;
    }

    private void Bind(HistoryRowItem? row)
    {
        _expander.IsExpanded = false;
        _expander.Content = null;
        _row = row;
        _date.Visibility = row?.DateHeading is not null ? Visibility.Visible : Visibility.Collapsed;
        _date.Text = row?.DateHeading ?? "";
        _expander.Header = row is null ? null : BuildHeader(row);
        _expander.ContextFlyout = row is null ? null : BuildActions(row);
    }

    private UIElement BuildHeader(HistoryRowItem row)
    {
        var entry = row.Entry;
        var result = ExecutionResult.From(entry);
        var status = T($"history.status.{result.Status}");
        var name = row.PresetName;
        var timing = EntryTiming(entry);
        var counts = LaunchCounts(result);
        var heading = new StackPanel { Spacing = 2 };
        heading.Children.Add(Text(name, "DesktopSectionTitle"));
        heading.Children.Add(Text(timing, "DesktopSecondaryText"));

        var (glyph, style) = result.Status switch
        {
            ExecutionStatus.Successful => ("\uE73E", "HistorySuccessIcon"),
            ExecutionStatus.PartialFailure => ("\uE7BA", "HistoryPartialIcon"),
            ExecutionStatus.Failed => ("\uE711", "HistoryFailureIcon"),
            _ => ("\uE946", null)
        };
        var icon = Icon(glyph);
        if (style is not null)
            icon.Style = (Style)Application.Current.Resources[style];
        var outcome = new StackPanel { Spacing = 2, MinWidth = 168, MaxWidth = 260, HorizontalAlignment = HorizontalAlignment.Left };
        outcome.Children.Add(Text(status));
        outcome.Children.Add(Text(counts, "DesktopSecondaryText"));

        var header = new Grid { ColumnSpacing = 12, Padding = new Thickness(0, 4, 0, 4) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        icon.VerticalAlignment = VerticalAlignment.Center;
        Place(header, icon, 0, 0);
        Place(header, AdaptivePair(heading, outcome, 455), 0, 1);
        AutomationProperties.SetName(_expander, $"{name}. {status}. {timing}. {counts}");
        AutomationProperties.SetHelpText(_expander, T("history.acceptanceHelp"));
        return header;
    }

    private void ShowDetails()
    {
        if (_row is not { } row) return;

        var details = new StackPanel { Spacing = 8 };
        var actions = new DropDownButton
        {
            Content = T("history.actions"),
            Flyout = BuildActions(row)
        };
        details.Children.Add(actions);
        var programs = row.Entry.Details;
        if (programs.Count == 0)
            details.Children.Add(Text(T("history.noPrograms"), "DesktopSecondaryText"));
        else
            AddPrograms(details, programs);
        _expander.Content = details;
    }

    private static void AddPrograms(StackPanel details, IReadOnlyList<ExecutionDetail> programs)
    {
        var page = new StackPanel { Spacing = 8 };
        if (programs.Count <= ProgramsPerPage)
        {
            RenderPrograms(page, programs, 0, programs.Count);
            details.Children.Add(page);
            return;
        }

        int offset = 0;
        var range = Text("", "DesktopSecondaryText");
        var previous = new Button { Content = Icon("\uE72B") };
        var next = new Button { Content = Icon("\uE72A") };
        Label(previous, T("history.previousPrograms"));
        Label(next, T("history.nextPrograms"));
        previous.Click += (_, _) => Move(-ProgramsPerPage);
        next.Click += (_, _) => Move(ProgramsPerPage);
        details.Children.Add(Actions(previous, next));
        details.Children.Add(range);
        details.Children.Add(page);
        Move(0);

        void Move(int step)
        {
            offset += step;
            int end = Math.Min(offset + ProgramsPerPage, programs.Count);
            range.Text = T("history.programRange", offset + 1, end, programs.Count);
            RenderPrograms(page, programs, offset, end);
            previous.IsEnabled = offset > 0;
            next.IsEnabled = end < programs.Count;
            if (step != 0 && !(step > 0 ? next.IsEnabled : previous.IsEnabled))
                (step > 0 ? previous : next).Focus(FocusState.Programmatic);
        }
    }

    private static void RenderPrograms(StackPanel page, IReadOnlyList<ExecutionDetail> programs, int start, int end)
    {
        page.Children.Clear();
        for (int index = start; index < end; index++)
        {
            var block = new StackPanel { Spacing = 3 };
            var lines = ProgramLines(programs[index]).ToArray();
            block.Children.Add(Text(lines[0], "DesktopSectionTitle"));
            block.Children.Add(Text(lines[1]));
            foreach (var line in lines.Skip(2))
            {
                var text = Text(line, "DesktopSecondaryText");
                text.IsTextSelectionEnabled = true;
                AutomationProperties.SetName(text, line);
                block.Children.Add(text);
            }
            page.Children.Add(index + 1 < end ? Row(block) : block);
        }
    }

    private static MenuFlyout BuildActions(HistoryRowItem row)
    {
        var menu = new MenuFlyout();
        void Add(string key, RoutedEventHandler handler)
        {
            var item = new MenuFlyoutItem { Text = T(key) };
            item.Click += handler;
            menu.Items.Add(item);
        }

        var entry = row.Entry;
        if (App.Data.FindHistoryPreset(entry) is not null)
            Add("preset.edit", (_, _) => row.Page.OpenPreset(entry));
        Add("history.copy", (_, _) => row.Page.CopyDiagnostics(Diagnostics(row)));
        menu.Items.Add(new MenuFlyoutSeparator());
        Add("history.delete", async (_, _) => await row.Page.DeleteEntryAsync(entry, row.PresetName));
        return menu;
    }

    private static string Diagnostics(HistoryRowItem row)
    {
        var entry = row.Entry;
        var result = ExecutionResult.From(entry);
        var summary = new[]
        {
            row.PresetName, EntryTiming(entry, "g"), T($"history.status.{result.Status}"),
            LaunchCounts(result), T("history.acceptanceHelp")
        };
        var programs = entry.Details.Select(detail => string.Join(Environment.NewLine, ProgramLines(detail)));
        return string.Join(Environment.NewLine + Environment.NewLine,
            new[] { string.Join(Environment.NewLine, summary) }.Concat(
                entry.Details.Count == 0 ? [T("history.noPrograms")] : programs));
    }

    private static IEnumerable<string> ProgramLines(ExecutionDetail detail)
    {
        yield return string.IsNullOrWhiteSpace(detail.ExecutableName) ? T("history.unnamedProgram") : detail.ExecutableName;
        var result = detail.RecordedResult;
        yield return result switch
        {
            true => T("history.requestAccepted"),
            false => T($"history.error.{detail.Failure}"),
            null => T("history.requestUnknown")
        };
        yield return string.IsNullOrWhiteSpace(detail.ExecutablePath) ? T("history.pathUnknown") : detail.ExecutablePath;
        yield return T("history.recordedTime", Timestamp(detail.StartedAt, "G"));

        if (result is not false) yield break;
        if (detail.NativeErrorCode is int code)
            yield return T("history.windowsCode", code);
        if (!string.IsNullOrWhiteSpace(detail.ErrorMessage))
            yield return T("history.technicalError", detail.ErrorMessage);
        else if (detail.NativeErrorCode is null)
            yield return T("history.errorUnavailable");
        if (detail.Failure != LaunchFailure.Unknown)
            yield return T($"history.help.{detail.Failure}");
    }

    private static string Timestamp(DateTime timestamp, string format) =>
        HistoryQuery.LocalTime(timestamp)?.ToString(format, App.Localization.Culture) ?? T("history.timeUnknown");

    private static string EntryTiming(ExecutionHistory entry, string format = "t") => T("history.timeAndDuration",
        Timestamp(entry.ExecutedAt, format),
        entry.Duration is { } duration ? T("history.duration", duration.TotalSeconds) : T("history.durationUnknown"));

    private static string LaunchCounts(ExecutionResult result) => result.Unrecorded > 0
        ? T("history.countsIncomplete", result.Accepted, result.Failed, result.Unrecorded)
        : T("history.counts", result.Accepted, result.Failed);
}
