using System.Windows;
using System.Windows.Controls;

namespace PresetLauncher.Views.Pages
{
    public partial class HistoryPage : Page
    {
        public HistoryPage()
        {
            InitializeComponent();
            UpdateLocalizedText();
            LoadHistory();
        }

        private void UpdateLocalizedText()
        {
            TxtTitle.Text = App.Localization.Get("history.title");
            TxtClear.Text = App.Localization.Get("history.clear");
            TxtEmpty.Text = App.Localization.Get("history.empty");
        }

        private void LoadHistory()
        {
            HistoryList.ItemsSource = App.Data.Data.History;
            TxtEmpty.Visibility = App.Data.Data.History.Count == 0 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                App.Localization.Get("history.clearConfirm"),
                App.Localization.Get("dialog.confirm"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                App.Data.ClearHistory();
                LoadHistory();
            }
        }
    }
}
