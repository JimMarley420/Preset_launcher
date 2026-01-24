using System.Windows;

namespace PresetLauncher.Views.Dialogs
{
    public partial class PasswordDialog : Window
    {
        private readonly bool _isCreating;
        public string Password { get; private set; } = string.Empty;

        public PasswordDialog(bool isCreating)
        {
            InitializeComponent();
            _isCreating = isCreating;

            if (isCreating)
            {
                Title = App.Localization.Get("dialog.password.set");
                TxtLabel.Text = App.Localization.Get("dialog.password.set");
                TxtConfirmLabel.Text = App.Localization.Get("dialog.password.confirm");
                TxtConfirmLabel.Visibility = Visibility.Visible;
                PwdConfirm.Visibility = Visibility.Visible;
                Height = 250;
            }
            else
            {
                Title = App.Localization.Get("dialog.password");
                TxtLabel.Text = App.Localization.Get("dialog.password.enter");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (_isCreating)
            {
                if (PwdPassword.Password != PwdConfirm.Password)
                {
                    MessageBox.Show(App.Localization.Get("dialog.password.mismatch"), 
                        App.Localization.Get("dialog.error"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(PwdPassword.Password))
                {
                    MessageBox.Show(App.Localization.Get("dialog.password.empty"), 
                        App.Localization.Get("dialog.error"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            Password = PwdPassword.Password;
            DialogResult = true;
            Close();
        }
    }
}
