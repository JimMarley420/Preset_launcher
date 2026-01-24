using System;
using System.Windows;
using Microsoft.Win32;

namespace PresetLauncher.Services
{
    public class ThemeService
    {
        public event EventHandler<string>? ThemeChanged;

        public void ApplyTheme(string theme)
        {
            var actualTheme = theme;
            
            if (theme == "system")
            {
                actualTheme = IsSystemDarkTheme() ? "dark" : "light";
            }

            var themeUri = actualTheme == "dark" 
                ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
                : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

            var themeDictionary = new ResourceDictionary { Source = themeUri };
            
            if (Application.Current.Resources.MergedDictionaries.Count > 0)
            {
                Application.Current.Resources.MergedDictionaries[0] = themeDictionary;
            }
            else
            {
                Application.Current.Resources.MergedDictionaries.Insert(0, themeDictionary);
            }

            ThemeChanged?.Invoke(this, actualTheme);
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
}
