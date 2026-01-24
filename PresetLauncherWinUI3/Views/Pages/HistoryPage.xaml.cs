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
        TxtEmpty.Text = App.Localization.Get("history.empty");
    }

    private void RefreshHistory()
    {
        HistoryList.ItemsSource = App.Data.Data.History;
        TxtEmpty.Visibility = App.Data.Data.History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
}
