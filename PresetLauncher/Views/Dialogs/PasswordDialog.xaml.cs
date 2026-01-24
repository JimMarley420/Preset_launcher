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
                TxtConfirmLabel.Text = "Confirmez le mot de passe";
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
                    MessageBox.Show("Les mots de passe ne correspondent pas.", "Erreur",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(PwdPassword.Password))
                {
                    MessageBox.Show("Le mot de passe ne peut pas être vide.", "Erreur",
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
