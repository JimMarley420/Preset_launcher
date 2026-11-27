using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using PresetLauncher.Services;
using Windows.System;
using static PresetLauncher.Views.DesktopUi;

namespace PresetLauncher.Views.Pages;

internal enum UpdateAction { Install, Postpone, Skip, Exit, Retry }

internal sealed class UpdatePage : Grid
{
    private readonly Grid _layout = new() { RowSpacing = 12, ColumnSpacing = 18 };
    private readonly Grid _left = new() { RowSpacing = 7 };
    private readonly Grid _right = new() { RowSpacing = 12 };
    private readonly Grid _rightContent = new() { RowSpacing = 12 };
    private readonly ScrollViewer _rightScroll;
    private readonly ScrollViewer _narrowScroll;
    private readonly StackPanel _info = new() { Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _actions = new() { Spacing = 7 };
    private readonly StackPanel _links = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _notes = new() { Spacing = 8 };
    private readonly Border _separator = new() { Width = 1 };
    private readonly TextBlock _subtitle = Text("", "DesktopSecondaryText");
    private readonly TextBlock _title = Heading("settings.updateAvailable", page: true);
    private readonly TextBlock _status = Text("");
    private readonly TextBlock _transfer = Text("", "DesktopSecondaryText");
    private readonly TextBlock _linkError = Text(T("update.linkFailed"), "DesktopWarningText");
    private readonly ProgressBar _progress = new() { Maximum = 100 };
    private readonly Button _primary;
    private readonly Button _secondary;
    private readonly HyperlinkButton _skip;
    private Action? _primaryAction;
    private Action? _secondaryAction;
    private Action? _skipAction;
    private bool _narrow;
    private bool? _short;

    internal UpdatePage()
    {
        var rule = (Style)Application.Current.Resources["DesktopDivider"];
        _layout.Margin = new Thickness(22, 20, 22, 20);
        _layout.ColumnDefinitions.Add(new() { Width = new GridLength(3, GridUnitType.Star) });
        _layout.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _layout.ColumnDefinitions.Add(new() { Width = new GridLength(2, GridUnitType.Star) });
        _layout.RowDefinitions.Add(new());
        _layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _left.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _left.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _left.RowDefinitions.Add(new());
        _left.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Place(_left, Heading("update.whatsNew", page: true), 0, 0);
        Place(_left, _subtitle, 1, 0);
        var notesScroll = Scroll(_notes);
        notesScroll.IsTabStop = true;
        Label(notesScroll, T("update.whatsNew"));
        Place(_left, new Border { Style = rule, BorderThickness = new Thickness(1), Padding = new Thickness(14), Child = notesScroll }, 2, 0);
        var footer = new StackPanel();
        footer.Children.Add(_links);
        footer.Children.Add(_linkError);
        _linkError.Visibility = Visibility.Collapsed;
        AutomationProperties.SetLiveSetting(_linkError, AutomationLiveSetting.Polite);
        Place(_left, footer, 3, 0);
        _left.SizeChanged += (_, args) => _links.Orientation = args.NewSize.Width < 265 ? Orientation.Vertical : Orientation.Horizontal;
        _separator.Style = rule;
        _separator.Padding = new Thickness(0);
        _separator.BorderThickness = new Thickness(1, 0, 0, 0);

        _right.RowDefinitions.Add(new());
        _right.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _rightContent.RowDefinitions.Add(new());
        _rightContent.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Place(_rightContent, _info, 0, 0);
        _rightScroll = Scroll(_rightContent);
        _rightScroll.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _rightScroll.SizeChanged += (_, args) => _rightContent.MinHeight = _short == false ? args.NewSize.Height : 0;
        Place(_right, _rightScroll, 0, 0);
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_progress, T("update.progressLabel"));

        _primary = Button("update.install", (_, _) => _primaryAction?.Invoke(), accent: true);
        _secondary = Button("update.later", (_, _) => _secondaryAction?.Invoke());
        _skip = new HyperlinkButton { Content = T("update.skip"), HorizontalAlignment = HorizontalAlignment.Left };
        _skip.Click += (_, _) => _skipAction?.Invoke();
        foreach (var button in new[] { _primary, _secondary })
        {
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.MinHeight = 34;
            var content = Text((string)button.Content);
            content.TextAlignment = TextAlignment.Center;
            button.Content = content;
            _actions.Children.Add(button);
        }
        _actions.Children.Add(_skip);

        Place(_layout, _left, 0, 0);
        Place(_layout, _separator, 0, 1);
        Place(_layout, _right, 0, 2);
        _narrowScroll = Scroll(_layout);
        _narrowScroll.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _narrowScroll.Content = null;
        Children.Add(_layout);
        Loaded += (_, _) => { Reflow(ActualWidth, ActualHeight); FocusAction(); };
        SizeChanged += (_, args) => Reflow(args.NewSize.Width, args.NewSize.Height);
    }

