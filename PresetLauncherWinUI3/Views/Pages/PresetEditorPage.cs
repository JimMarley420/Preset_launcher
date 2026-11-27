using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PresetLauncher.Models;
using static PresetLauncher.Views.DesktopUi;

namespace PresetLauncher.Views.Pages;

public sealed class PresetEditorPage : DesktopPage
{
    private readonly PresetDraft _draft;
    private readonly TextBox _name = new();
    private readonly TextBox _description = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _nameError = Text("", "DesktopWarningText");
    private readonly StackPanel _programs = new() { Spacing = 6 };
    private readonly Dictionary<string, string> _invalidDelays = [];
    private bool _leaving;

    public bool IsDirty => _draft.IsDirty || _invalidDelays.Count > 0;

    public PresetEditorPage(Preset? preset)
    {
        _draft = new(preset);
        var title = Heading(preset is null ? "preset.new" : "preset.edit", page: true);
        Header.Children.Add(Actions(Button("editor.back", Cancel, glyph: "\uE72B"), title));
        _name.Header = T("preset.name");
        _name.Text = _draft.Name;
        _name.TextChanged += (_, _) =>
        {
            _draft.Name = _name.Text;
            if (!string.IsNullOrWhiteSpace(_name.Text)) _nameError.Visibility = Visibility.Collapsed;
        };
        _description.Header = T("preset.description");
        _description.Text = _draft.Description;
        _description.TextChanged += (_, _) => _draft.Description = _description.Text;
        var description = new Expander
        {
            Header = T("preset.description"),
            Content = _description,
            IsExpanded = _description.Text.Length > 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        _description.Header = null;
        Label(_description, T("preset.description"));

        Body.Children.Add(_name);
        _nameError.Visibility = Visibility.Collapsed;
        Body.Children.Add(_nameError);
        Body.Children.Add(description);
        Body.Children.Add(AdaptivePair(Heading("exe.executables"),
            Actions(Button("exe.addFromFile", AddFiles), Button("exe.addFromProcess", AddProcess)), 530));
        Body.Children.Add(_programs);

        var save = Button(preset is null ? "editor.create" : "editor.save", Save, accent: true);
        var footer = Actions(Button("dialog.cancel", Cancel), save);
        footer.HorizontalAlignment = HorizontalAlignment.Right;
        Footer.Content = footer;
        var escape = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, args) => { args.Handled = true; Cancel(this, new RoutedEventArgs()); };
        KeyboardAccelerators.Add(escape);
        Loaded += (_, _) => _name.Focus(FocusState.Programmatic);
        ShowPrograms();
    }

    public async Task<bool> CanLeaveAsync(XamlRoot? dialogRoot = null)
    {
        if (!IsDirty)
            return true;
        if (_leaving)
            return false;
        _leaving = true;
        try
        {
            return await Confirm(dialogRoot ?? XamlRoot, "editor.discard", T("editor.discardMessage"), "editor.discardAction");
        }
        finally { _leaving = false; }
    }

    private async void Cancel(object sender, RoutedEventArgs args)
    {
        await Shell.RunOperationAsync(async () =>
        {
            if (await CanLeaveAsync())
                Shell.ReturnToPresets();
        }, "dialog.error");
    }

    private async void Save(object sender, RoutedEventArgs args)
    {
        _nameError.Text = string.IsNullOrWhiteSpace(_draft.Name) ? T("dialog.preset.nameRequired") : "";
        if (_nameError.Text.Length > 0)
        {
            _nameError.Visibility = Visibility.Visible;
            _name.Focus(FocusState.Programmatic);
            return;
        }
        if (_invalidDelays.Count > 0 || !_draft.HasValidDelays)
        {
            Report("editor.delayError", InfoBarSeverity.Error, PresetDraft.MaxDelay);
            return;
        }

        await Shell.RunOperationAsync(() =>
        {
            App.Data.SaveDraft(_draft);
            Shell.ReturnToPresets();
            return Task.CompletedTask;
        }, "editor.saveFailed");
    }

    private async void AddFiles(object sender, RoutedEventArgs args) =>
        await Shell.RunOperationAsync(async () =>
        {
            var files = await OpenPicker("exe.addFromFile", ".exe", ".bat", ".cmd", ".lnk", "*").PickMultipleFilesAsync();
            if (files is null || files.Count == 0) return;
            foreach (var file in files)
                AddPath(file.Path);
            ShowPrograms();
        }, "editor.addFailed");

