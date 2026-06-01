using System;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using WinRT;
using WinRT.Interop;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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

    public void SetWindow(Window window)
    {
        _mainWindow = window;
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

        var rootElement = _mainWindow.Content as FrameworkElement;

        // Helper to set background safely on various element types
        void SetElementBackground(FrameworkElement? el, Brush? brush)
        {
            if (el == null || brush == null) return;
            try
            {
                if (el is Control ctrl)
                {
                    ctrl.Background = brush;
                }
                else if (el is Panel panel)
                {
                    panel.Background = brush;
                }
                else if (el is Border border)
                {
                    border.Background = brush;
                }
            }
            catch { }
        }

        // Restore or set transparent backgrounds depending on backdrop selection
        Brush? normalBackground = null;
        try
        {
            normalBackground = Application.Current?.Resources["ApplicationPageBackgroundThemeBrush"] as Brush;
        }
        catch { }

        var transparentBrush = new SolidColorBrush(Colors.Transparent);

        // If selecting none, restore normal backgrounds; otherwise make backgrounds transparent so system backdrop is visible
        var useTransparent = backdrop == "mica" || backdrop == "acrylic";
        if (rootElement != null)
        {
            SetElementBackground(rootElement, useTransparent ? transparentBrush : normalBackground ?? transparentBrush);

            // Try to find common title bar element by name and update its background as well
            try
            {
                var titleBarEl = rootElement.FindName("AppTitleBar") as FrameworkElement;
                SetElementBackground(titleBarEl, useTransparent ? transparentBrush : normalBackground ?? transparentBrush);
            }
            catch { }

                // Also try to apply transparent/normal backgrounds to common navigation and content elements
                try
                {
                    var navViewEl = rootElement.FindName("NavView") as FrameworkElement;
                    SetElementBackground(navViewEl, useTransparent ? transparentBrush : normalBackground ?? transparentBrush);
                }
                catch { }

                try
                {
                    var contentFrameEl = rootElement.FindName("ContentFrame") as FrameworkElement;
                    SetElementBackground(contentFrameEl, useTransparent ? transparentBrush : normalBackground ?? transparentBrush);
                }
                catch { }

                try
                {
                    var rootGridEl = rootElement.FindName("RootGrid") as FrameworkElement;
                    SetElementBackground(rootGridEl, useTransparent ? transparentBrush : normalBackground ?? transparentBrush);
                }
                catch { }
        }

        if (backdrop == "mica")
        {
            try
            {
                _backdropConfiguration = new SystemBackdropConfiguration();
                _backdropConfiguration.IsInputActive = true;
                _backdropConfiguration.Theme = IsSystemDarkTheme() ? SystemBackdropTheme.Dark : SystemBackdropTheme.Default;

                _micaController = new MicaController();
                var target = _mainWindow.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>();
                _micaController.AddSystemBackdropTarget(target);
                _micaController.SetSystemBackdropConfiguration(_backdropConfiguration);
                _mainWindow.Activated += Window_Activated;
                _mainWindow.Closed += Window_Closed;
            }
            catch
            {
            }
        }
        else if (backdrop == "acrylic")
        {
            try
            {
                _backdropConfiguration = new SystemBackdropConfiguration();
                _backdropConfiguration.IsInputActive = true;
                _backdropConfiguration.Theme = IsSystemDarkTheme() ? SystemBackdropTheme.Dark : SystemBackdropTheme.Default;

                _acrylicController = new DesktopAcrylicController();
                var target = _mainWindow.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>();
                _acrylicController.AddSystemBackdropTarget(target);
                _acrylicController.SetSystemBackdropConfiguration(_backdropConfiguration);
                _mainWindow.Activated += Window_Activated;
                _mainWindow.Closed += Window_Closed;
            }
            catch
            {
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