    internal void ShowOffer(string installed, ReleaseUpdate? offer, bool required)
    {
        _title.Text = T(required ? "update.requiredTitle" :
            offer?.Policy == UpdatePolicy.Recommended ? "update.recommendedTitle" : "settings.updateAvailable");
        _info.Children.Clear();
        var icon = Icon("\uE895", accent: true);
        icon.HorizontalAlignment = HorizontalAlignment.Left;
        _info.Children.Add(icon);
        _info.Children.Add(Text(T("update.eyebrow"), "DesktopSecondaryText"));
        _info.Children.Add(_title);
        _info.Children.Add(VersionGroup("update.installedVersion", installed));
        if (offer is not null)
        {
            _info.Children.Add(Text("↓", "DesktopSecondaryText"));
            _info.Children.Add(VersionGroup("update.availableVersion", offer.Version.ToString(3)));
            _info.Children.Add(Text(T(required ? "update.requiredDescription" :
                offer.Policy == UpdatePolicy.Recommended ? "update.recommendedDescription" : "update.optionalDescription")));
            var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            _info.Children.Add(Text(T("update.installerInfo", FormatSize(offer.Size), arch), "DesktopSecondaryText"));
        }
        _info.Children.Add(_status);
        _info.Children.Add(_transfer);
        _info.Children.Add(_progress);

        _subtitle.Text = offer is null ? "" : T("update.notesVersion", offer.Version.ToString(3));
        _notes.Children.Clear();
        if (offer?.Notes is { } markdown)
        {
            foreach (var block in UpdateReleaseNotes.Format(markdown)) AddNote(block);
        }
        if (_notes.Children.Count == 0)
            _notes.Children.Add(Text(T(offer is null ? "update.notesPending" : "update.notesFallback"), "DesktopSecondaryText"));
        _linkError.Visibility = Visibility.Collapsed;
        _links.Children.Clear();
        if (offer?.ReleaseUri is { } uri) AddLink("update.githubRelease", uri);
        AddLink("update.versionHistory", UpdateService.VersionHistoryUri(App.Localization.Language));
    }

    internal Task<UpdateAction> PromptAsync(string? status, (string Key, UpdateAction Action)? primary,
        (string Key, UpdateAction Action)? secondary, bool skip = false)
    {
        SetStatus(status);
        var response = new TaskCompletionSource<UpdateAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action Answer(UpdateAction action) => () =>
        {
            if (response.TrySetResult(action))
                SetActions(null, null);
        };
        SetActions(primary is { } p ? (p.Key, Answer(p.Action)) : null,
            secondary is { } s ? (s.Key, Answer(s.Action)) : null, skip ? Answer(UpdateAction.Skip) : null);
        FocusAction();
        return response.Task;
    }

    internal void ShowProgress(string key, Action? cancel = null, UpdateDownloadProgress? progress = null)
    {
        var status = T(key);
        if (_status.Text != status || _progress.Visibility != Visibility.Visible)
            SetStatus(status, busy: true);
        if (progress is { } transfer)
        {
            _progress.IsIndeterminate = false;
            _progress.Value = 100d * transfer.Transferred / transfer.Total;
            _transfer.Text = T("update.transfer", _progress.Value / 100d, FormatSize(transfer.Transferred), FormatSize(transfer.Total));
            _transfer.Visibility = Visibility.Visible;
        }
        if (cancel is null)
            SetActions(null, null);
        else if (_secondaryAction is null)
        {
            SetActions(null, ("dialog.cancel", cancel));
            FocusAction();
        }
    }