    private async void AddProcess(object sender, RoutedEventArgs args) =>
        await Shell.RunOperationAsync(async () =>
        {
            var path = await new ProcessSelector().PickAsync(XamlRoot);
            if (path is null) return;
            AddPath(path);
            ShowPrograms();
        }, "process.error");

    private void AddPath(string path)
    {
        var item = new ExecutableItem(path);
        item.ValidatePath();
        _draft.Programs.Add(item);
    }

    private void ShowPrograms(string? focusId = null)
    {
        _programs.Children.Clear();
        if (_draft.Programs.Count == 0)
            _programs.Children.Add(Text(T("editor.empty"), "DesktopSecondaryText"));

        foreach (var program in _draft.Programs)
        {
            var name = Text(program.Name, "DesktopSectionTitle");
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.MaxLines = 2;
            var labels = new StackPanel { Spacing = 3 };
            var programLine = new Grid { ColumnSpacing = 8 };
            programLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            programLine.ColumnDefinitions.Add(new ColumnDefinition());
            Place(programLine, Icon("\uE7FC", accent: true), 0, 0);
            Place(programLine, name, 0, 1);
            labels.Children.Add(programLine);
            labels.Children.Add(PathText(program.Path));
            if (!program.IsValid)
                labels.Children.Add(Text(T("exe.invalid"), "DesktopWarningText"));

            var position = _draft.Programs.IndexOf(program);
            var up = Button("exe.moveUp", (_, _) => Move(program, -1), glyph: "\uE74A");
            var down = Button("exe.moveDown", (_, _) => Move(program, 1), glyph: "\uE74B");
            up.IsEnabled = position > 0;
            down.IsEnabled = position + 1 < _draft.Programs.Count;
            var remove = Button("exe.remove", (_, _) =>
            {
                _draft.Programs.Remove(program);
                _invalidDelays.Remove(program.Id);
                if (_invalidDelays.Count == 0) ClearStatus();
                ShowPrograms();
            }, glyph: "\uE74D");
            _programs.Children.Add(Row(AdaptivePair(labels, Actions(AdvancedOptions(program), up, down, remove), 400)));
            if (program.Id == focusId)
                (up.IsEnabled ? up : down).Focus(FocusState.Programmatic);
        }
    }

    private void Move(ExecutableItem item, int offset)
    {
        if (_invalidDelays.Count > 0)
        {
            Report("editor.delayError", InfoBarSeverity.Error, PresetDraft.MaxDelay);
            return;
        }
        int index = _draft.Programs.IndexOf(item);
        _draft.Programs.Move(index, index + offset);
        ShowPrograms(item.Id);
    }

    private Button AdvancedOptions(ExecutableItem program)
    {
        var admin = new CheckBox { Content = T("exe.runAsAdmin"), IsChecked = program.RunAsAdmin };
        admin.Checked += (_, _) => program.RunAsAdmin = true;
        admin.Unchecked += (_, _) => program.RunAsAdmin = false;
        var delay = new TextBox { Header = T("exe.delay"), Text = _invalidDelays.GetValueOrDefault(program.Id) ?? program.DelaySeconds.ToString(App.Localization.Culture) };
        var error = Text(T("editor.delayError", PresetDraft.MaxDelay), "DesktopWarningText");
        error.Visibility = _invalidDelays.ContainsKey(program.Id) ? Visibility.Visible : Visibility.Collapsed;
        delay.TextChanged += (_, _) =>
        {
            if (int.TryParse(delay.Text, App.Localization.Culture, out var seconds) && seconds is >= 0 and <= PresetDraft.MaxDelay)
            {
                program.DelaySeconds = seconds;
                _invalidDelays.Remove(program.Id);
                error.Visibility = Visibility.Collapsed;
                if (_invalidDelays.Count == 0)
                {
                    ClearStatus();
                }
            }
            else
            {
                _invalidDelays[program.Id] = delay.Text;
                error.Visibility = Visibility.Visible;
            }
        };
        var fields = new StackPanel { Spacing = 6 };
        fields.Children.Add(admin);
        fields.Children.Add(delay);
        fields.Children.Add(error);
        var flyout = new Flyout { Content = fields };
        var options = new Button { Content = Icon("\uE713", accent: true), Flyout = flyout };
        Label(options, T("editor.optionsNamed", program.Name));
        fields.Children.Insert(0, Heading("editor.options"));
        return options;
    }
}
