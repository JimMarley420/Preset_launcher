using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Models;
using PresetLauncher.Services;
using static PresetLauncher.Views.DesktopUi;

namespace PresetLauncher.Views.Pages;

public sealed class PresetsPage : DesktopPage
{
    private readonly LauncherService _launcher = new();
    private readonly TextBox _search = new();
    private readonly TextBlock _title;
    private readonly Button _create;
    private readonly Button _import;
    private readonly ProgressBar _progress = new() { IsIndeterminate = true, Visibility = Visibility.Collapsed };

    public PresetsPage()
    {
        _title = Heading("preset.title", page: true);
        _create = Button("preset.new", (_, _) => Shell.EditPreset(null), accent: true);
        _import = Button("preset.import", Import);
        Header.Children.Add(_title);
        Header.Children.Add(AdaptivePair(_search, Actions(_create, _import), 540));
        Header.Children.Add(_progress);
        _search.TextChanged += (_, _) => ShowPresets();
    }

    internal override void Refresh()
    {
        base.Refresh();
        _title.Text = T("preset.title");
        _create.Content = T("preset.new");
        var wasUnavailable = !_create.IsEnabled;
        _create.IsEnabled = !App.Data.LoadFailed;
        _import.Content = T("preset.import");
        _import.IsEnabled = !App.Data.LoadFailed;
        _search.PlaceholderText = T("search.placeholder");
        Label(_search, _search.PlaceholderText);
        Label(_progress, T("execution.inProgress"));
        if (App.Data.LoadFailed)
            Report("data.loadFailed", InfoBarSeverity.Error);
        else if (wasUnavailable)
            ClearStatus();
        ShowPresets();
    }

    private void ShowPresets()
    {
        Body.Children.Clear();
        var search = _search.Text.Trim();
        var presets = App.Data.Data.Presets.Where(p =>
            App.Localization.Culture.CompareInfo.IndexOf(p.Name, search, System.Globalization.CompareOptions.IgnoreCase) >= 0 ||
            App.Localization.Culture.CompareInfo.IndexOf(p.Description, search, System.Globalization.CompareOptions.IgnoreCase) >= 0).ToArray();
        if (presets.Length == 0)
            Body.Children.Add(Text(T(search.Length == 0 ? "preset.nopresets" : "preset.noMatches"), "DesktopSecondaryText"));

        foreach (var preset in presets)
        {
            var name = Text(preset.Name, "DesktopSectionTitle");
            name.MaxLines = 2;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            ToolTipService.SetToolTip(name, preset.Name);
            var labels = new StackPanel { Spacing = 3 };
            labels.Children.Add(name);
            var missing = preset.Executables.Count(program => !program.IsValid);
            labels.Children.Add(Text(preset.ProgramsText, "DesktopSecondaryText"));
            if (missing > 0)
                labels.Children.Add(Text(T("preset.programSummary", missing), "DesktopWarningText"));
            if (!string.IsNullOrWhiteSpace(preset.Description))
            {
                var description = Text(preset.Description, "DesktopSecondaryText");
                description.MaxLines = 2;
                description.TextTrimming = TextTrimming.CharacterEllipsis;
                ToolTipService.SetToolTip(description, preset.Description);
                labels.Children.Add(description);
            }

            var menu = new MenuFlyout();
            AddMenuItem(menu, "menu.closeProcesses", async (_, _) => await CloseProcesses(preset));
            AddMenuItem(menu, "menu.delete", async (_, _) => await DeletePreset(preset));
            var more = new Button { Content = Icon("\uE712"), Flyout = menu };
            Label(more, T("menu.more", preset.Name));
            var edit = Button("preset.edit", (_, _) => Shell.EditPreset(preset), glyph: "\uE70F");
            Label(edit, T("preset.editNamed", preset.Name));
            var row = AdaptivePair(labels, Actions(CreateLaunchButton(preset), edit, more), 390);
            Body.Children.Add(Row(row));
        }
    }

    private DropDownButton CreateLaunchButton(Preset preset)
    {
        var modes = new MenuFlyout();
        AddMenuItem(modes, "preset.launchAll", async (_, _) => await Launch(preset, LaunchMode.All));
        AddMenuItem(modes, "preset.launchMissing", async (_, _) => await Launch(preset, LaunchMode.MissingOnly));
        var launch = new DropDownButton
        {
            Content = Actions(Icon("\uE768", accent: true), Text(T("preset.launch"))),
            Flyout = modes
        };
        Label(launch, T("preset.launchNamed", preset.Name));
        return launch;
    }

    private static void AddMenuItem(MenuFlyout menu, string key, RoutedEventHandler click)
    {
        var item = new MenuFlyoutItem { Text = T(key) };
        item.Click += click;
        menu.Items.Add(item);
    }

    private async void Import(object sender, RoutedEventArgs args) =>
        await Shell.RunOperationAsync(async () =>
        {
            var file = await OpenPicker("preset.import", ".json").PickSingleFileAsync();
            if (file is null) return;
            var preset = await Task.Run(() => App.Data.ImportPresetFile(file.Path));
            if (preset is null)
            {
                Report("preset.importError", InfoBarSeverity.Error);
                return;
            }
            App.Data.AddPreset(preset);
            ShowPresets();
            Report("preset.imported", InfoBarSeverity.Success);
        }, "preset.importWriteFailed");

    private Task<bool> DeletePreset(Preset preset) => Shell.RunOperationAsync(async () =>
    {
        if (!await Confirm(XamlRoot, "dialog.delete.title", T("dialog.delete.message", preset.Name), "menu.delete"))
            return;
        App.Data.RemovePreset(preset);
        ShowPresets();
        Report("preset.deleted", InfoBarSeverity.Success);
    }, "data.writeFailed");

    private Task<bool> CloseProcesses(Preset preset) => Shell.RunOperationAsync(async () =>
    {
        if (!await Confirm(XamlRoot, "dialog.close.title", T("dialog.close.message", preset.Name), "menu.closeProcesses"))
            return;
        var count = await _launcher.ClosePresetProcessesAsync(preset);
        Report("execution.processesClosed", InfoBarSeverity.Informational, count);
    }, "execution.closeFailed");

    private Task<bool> Launch(Preset preset, LaunchMode mode) => Shell.RunOperationAsync(async () =>
    {
        var action = LaunchInteraction.ConfirmationAction(App.Settings.CurrentSettings, mode);
        if (action is not null && !await Confirm(XamlRoot, action, T("dialog.launch.message", preset.Name), action))
            return;

        ClearStatus();
        _progress.Visibility = Visibility.Visible;
        try
        {
            _launcher.StatusChanged += LaunchStatus;
            var result = mode == LaunchMode.All
                ? await LaunchAll(preset)
                : await Task.Run(() => _launcher.LaunchPresetMissing(preset));
            Report(result.FailedCount > 0 ? "execution.resultsFailed" : "execution.results",
                result.FailedCount > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Informational,
                result.SuccessCount, result.FailedCount);
        }
        finally
        {
            _launcher.StatusChanged -= LaunchStatus;
            _progress.Visibility = Visibility.Collapsed;
        }
    }, "execution.saveFailed");

    private async Task<(int SuccessCount, int FailedCount)> LaunchAll(Preset preset)
    {
        var history = await _launcher.LaunchPresetAsync(preset);
        return (history.SuccessCount, history.FailedCount);
    }

    private void LaunchStatus(object? sender, string program) =>
        Report("execution.launching", InfoBarSeverity.Informational, program);
}
