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
        NavView.Header = App.Localization.Get("nav.presets");
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            NavView.Header = App.Localization.Get("nav.settings");
            return;
        }

        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "presets":
                    ContentFrame.Navigate(typeof(PresetsPage));
                    NavView.Header = App.Localization.Get("nav.presets");
                    break;
                case "history":
                    ContentFrame.Navigate(typeof(HistoryPage));
                    NavView.Header = App.Localization.Get("nav.history");
                    break;
                case "statistics":
                    ContentFrame.Navigate(typeof(StatisticsPage));
                    NavView.Header = App.Localization.Get("nav.statistics");
                    break;
            }
        }
    }

    public void RefreshUI()
    {
        UpdateLocalizedText();
    }

    public void NavigateToPresets()
    {
        NavView.SelectedItem = NavPresets;
        ContentFrame.Navigate(typeof(PresetsPage));
        NavView.Header = App.Localization.Get("nav.presets");
    }
}
