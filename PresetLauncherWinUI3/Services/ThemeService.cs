using System;
using Microsoft.UI.Xaml;
using Microsoft.Win32;

namespace PresetLauncher.Services;

public class ThemeService
{
    public event EventHandler<string>? ThemeChanged;
    
    private Window? _mainWindow;

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