    private void SetStatus(string? text, bool busy = false)
    {
        var value = text ?? "";
        var changed = _status.Text != value;
        _status.Text = value;
        _status.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
        _transfer.Visibility = Visibility.Collapsed;
        _progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        _progress.IsIndeterminate = busy;
        if (changed && text is not null)
            FrameworkElementAutomationPeer.FromElement(_status)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void SetActions((string Key, Action Run)? primary, (string Key, Action Run)? secondary, Action? skip = null)
    {
        SetButton(_primary, primary);
        SetButton(_secondary, secondary);
        _skip.Visibility = skip is null ? Visibility.Collapsed : Visibility.Visible;
        _primaryAction = primary?.Run;
        _secondaryAction = secondary?.Run;
        _skipAction = skip;
    }

    private static void SetButton(Button button, (string Key, Action Run)? action)
    {
        button.Visibility = action is null ? Visibility.Collapsed : Visibility.Visible;
        if (action is { } available)
        {
            ((TextBlock)button.Content).Text = T(available.Key);
            AutomationProperties.SetName(button, T(available.Key));
        }
    }

    private void FocusAction()
    {
        if (IsLoaded)
            (_primary.Visibility == Visibility.Visible ? _primary : _secondary).Focus(FocusState.Programmatic);
    }

    private void Reflow(double width, double height)
    {
        if (width <= 0 || height <= 0) return;
        var narrow = width < 620;
        var shortWindow = height < 410 || narrow;
        if (_narrow != narrow)
        {
            _narrow = narrow;
            if (narrow)
            {
                Children.Remove(_layout);
                _narrowScroll.Content = _layout;
                Children.Add(_narrowScroll);
            }
            else
            {
                Children.Remove(_narrowScroll);
                _narrowScroll.Content = null;
                Children.Add(_layout);
            }
            _left.Height = narrow ? 310 : double.NaN;
            _layout.RowDefinitions[0].Height = narrow ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
            _layout.ColumnDefinitions[0].Width = new GridLength(narrow ? 1 : 3, GridUnitType.Star);
            _layout.ColumnDefinitions[2].Width = narrow ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
            _layout.ColumnSpacing = narrow ? 0 : 18;
            _separator.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
            Grid.SetRow(_right, narrow ? 1 : 0);
            Grid.SetColumn(_right, narrow ? 0 : 2);
        }
        if (_short != shortWindow)
        {
            _short = shortWindow;
            _rightContent.MinHeight = shortWindow ? 0 : _rightScroll.ActualHeight;
            _right.Children.Remove(_actions);
            _rightContent.Children.Remove(_actions);
            Place(shortWindow ? _rightContent : _right, _actions, 1, 0);
        }
    }

    private void AddNote(ReleaseNoteBlock note)
    {
        var block = Text("", note.Kind == ReleaseNoteKind.Heading ? "DesktopSectionTitle" : null);
        if (note.Kind == ReleaseNoteKind.Heading)
            AutomationProperties.SetHeadingLevel(block, AutomationHeadingLevel.Level3);
        foreach (var span in note.Spans)
        {
            var run = new Run { Text = span.Text };
            if (span.Style == ReleaseNoteStyle.Bold) run.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            if (span.Style == ReleaseNoteStyle.Code) run.FontFamily = new FontFamily("Consolas");
            block.Inlines.Add(run);
        }
        _notes.Children.Add(block);
    }

    private void AddLink(string key, Uri target)
    {
        var link = new HyperlinkButton { Content = T(key) };
        link.Click += async (_, _) =>
        {
            var opened = false;
            try { opened = await Launcher.LaunchUriAsync(target); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            _linkError.Visibility = opened ? Visibility.Collapsed : Visibility.Visible;
        };
        _links.Children.Add(link);
    }

    private static StackPanel VersionGroup(string key, string value)
    {
        var pair = new StackPanel();
        pair.Children.Add(Text(T(key), "DesktopSecondaryText"));
        pair.Children.Add(Text(value, "DesktopSectionTitle"));
        return pair;
    }

    private static string FormatSize(long bytes) => bytes < 1024 * 1024
        ? T("update.sizeKiB", bytes / 1024d)
        : T("update.sizeMiB", bytes / (1024d * 1024));
}
