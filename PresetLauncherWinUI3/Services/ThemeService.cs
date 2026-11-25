using System.Diagnostics;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Models;
using Windows.UI.ViewManagement;
using WinRT;

namespace PresetLauncher.Services;

internal sealed class ThemeService
{
    private Window _window = null!;
    private FrameworkElement _root = null!;
    private Border _fallback = null!;
    private readonly AccessibilitySettings _accessibility = new();
    private readonly UISettings _system = new();
    private readonly WindowsSystemDispatcherQueueHelper _queue = new();
    private ISystemBackdropControllerWithTargets? _controller;
    private SystemBackdropConfiguration? _configuration;
    private string? _material;
    private string _theme = "system";
    private string _backdrop = "none";
    private bool _active;
    private bool _closed;

    public void Attach(Window window, FrameworkElement root, Border fallback)
    {
        _window = window;
        _root = root;
        _fallback = fallback;
        _root.ActualThemeChanged += ActualThemeChanged;
        _window.Activated += Activated;
        _window.Closed += Closed;
        _system.ColorValuesChanged += SystemColorsChanged;
    }

    public void Apply(AppSettings settings)
    {
        _theme = settings.Theme;
        _backdrop = settings.Backdrop;
        UpdateAppearance();
    }

    private void UpdateAppearance()
    {
        if (_closed) return;
        var highContrast = _accessibility.HighContrast;
        _root.RequestedTheme = highContrast ? ElementTheme.Default : _theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => _system.GetColorValue(UIColorType.Background).R < 128 ? ElementTheme.Dark : ElementTheme.Light
        };
        SetMaterial(highContrast ? "none" : _backdrop);
        SyncTheme();
    }

    private void SetMaterial(string material)
    {
        if (material == _material)
            return;
        _material = material;
        _controller?.Dispose();
        _controller = null;
        _configuration = null;
        _fallback.Visibility = Visibility.Visible;

        try
        {
            _controller = material switch
            {
                "mica" when MicaController.IsSupported() => new MicaController(),
                "acrylic" when DesktopAcrylicController.IsSupported() => new DesktopAcrylicController(),
                _ => null
            };
            if (_controller is null)
                return;
            _queue.EnsureWindowsSystemDispatcherQueueController();
            _configuration = new SystemBackdropConfiguration { IsInputActive = _active, Theme = BackdropTheme };
            if (!_controller.AddSystemBackdropTarget(_window.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>()))
                throw new InvalidOperationException("Backdrop target could not be attached.");
            _controller.SetSystemBackdropConfiguration(_configuration);
            _fallback.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            Debug.WriteLine(ex.GetType().Name);
            _controller?.Dispose();
            _controller = null;
            _configuration = null;
        }
    }

    private SystemBackdropTheme BackdropTheme => _root.ActualTheme == ElementTheme.Dark ?
        SystemBackdropTheme.Dark : SystemBackdropTheme.Light;

    private void SyncTheme()
    {
        if (_configuration is not null)
            _configuration.Theme = BackdropTheme;
        if (AppWindowTitleBar.IsCustomizationSupported())
            _window.AppWindow.TitleBar.PreferredTheme = _accessibility.HighContrast ? TitleBarTheme.UseDefaultAppMode :
                _root.ActualTheme == ElementTheme.Dark ? TitleBarTheme.Dark : TitleBarTheme.Light;
    }

    private void ActualThemeChanged(FrameworkElement sender, object args) => SyncTheme();

    private void Activated(object sender, WindowActivatedEventArgs args)
    {
        _active = args.WindowActivationState != WindowActivationState.Deactivated;
        if (_configuration is not null)
            _configuration.IsInputActive = _active;
    }

    private void SystemColorsChanged(UISettings sender, object args) => QueueRefresh();
    private void QueueRefresh() => _root.DispatcherQueue.TryEnqueue(UpdateAppearance);

    private void Closed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _system.ColorValuesChanged -= SystemColorsChanged;
        _root.ActualThemeChanged -= ActualThemeChanged;
        _window.Activated -= Activated;
        _window.Closed -= Closed;
        _controller?.Dispose();
    }
}
