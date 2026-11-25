using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Models;
using PresetLauncher.Services;
using static PresetLauncher.Views.DesktopUi;

namespace PresetLauncher.Views.Pages;

public sealed class SettingsPage : DesktopPage
{
    private readonly TextBlock _title;
    private readonly UpdateService _updates = new();
    private int _renderGeneration;

    public SettingsPage()
    {
        _title = Heading("settings.title", page: true);
        Header.Children.Add(_title);
    }

    internal override void Refresh()
    {
        base.Refresh();
        _title.Text = T("settings.title");
        var generation = ++_renderGeneration;
        Body.Children.Clear();

        var settings = App.Settings.CurrentSettings;
        Section("settings.appearance");
        AddSetting("settings.theme", Choice("settings.theme", settings.Theme, ["system", "light", "dark"],
            value => Change(settings => settings.Theme = value)));
        AddSetting("settings.backdrop", Choice("settings.backdrop", settings.Backdrop, ["none", "mica", "acrylic"],
            value => Change(settings => settings.Backdrop = value)));
        AddSetting("settings.language", Choice("language", App.Localization.Language, ["fr", "en"],
            value => Change(settings => settings.Language = value, refreshLanguage: true)));

        Section("settings.behavior");
        var confirmation = new ToggleSwitch
        {
            IsOn = settings.ConfirmBeforeLaunch,
            OnContent = T("settings.on"),
            OffContent = T("settings.off")
        };
        Label(confirmation, T("settings.confirmLaunch"));
        confirmation.Toggled += (_, _) =>
        {
            if (generation == _renderGeneration && confirmation.IsLoaded &&
                App.Settings.CurrentSettings.ConfirmBeforeLaunch != confirmation.IsOn)
                Change(settings => settings.ConfirmBeforeLaunch = confirmation.IsOn);
        };
        AddSetting("settings.confirmLaunch", confirmation);

        Section("settings.data");
        AddSetting("settings.dataPath", description: App.Data.StoragePath);
        AddSetting("data.backupScope", DataAction("data.export", Export), T("data.exportScope"));
        AddSetting("data.restoreTitle", Button("data.import", Import), T("data.restoreScope"));
        AddSetting("settings.cleanup", DataAction("settings.cleanup.action", Cleanup), T("settings.cleanup.subtitle"));

        Section("settings.about");
        AddSetting("settings.version", Button("settings.checkUpdate", CheckUpdates), _updates.GetInstalledVersion());

        Section("settings.reset.section");
        AddSetting("settings.reset.title", DataAction("settings.reset.action", Reset), T("settings.reset.subtitle"));
    }

    private static Button DataAction(string key, RoutedEventHandler action)
    {
        var button = Button(key, action);
        button.IsEnabled = !App.Data.LoadFailed;
        return button;
    }

    private void Section(string key)
    {
        var heading = Heading(key);
        heading.Margin = new Thickness(0, 10, 0, 0);
        Body.Children.Add(heading);
    }

    private void AddSetting(string key, FrameworkElement? action = null, string? description = null)
    {
        var labels = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(Text(T(key)));
        if (description is not null)
        {
            var detail = Text(description, "DesktopSecondaryText");
            if (action is null)
                detail.IsTextSelectionEnabled = true;
            labels.Children.Add(detail);
        }
        Body.Children.Add(Row(action is null ? labels : AdaptivePair(labels, action, 500)));
    }

    private ComboBox Choice(string category, string selected, string[] choices, Action<string> changed)
    {
        var generation = _renderGeneration;
        var selector = new ComboBox { MinWidth = 132 };
        Label(selector, T(category == "language" ? "settings.language" : category));
        foreach (var choice in choices)
            selector.Items.Add(new ComboBoxItem { Content = T($"{category}.{choice}"), Tag = choice });
        selector.SelectedIndex = Math.Max(0, Array.IndexOf(choices, selected));
        var committedSelection = (string)((ComboBoxItem)selector.SelectedItem).Tag;
        selector.SelectionChanged += (_, _) =>
        {
            if (generation != _renderGeneration || !selector.IsLoaded ||
                selector.SelectedItem is not ComboBoxItem { Tag: string value } || committedSelection == value)
                return;
            committedSelection = value;
            changed(value);
        };
        return selector;
    }

