using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Views.Pages;

namespace PresetLauncher;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        
        this.Title = App.Localization.Get("app.title");
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        
        AppTitleText.Text = App.Localization.Get("app.title");
        
        App.Theme.SetWindow(this);
        App.Theme.ApplyTheme(App.Settings.CurrentSettings.Theme);
        // Apply saved backdrop/materials
        App.Theme.ApplyBackdrop(App.Settings.CurrentSettings.Backdrop);
        
        // Définir la taille de la fenêtre
        AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(850, 600));
        
        UpdateLocalizedText();
    }

    private void UpdateLocalizedText()
    {
        NavPresets.Content = App.Localization.Get("nav.presets");
        NavHistory.Content = App.Localization.Get("nav.history");
        NavStats.Content = App.Localization.Get("nav.statistics");
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.SelectedItem = NavPresets;
        ContentFrame.Navigate(typeof(PresetsPage));
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "presets":
                    ContentFrame.Navigate(typeof(PresetsPage));
                    break;
                case "history":
                    ContentFrame.Navigate(typeof(HistoryPage));
                    break;
                case "statistics":
                    ContentFrame.Navigate(typeof(StatisticsPage));
                    break;
            }
        }
    }

    internal void SetUpdateGate(bool blocked)
    {
        NavView.IsEnabled = !blocked;
        ContentFrame.IsEnabled = !blocked;
    }

    public void RefreshUI()
    {
        // Update nav/localized texts
        UpdateLocalizedText();

        try
        {
            var content = ContentFrame.Content;
            if (content != null)
            {
                // Try to invoke a RefreshUI method on the current page (public or non-public)
                var mi = content.GetType().GetMethod("RefreshUI", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (mi != null)
                {
                    mi.Invoke(content, null);
                }
                else
                {
                    // Navigate to the same page type to force re-creation (will update UI texts on load)
                    var t = content.GetType();
                    ContentFrame.Navigate(t);
                }
            }
        }
        catch
        {
            // ignore any errors while refreshing pages
        }
    }

    public void NavigateToPresets()
    {
        NavView.SelectedItem = NavPresets;
        ContentFrame.Navigate(typeof(PresetsPage));
    }
}
