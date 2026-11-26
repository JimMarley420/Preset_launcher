using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;

namespace PresetLauncher.Views;

public abstract class DesktopPage : Page
{
    protected StackPanel Header { get; } = new() { Spacing = 8 };
    protected StackPanel Body { get; } = new() { Spacing = 8 };
    protected ScrollViewer Scroller { get; }
    protected ContentControl Footer { get; } = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly InfoBar _status = new();
    private string? _statusKey;
    private object[] _statusArgs = [];
    protected MainWindow Shell => App.Current.MainWindow;

    protected DesktopPage(FrameworkElement? scrollingContent = null)
    {
        var layout = new Grid { Margin = new Thickness(20, 12, 20, 12), MaxWidth = 1060, RowSpacing = 8 };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Scroller = DesktopUi.Scroll(Body);
        layout.Children.Add(Header);
        DesktopUi.Place(layout, _status, 1, 0);
        DesktopUi.Place(layout, scrollingContent ?? Scroller, 2, 0);
        DesktopUi.Place(layout, Footer, 3, 0);
        Content = layout;
    }

    internal virtual void Refresh()
    {
        if (_statusKey is not null)
            _status.Message = DesktopUi.T(_statusKey, _statusArgs);
    }

    internal void Report(string key, InfoBarSeverity severity = InfoBarSeverity.Informational, params object[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        _status.Severity = severity;
        _status.Message = DesktopUi.T(key, args);
        _status.IsOpen = true;
    }

    protected void ClearStatus() => _status.IsOpen = false;
}

internal static class DesktopUi
{
    public static string T(string key, params object[] args) => App.Localization.Get(key, args);

    public static TextBlock Text(string text, string? style = null)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        if (style is not null)
            block.Style = (Style)Application.Current.Resources[style];
        return block;
    }

    public static TextBlock Heading(string key, bool page = false)
    {
        var block = Text(T(key), page ? "DesktopPageTitle" : "DesktopSectionTitle");
        AutomationProperties.SetHeadingLevel(block, page ? AutomationHeadingLevel.Level1 : AutomationHeadingLevel.Level2);
        return block;
    }

    public static TextBlock PathText(string path)
    {
        var block = Text(path, "DesktopSecondaryText");
        block.TextWrapping = TextWrapping.NoWrap;
        block.TextTrimming = TextTrimming.CharacterEllipsis;
        block.IsTextSelectionEnabled = true;
        ToolTipService.SetToolTip(block, path);
        return block;
    }

    public static Button Button(string key, RoutedEventHandler action, bool accent = false, string? glyph = null)
    {
        var button = new Button { Content = glyph is null ? T(key) : Icon(glyph) };
        if (glyph is not null)
            Label(button, T(key));
        if (accent)
            button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        button.Click += action;
        return button;
    }

    public static FontIcon Icon(string glyph, bool accent = false)
    {
        var icon = new FontIcon { Glyph = glyph, FontSize = 16 };
        if (accent)
            icon.Style = (Style)Application.Current.Resources["DesktopAccentIcon"];
        return icon;
    }

    public static void Label(FrameworkElement element, string label)
    {
        AutomationProperties.SetName(element, label);
        ToolTipService.SetToolTip(element, label);
    }

    public static Border Row(UIElement content) => new()
    {
        Child = content,
        Style = (Style)Application.Current.Resources["DesktopDivider"]
    };

    public static StackPanel Actions(params UIElement[] controls)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var control in controls)
            panel.Children.Add(control);
        return panel;
    }

    public static Grid AdaptivePair(FrameworkElement leading, FrameworkElement trailing, double breakpoint)
    {
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Place(grid, leading, 0, 0);
        Place(grid, trailing, 1, 0);
        trailing.VerticalAlignment = VerticalAlignment.Center;
        grid.SizeChanged += (_, args) =>
        {
            var stack = args.NewSize.Width < breakpoint;
            Grid.SetRow(trailing, stack ? 1 : 0);
            Grid.SetColumn(trailing, stack ? 0 : 1);
        };
        return grid;
    }

    public static ScrollViewer Scroll(UIElement content) => new()
    {
        Content = content,
        HorizontalScrollMode = ScrollMode.Disabled,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    public static void Place(Grid grid, FrameworkElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    public static ContentDialog Dialog(XamlRoot root, string title, object content, string? action = null) => new()
    {
        XamlRoot = root,
        Title = T(title),
        Content = content is string text ? Text(text) : content,
        CloseButtonText = T(action is null ? "dialog.ok" : "dialog.cancel"),
        PrimaryButtonText = action is null ? "" : T(action),
        DefaultButton = ContentDialogButton.Close
    };

    public static async Task<bool> Confirm(XamlRoot root, string title, object content, string action) =>
        await Dialog(root, title, content, action).ShowAsync() == ContentDialogResult.Primary;

    public static FileOpenPicker OpenPicker(string title, params string[] extensions)
    {
        var picker = new FileOpenPicker(App.Current.MainWindow.AppWindow.Id)
        {
            Title = T(title),
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        foreach (var extension in extensions)
            picker.FileTypeFilter.Add(extension);
        return picker;
    }

    public static FileSavePicker BackupPicker()
    {
        var picker = new FileSavePicker(App.Current.MainWindow.AppWindow.Id)
        {
            Title = T("data.export"),
            CommitButtonText = T("data.export"),
            DefaultFileExtension = ".json",
            ShowOverwritePrompt = true,
            SuggestedFileName = $"PresetLauncher-{DateTime.Now:yyyy-MM-dd}",
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeChoices.Add(T("data.fileType"), [".json"]);
        return picker;
    }
}
