using System;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using WinRT;
using WinRT.Interop;
using Windows.UI;

namespace PresetLauncher.Services;

public class ThemeService
{
    public event EventHandler<string>? ThemeChanged;
    
    private Window? _mainWindow;

    private WindowsSystemDispatcherQueueHelper? _dqHelper;
    private MicaController? _micaController;
    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _backdropConfiguration;
    private string _currentBackdrop = "none";

    public void SetWindow(Window window)
    {
        // store window and hook events when needed
        _mainWindow = window;
    }

    public void ApplyAccent(string colorName, bool useAccent, bool useTransparency)
    {
        if (_mainWindow == null) return;

        var rootElement = _mainWindow.Content as FrameworkElement;
        var rootPanel = rootElement as Microsoft.UI.Xaml.Controls.Panel;
        var rootControl = rootElement as Microsoft.UI.Xaml.Controls.Control;

        if (!useAccent)
        {
            // clear accent
            if (rootPanel != null) rootPanel.ClearValue(Microsoft.UI.Xaml.Controls.Panel.BackgroundProperty);
            if (rootControl != null) rootControl.ClearValue(Microsoft.UI.Xaml.Controls.Control.BackgroundProperty);
            return;
        }

        byte r = 0x00, g = 0x78, b = 0xD7; // default blue
        switch (colorName?.ToLowerInvariant())
        {
            case "red": r = 0xD8; g = 0x33; b = 0x3A; break; // red
            case "green": r = 0x10; g = 0xA1; b = 0x10; break; // green
            case "purple": r = 0x6A; g = 0x00; b = 0xB4; break; // purple
            default: r = 0x00; g = 0x78; b = 0xD7; break; // blue
        }

        var alpha = useTransparency ? (byte)0xCC : (byte)0xFF;
        var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(alpha, r, g, b));
        if (rootPanel != null) rootPanel.Background = brush;
        else if (rootControl != null) rootControl.Background = brush;
    }

    public void ApplyTheme(string theme)
    {
        if (_mainWindow == null) return;
        
        var actualTheme = theme;
        
        if (theme == "system")
        {
            actualTheme = IsSystemDarkTheme() ? "dark" : "light";
        }

        var rootElement = _mainWindow.Content as FrameworkElement;
        var rootPanel = rootElement as Microsoft.UI.Xaml.Controls.Panel;
        var rootControl = rootElement as Microsoft.UI.Xaml.Controls.Control;
        if (rootElement != null)
        {
            rootElement.RequestedTheme = actualTheme == "dark" 
                ? ElementTheme.Dark 
                : ElementTheme.Light;
        }

        ThemeChanged?.Invoke(this, actualTheme);
    }

    public ElementTheme GetElementTheme(string theme)
    {
        if (theme == "system")
        {
            return IsSystemDarkTheme() ? ElementTheme.Dark : ElementTheme.Light;
        }
        return theme == "dark" ? ElementTheme.Dark : ElementTheme.Light;
    }

    public void ApplyBackdrop(string backdrop)
    {
        if (_mainWindow == null) return;

        // Ensure dispatcher queue is available
        _dqHelper ??= new WindowsSystemDispatcherQueueHelper();
        _dqHelper.EnsureWindowsSystemDispatcherQueueController();

        // Dispose existing controllers and remove handlers
        TryRemoveWindowHandlers();
        _micaController?.Dispose();
        _micaController = null;
        _acrylicController?.Dispose();
        _acrylicController = null;
        _backdropConfiguration = null;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);

        var rootElement = _mainWindow.Content as FrameworkElement;
        var rootPanel = rootElement as Microsoft.UI.Xaml.Controls.Panel;
        var rootControl = rootElement as Microsoft.UI.Xaml.Controls.Control;

        // Always apply a visible brush immediately so user sees change even if system controllers fail
        if (backdrop == "mica")
        {
            // apply mica-like translucent brush
            try
            {
                var micaBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(IsSystemDarkTheme() ? Windows.UI.Color.FromArgb(0xCC, 0x12, 0x12, 0x12) : Windows.UI.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
                if (rootPanel != null)
                    rootPanel.Background = micaBrush;
                else if (rootControl != null)
                    rootControl.Background = micaBrush;
            }
            catch { }
            try
            {
                _backdropConfiguration = new SystemBackdropConfiguration();
                _backdropConfiguration.IsInputActive = true;
                _backdropConfiguration.Theme = IsSystemDarkTheme() ? SystemBackdropTheme.Dark : SystemBackdropTheme.Default;

                _micaController = new MicaController();
                try
                {
                    var target = _mainWindow.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>();
                    _micaController.AddSystemBackdropTarget(target);
                    _micaController.SetSystemBackdropConfiguration(_backdropConfiguration);
                    _mainWindow.Activated += Window_Activated;
                    _mainWindow.Closed += Window_Closed;
                    System.Diagnostics.Debug.WriteLine("Mica applied");
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("Failed to add mica target");
                }
            }
            catch
            {
            }
            // fallback: if controller not available, apply a simple translucent background
            if (_micaController == null && rootElement != null)
            {
                try
                {
                    var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(IsSystemDarkTheme() ? Windows.UI.Color.FromArgb(0xCC, 0x11, 0x11, 0x11) : Windows.UI.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
                    if (rootPanel != null)
                        rootPanel.Background = brush;
                    else if (rootControl != null)
                        rootControl.Background = brush;
                    System.Diagnostics.Debug.WriteLine("Applied mica fallback brush");
                }
                catch
                {
                }
            }
        }
        else if (backdrop == "acrylic")
        {
            // apply translucent tint to simulate acrylic immediately
            try
            {
                var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(IsSystemDarkTheme() ? Windows.UI.Color.FromArgb(0x99, 0x00, 0x00, 0x00) : Windows.UI.Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
                if (rootPanel != null)
                    rootPanel.Background = brush;
                else if (rootControl != null)
                    rootControl.Background = brush;
            }
            catch { }
            try
            {
                _backdropConfiguration = new SystemBackdropConfiguration();
                _backdropConfiguration.IsInputActive = true;
                _backdropConfiguration.Theme = IsSystemDarkTheme() ? SystemBackdropTheme.Dark : SystemBackdropTheme.Default;

                _acrylicController = new DesktopAcrylicController();
                try
                {
                    var target = _mainWindow.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>();
                    _acrylicController.AddSystemBackdropTarget(target);
                    _acrylicController.SetSystemBackdropConfiguration(_backdropConfiguration);
                    _mainWindow.Activated += Window_Activated;
                    _mainWindow.Closed += Window_Closed;
                    System.Diagnostics.Debug.WriteLine("Acrylic applied");
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("Failed to add acrylic target");
                }
            }
            catch
            {
            }
            // fallback: if acrylic controller not available, apply a translucent tint similar to acrylic
            if (_acrylicController == null && rootElement != null)
            {
                try
                {
                    var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(IsSystemDarkTheme() ? Windows.UI.Color.FromArgb(0x99, 0x00, 0x00, 0x00) : Windows.UI.Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
                    if (rootPanel != null)
                        rootPanel.Background = brush;
                    else if (rootControl != null)
                        rootControl.Background = brush;
                    System.Diagnostics.Debug.WriteLine("Applied acrylic fallback brush");
                }
                catch
                {
                }
            }
        }
        else
        {
            // none: nothing to do, default background
            System.Diagnostics.Debug.WriteLine("Backdrop set to none");
            if (rootElement != null)
            {
                try
                {
                    if (rootPanel != null)
                        rootPanel.ClearValue(Microsoft.UI.Xaml.Controls.Panel.BackgroundProperty);
                    else if (rootControl != null)
                        rootControl.ClearValue(Microsoft.UI.Xaml.Controls.Control.BackgroundProperty);
                }
                catch
                {
                }
            }
        }
    }

    private void Window_Activated(object? sender, WindowActivatedEventArgs e)
    {
        if (_backdropConfiguration == null) return;
        _backdropConfiguration.IsInputActive = e.WindowActivationState != WindowActivationState.Deactivated;
    }

    private void Window_Closed(object? sender, WindowEventArgs e)
    {
        TryRemoveWindowHandlers();
        _micaController?.Dispose();
        _acrylicController?.Dispose();
        _backdropConfiguration = null;
    }

    private void TryRemoveWindowHandlers()
    {
        if (_mainWindow == null) return;
        try
        {
            _mainWindow.Activated -= Window_Activated;
            _mainWindow.Closed -= Window_Closed;
        }
        catch
        {
        }
    }

    private bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            
            if (key != null)
            {
                var value = key.GetValue("AppsUseLightTheme");
                if (value is int intValue)
                {
                    return intValue == 0;
                }
            }
        }
        catch
        {
        }
        
        return false;
    }
}