    private void Change(Action<AppSettings> change, bool refreshLanguage = false)
    {
        try
        {
            App.Settings.Update(change);
            if (refreshLanguage)
                Shell.ApplyPreferences();
            else
                App.Theme.Apply(App.Settings.CurrentSettings);
        }
        catch (Exception ex) when (DataService.IsFileError(ex))
        {
            Refresh();
            Report("settings.saveFailed", InfoBarSeverity.Error);
        }
    }

    private async void Export(object sender, RoutedEventArgs args) =>
        await Shell.RunOperationAsync(async () =>
        {
            if (!await Confirm(XamlRoot, "data.export", T("data.sensitivity"), "data.export"))
                return;
            var file = await BackupPicker().PickSaveFileAsync();
            if (file is null) return;
            await Task.Run(() => App.Data.ExportData(file.Path));
            Report("data.exported", InfoBarSeverity.Success);
        }, "settings.export.error");

    private async void Import(object sender, RoutedEventArgs args) =>
        await Shell.RunOperationAsync(async () =>
        {
            var file = await OpenPicker("data.import", ".json").PickSingleFileAsync();
            if (file is null) return;

            AppData backup;
            try { backup = await Task.Run(() => DataService.ReadBackup(file.Path)); }
            catch (Exception ex) when (DataService.IsFileError(ex))
            {
                Report("data.invalidBackup", InfoBarSeverity.Error);
                return;
            }

            var summary = new StackPanel { Spacing = 8 };
            summary.Children.Add(Text(T("data.importReplace")));
            summary.Children.Add(Text(T("data.importSummary", backup.Presets.Count, backup.History.Count, backup.UsageStats.Count)));
            if (!await Confirm(XamlRoot, "data.import", summary, "data.replace"))
                return;
            var previous = await Task.Run(() => App.Data.RestoreBackup(backup));
            Refresh();
            if (previous is null)
                Report("data.imported", InfoBarSeverity.Success);
            else
                Report("data.importedWithCopy", InfoBarSeverity.Success, System.IO.Path.GetFileName(previous));
        }, "data.restoreFailed");

    private async void Cleanup(object sender, RoutedEventArgs args) =>
        await Shell.RunOperationAsync(async () =>
        {
            var missing = await Task.Run(App.Data.FindMissingExecutables);
            if (missing.Count == 0)
            {
                Report("cleanup.none");
                return;
            }

            var preview = new StackPanel { Spacing = 6 };
            foreach (var group in missing.GroupBy(item => (item.PresetId, item.PresetName)))
            {
                preview.Children.Add(Text(group.Key.PresetName, "DesktopSectionTitle"));
                foreach (var item in group)
                    preview.Children.Add(PathText(item.Path));
            }
            var scroller = Scroll(preview);
            scroller.MaxHeight = 180;
            var panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(Text(T("cleanup.confirm", missing.Count)));
            panel.Children.Add(scroller);
            if (!await Confirm(XamlRoot, "settings.cleanup", panel, "cleanup.remove"))
                return;
            var removed = await Task.Run(() => App.Data.CleanupInvalidExecutables(missing));
            Report("cleanup.removed", InfoBarSeverity.Success, removed);
        }, "data.writeFailed");

    private async void Reset(object sender, RoutedEventArgs args) =>
        await Shell.RunOperationAsync(async () =>
        {
            if (!await Confirm(XamlRoot, "settings.reset.title", T("settings.reset.confirm"), "settings.reset.action"))
                return;
            try { await Task.Run(() => App.Data.ResetApplication(App.Settings)); }
            finally { Shell.ApplyPreferences(); }
            Report("settings.reset.success", InfoBarSeverity.Success);
        }, "settings.reset.failed");

    private async void CheckUpdates(object sender, RoutedEventArgs args) =>
        await Shell.RunOperationAsync(() => App.Current.Updates.CheckAsync(manual: true), "settings.updateCheckFailed");
}
