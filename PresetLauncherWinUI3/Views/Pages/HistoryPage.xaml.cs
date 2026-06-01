using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PresetLauncher.Views.Pages;

public sealed partial class HistoryPage : Page
{
    public HistoryPage()
    {
        this.InitializeComponent();
        UpdateLocalizedText();
        RefreshHistory();
    }

    private void UpdateLocalizedText()
    {
        TxtClear.Text = App.Localization.Get("history.clear");
        if (TxtEmpty != null)
            TxtEmpty.Text = App.Localization.Get("history.empty");
        if (TxtHistoryTitle != null)
            TxtHistoryTitle.Text = App.Localization.Get("history.title");
        if (HistoryCountText != null)
            HistoryCountText.Text = App.Localization.Get("history.subtitle");
        if (FilterAll != null)
            FilterAll.Content = App.Localization.Get("history.filter.all");
        if (FilterSuccess != null)
            FilterSuccess.Content = App.Localization.Get("history.filter.success");
        if (FilterFailures != null)
            FilterFailures.Content = App.Localization.Get("history.filter.failures");
        if (FilterToday != null)
            FilterToday.Content = App.Localization.Get("history.filter.today");
    }

    private void RefreshHistory()
    {
        if (App.Data?.Data?.History != null && HistoryList != null)
        {
            HistoryList.ItemsSource = App.Data.Data.History;
            if (TxtEmpty != null)
                TxtEmpty.Visibility = App.Data.Data.History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = App.Localization.Get("history.clear"),
            Content = App.Localization.Get("history.clearConfirm"),
            PrimaryButtonText = App.Localization.Get("dialog.yes"),
            CloseButtonText = App.Localization.Get("dialog.no"),
            XamlRoot = this.XamlRoot
        };
        
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            App.Data.ClearHistory();
            RefreshHistory();
        }
    }

    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Filter implementation for future use
        RefreshHistory();
    }
}
