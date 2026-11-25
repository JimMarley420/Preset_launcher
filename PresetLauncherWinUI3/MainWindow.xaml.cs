using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Models;
using PresetLauncher.Services;
using PresetLauncher.Views;
using PresetLauncher.Views.Pages;

namespace PresetLauncher;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<NavigationViewItemBase, DesktopPage> _pages = [];
    private NavigationViewItemBase? _destination;
    private bool _navigating;
    private bool _updatingPaneMode;
    private bool _busy = true;
    private bool _updateBlocked;
    private bool _closing;
    private bool _closed;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        NativeWindow.ResizeClient(this, 900, 640);
        App.Theme.Attach(this, Root, OpaqueBackground);
        ApplyPreferences();
        AppWindow.Closing += HandleClosing;
        var paneCallback = NavView.RegisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty,
            (_, _) => UpdatePaneMode(NavView.ActualWidth));
        Closed += (_, _) =>
        {
            _closed = true;
            NavView.UnregisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, paneCallback);
        };
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.Loaded -= NavView_Loaded;
        NavView.IsPaneOpen = false;
        _destination = NavPresets;
        NavView.SelectedItem = NavPresets;
        RefreshNavigation();
        ShowDestination(NavPresets);
        UpdateInput();
    }

    private void NavView_SizeChanged(object sender, SizeChangedEventArgs args) => UpdatePaneMode(args.NewSize.Width);

    private void UpdatePaneMode(double width)
    {
        if (_updatingPaneMode || width <= 0)
            return;

        var open = NavView.IsPaneOpen;
        // An inline pane shares the window material. At very narrow widths the native overlay keeps the page usable.
        var mode = width < 560 ? NavigationViewPaneDisplayMode.LeftMinimal :
            open ? NavigationViewPaneDisplayMode.Left : NavigationViewPaneDisplayMode.LeftCompact;
        if (mode == NavView.PaneDisplayMode)
            return;

        _updatingPaneMode = true;
        try
        {
            NavView.PaneDisplayMode = mode;
            NavView.IsPaneOpen = open;
        }
        finally { _updatingPaneMode = false; }
    }

    private async void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var requested = args.InvokedItemContainer;
        if (_navigating || _destination is null ||
            Equals(requested, _destination) && PageHost.Content is not PresetEditorPage)
            return;

        _navigating = true;
        try
        {
            NavView.SelectedItem = _destination;
            if (_busy || _updateBlocked || requested is null)
                return;
            if (PageHost.Content is PresetEditorPage editor)
            {
                var leave = false;
                await RunOperationAsync(async () => leave = await editor.CanLeaveAsync(), "dialog.error");
                if (!leave) return;
            }
            _destination = requested;
            NavView.SelectedItem = requested;
            ShowDestination(requested);
        }
        finally { _navigating = false; }
    }

    private void ShowDestination(NavigationViewItemBase destination)
    {
        if (!_pages.TryGetValue(destination, out var page))
        {
            page = destination == NavPresets ? new PresetsPage() :
                destination == NavHistory ? new HistoryPage() :
                destination == NavStats ? new StatisticsPage() : new SettingsPage();
            _pages.Add(destination, page);
        }
        PageHost.Content = page;
        page.Refresh();
    }

    internal void EditPreset(Preset? preset) => PageHost.Content = new PresetEditorPage(preset);

    internal void ReturnToPresets()
    {
        _navigating = true;
        _destination = NavPresets;
        NavView.SelectedItem = NavPresets;
        _navigating = false;
        ShowDestination(NavPresets);
    }

    internal void ApplyPreferences()
    {
        var settings = App.Settings.CurrentSettings;
        App.Localization.SetLanguage(settings.Language);
        Root.Language = App.Localization.Culture.Name;
        App.Theme.Apply(settings);
        RefreshNavigation();
        if (PageHost.Content is DesktopPage page)
            page.Refresh();
    }

    private void RefreshNavigation()
    {
        Title = AppTitleBar.Title = DesktopUi.T("app.title");
        SetNav(NavPresets, "nav.presets");
        SetNav(NavHistory, "nav.history");
        SetNav(NavStats, "nav.statistics");
        if (NavView.SettingsItem is NavigationViewItem settings)
            SetNav(settings, "nav.settings");
    }

    private static void SetNav(NavigationViewItem item, string key)
    {
        var label = DesktopUi.T(key);
        item.Content = label;
        DesktopUi.Label(item, label);
    }

    internal void SetUpdateGate(bool blocked)
    {
        _updateBlocked = blocked;
        UpdateInput();
    }

    internal void FinishStartup()
    {
        _busy = false;
        UpdateInput();
    }

    internal async Task<bool> RunOperationAsync(Func<Task> operation, string errorKey, bool pageEnabled = false)
    {
        if (_busy || _updateBlocked)
            return false;
        _busy = true;
        UpdateInput(pageEnabled);
        try
        {
            await operation();
            return true;
        }
        catch (Exception ex) when (DataService.IsFileError(ex) || ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            Debug.WriteLine(ex.GetType().Name);
            if (PageHost.Content is DesktopPage page)
                page.Report(errorKey, InfoBarSeverity.Error);
            return false;
        }
        finally
        {
            _busy = false;
            UpdateInput();
        }
    }

    private void UpdateInput(bool pageEnabled = false)
    {
        if (_closed) return;
        NavView.IsEnabled = !_updateBlocked;
        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
            item.IsEnabled = !_busy;
        if (NavView.SettingsItem is NavigationViewItem settings)
            settings.IsEnabled = !_busy;
        PageHost.IsEnabled = !_updateBlocked && (!_busy || pageEnabled);
    }

    private async void HandleClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_closing || _updateBlocked)
            return;
        if (_busy)
        {
            args.Cancel = true;
            return;
        }
        if (PageHost.Content is not PresetEditorPage { IsDirty: true } editor)
            return;

        args.Cancel = true;
        var leave = false;
        await RunOperationAsync(async () => leave = await editor.CanLeaveAsync(), "dialog.error");
        if (leave)
        {
            _closing = true;
            Close();
        }
    }
}
