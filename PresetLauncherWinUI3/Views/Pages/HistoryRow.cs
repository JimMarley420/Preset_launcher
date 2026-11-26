using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Models;
using PresetLauncher.Services;
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

        var icon = OutcomeIcon(result.Status);
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

        var details = new StackPanel { Spacing = 10 };
        details.Children.Add(BuildDetailActions(row));
        if (ExecutionResult.From(row.Entry).CountsConflict)
            details.Children.Add(Text(T("history.countsConflict"), "DesktopSecondaryText"));
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
            var block = BuildProgram(programs[index]);
            page.Children.Add(index + 1 < end ? Row(block) : block);
        }
    }

    private static Grid BuildProgram(ExecutionDetail detail)
    {
        var program = ReadProgram(detail, App.Localization);
        var status = detail.RecordedResult switch
        {
            true => ExecutionStatus.Successful,
            false => ExecutionStatus.Failed,
            null => ExecutionStatus.Unavailable
        };
        var icon = OutcomeIcon(status);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 2, 0, 0);
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Place(grid, icon, 0, 0);

        var body = new StackPanel { Spacing = 4 };
        var name = Text(program.Name, "DesktopSectionTitle");
        var category = Text(program.Category, status == ExecutionStatus.Failed ? null : "DesktopSecondaryText");
        category.MinWidth = 236;
        category.HorizontalAlignment = HorizontalAlignment.Left;
        category.MaxWidth = 310;
        body.Children.Add(AdaptivePair(name, category, 440));

        var path = Text(program.Path, "DesktopSecondaryText");
        path.MaxLines = 2;
        path.TextTrimming = TextTrimming.CharacterEllipsis;
        path.IsTextSelectionEnabled = true;
        ToolTipService.SetToolTip(path, program.Path);
        var recordedTime = Text(program.RecordedTime, "DesktopSecondaryText");
        if (status == ExecutionStatus.Failed)
        {
            body.Children.Add(path);
            AddFailureDetails(body, program, recordedTime);
        }
        else
        {
            recordedTime.MinWidth = 236;
            body.Children.Add(AdaptivePair(path, recordedTime, 590));
        }

        Place(grid, body, 0, 1);
        return grid;
    }

    private static void AddFailureDetails(StackPanel body, ProgramPresentation program, TextBlock recordedTime)
    {
        if (program.Suggestion is { } suggestion)
        {
            var help = Text(suggestion);
            help.Margin = new Thickness(0, 4, 0, 0);
            body.Children.Add(help);
        }

        var diagnostics = new StackPanel { Spacing = 2 };
        if (program.TechnicalError is { } error)
        {
            var message = Text(error, "DesktopSecondaryText");
            message.IsTextSelectionEnabled = true;
            diagnostics.Children.Add(message);
        }
        if (program.WindowsCode is { } code)
            diagnostics.Children.Add(Text(code, "DesktopSecondaryText"));
        diagnostics.Children.Add(recordedTime);
        body.Children.Add(new Border
        {
            Style = (Style)Application.Current.Resources["HistoryDiagnosticRule"],
            Margin = new Thickness(0, 4, 0, 0), Child = diagnostics
        });
    }

    private static FontIcon OutcomeIcon(ExecutionStatus status)
    {
        var (glyph, style) = status switch
        {
            ExecutionStatus.Successful => ("\uE73E", "HistorySuccessIcon"),
            ExecutionStatus.PartialFailure => ("\uE7BA", "HistoryPartialIcon"),
            ExecutionStatus.Failed => ("\uE711", "HistoryFailureIcon"),
            _ => ("\uE946", null)
        };
        var icon = Icon(glyph);
        if (style is not null)
            icon.Style = (Style)Application.Current.Resources[style];
        AutomationProperties.SetAccessibilityView(icon, AccessibilityView.Raw);
        return icon;
    }

    private static Grid BuildDetailActions(HistoryRowItem row)
    {
        var edit = App.Data.FindHistoryPreset(row.Entry) is not null
            ? Button("preset.edit", (_, _) => row.Page.OpenPreset(row.Entry), glyph: "\uE70F") : null;
        var copy = Button("history.copy", (_, _) => row.Page.CopyDiagnostics(Diagnostics(row)), glyph: "\uE8C8");
        var more = new Button { Content = Icon("\uE712"), Flyout = BuildActions(row, deleteOnly: true) };
        Label(more, T("history.actions"));
        var actions = edit is null ? Actions(copy, more) : Actions(edit, copy, more);
        var layout = new Grid();
        layout.Children.Add(actions);
        layout.SizeChanged += (_, args) =>
        {
            bool wide = args.NewSize.Width >= 440;
            if (edit is not null) edit.Content = wide ? T("preset.edit") : Icon("\uE70F");
            copy.Content = wide ? T("history.copyShort") : Icon("\uE8C8");
        };
        return layout;
    }

    private static MenuFlyout BuildActions(HistoryRowItem row, bool deleteOnly = false)
    {
        var menu = new MenuFlyout();
        void Add(string key, RoutedEventHandler handler)
        {
            var item = new MenuFlyoutItem { Text = T(key) };
            item.Click += handler;
            menu.Items.Add(item);
        }

        var entry = row.Entry;
        if (!deleteOnly)
        {
            if (App.Data.FindHistoryPreset(entry) is not null)
                Add("preset.edit", (_, _) => row.Page.OpenPreset(entry));
            Add("history.copy", (_, _) => row.Page.CopyDiagnostics(Diagnostics(row)));
            menu.Items.Add(new MenuFlyoutSeparator());
        }
        Add("history.delete", async (_, _) => await row.Page.DeleteEntryAsync(entry, row.PresetName));
        return menu;
    }

    private static string Diagnostics(HistoryRowItem row)
    {
        var entry = row.Entry;
        var result = ExecutionResult.From(entry);
        var summary = new List<string>
        {
            row.PresetName, EntryTiming(entry, "g"), T($"history.status.{result.Status}"),
            LaunchCounts(result), T("history.acceptanceHelp")
        };
        if (result.CountsConflict)
            summary.Add(T("history.countsConflict"));
        var programs = entry.Details.Select(detail => string.Join(Environment.NewLine, ProgramLines(detail, App.Localization)));
        return string.Join(Environment.NewLine + Environment.NewLine,
            new[] { string.Join(Environment.NewLine, summary) }.Concat(
                entry.Details.Count == 0 ? [T("history.noPrograms")] : programs));
    }

    internal static IEnumerable<string> ProgramLines(ExecutionDetail detail, LocalizationService localization)
    {
        var program = ReadProgram(detail, localization);
        yield return program.Name;
        bool leadWithError = detail.RecordedResult is false && detail.Failure == LaunchFailure.Unknown &&
            !string.IsNullOrWhiteSpace(detail.ErrorMessage);
        yield return leadWithError ? program.TechnicalError! : program.Category;
        yield return program.Path;
        yield return program.RecordedTime;
        if (program.WindowsCode is { } code) yield return code;
        if (program.TechnicalError is { } error && !leadWithError) yield return error;
        if (program.Suggestion is { } suggestion) yield return suggestion;
    }

    private static ProgramPresentation ReadProgram(ExecutionDetail detail, LocalizationService localization)
    {
        var result = detail.RecordedResult;
        var failure = detail.Failure;
        var category = result switch
        {
            true => localization.Get("history.requestAccepted"),
            false => localization.Get($"history.error.{failure}"),
            null => localization.Get("history.requestUnknown")
        };
        var recordedTime = HistoryQuery.LocalTime(detail.StartedAt) is { } timestamp
            ? localization.Get("history.recordedTime", timestamp.ToString("G", localization.Culture))
            : localization.Get("history.timeUnknown");
        return new ProgramPresentation(
            string.IsNullOrWhiteSpace(detail.ExecutableName) ? localization.Get("history.unnamedProgram") : detail.ExecutableName,
            category,
            string.IsNullOrWhiteSpace(detail.ExecutablePath) ? localization.Get("history.pathUnknown") : detail.ExecutablePath,
            recordedTime,
            result is false && failure != LaunchFailure.Unknown ? localization.Get($"history.help.{failure}") : null,
            result is false && detail.NativeErrorCode is int code ? localization.Get("history.windowsCode", code) : null,
            result is not false ? null : !string.IsNullOrWhiteSpace(detail.ErrorMessage)
                ? localization.Get("history.technicalError", detail.ErrorMessage)
                : detail.NativeErrorCode is null ? localization.Get("history.errorUnavailable") : null);
    }

    private sealed record ProgramPresentation(string Name, string Category, string Path, string RecordedTime,
        string? Suggestion, string? WindowsCode, string? TechnicalError);

    private static string Timestamp(DateTime timestamp, string format, LocalizationService localization) =>
        HistoryQuery.LocalTime(timestamp)?.ToString(format, localization.Culture) ?? localization.Get("history.timeUnknown");

    private static string EntryTiming(ExecutionHistory entry, string format = "t") => T("history.timeAndDuration",
        Timestamp(entry.ExecutedAt, format, App.Localization),
        entry.Duration is { } duration ? T("history.duration", duration.TotalSeconds) : T("history.durationUnknown"));

    private static string LaunchCounts(ExecutionResult result) => result.Unrecorded > 0 && !result.CountsConflict
        ? T("history.countsIncomplete", result.Accepted, result.Failed, result.Unrecorded)
        : T("history.counts", result.Accepted, result.Failed);
}
