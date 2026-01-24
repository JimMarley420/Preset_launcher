using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PresetLauncher.Models;
using PresetLauncher.Services;

namespace PresetLauncher.Views.Dialogs
{
    public partial class PresetEditorDialog : Window
    {
        public Preset? Preset { get; private set; }
        private readonly ObservableCollection<ExecutableItem> _executables;
        private readonly bool _isNew;

        public PresetEditorDialog(Preset? preset)
        {
            InitializeComponent();
            
            _isNew = preset == null;
            Preset = preset ?? new Preset();
            _executables = new ObservableCollection<ExecutableItem>(Preset.Executables);

            TxtName.Text = Preset.Name;
            TxtDescription.Text = Preset.Description;
            ChkLocked.IsChecked = Preset.IsLocked;
            ExeList.ItemsSource = _executables;

            UpdateLocalizedText();

            if (_isNew)
            {
                Title = App.Localization.Get("preset.new");
            }
            else
            {
                Title = App.Localization.Get("preset.edit");
            }
        }

        private void UpdateLocalizedText()
        {
            TxtNameLabel.Text = App.Localization.Get("preset.name");
            TxtDescLabel.Text = App.Localization.Get("preset.description");
            TxtAddFile.Text = App.Localization.Get("exe.addFromFile");
            TxtAddProcess.Text = App.Localization.Get("exe.addFromProcess");
            TxtCancel.Text = App.Localization.Get("dialog.cancel");
            TxtSave.Text = App.Localization.Get("dialog.save");
            TxtLock.Text = App.Localization.Get("lock.preset");
        }

        private void BtnAddFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    var exe = new ExecutableItem(file)
                    {
                        Order = _executables.Count
                    };
                    _executables.Add(exe);
                }
            }
        }

        private void BtnAddProcess_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProcessPickerDialog();
            if (dialog.ShowDialog() == true && dialog.SelectedProcesses != null)
            {
                foreach (var process in dialog.SelectedProcesses)
                {
                    if (!_executables.Any(e => e.Path == process.Path))
                    {
                        var exe = new ExecutableItem(process.Path)
                        {
                            Name = process.Name,
                            Order = _executables.Count
                        };
                        _executables.Add(exe);
                    }
                }
            }
        }

        private void BtnRemoveExe_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ExecutableItem exe)
            {
                _executables.Remove(exe);
                ReorderExecutables();
            }
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ExecutableItem exe)
            {
                var index = _executables.IndexOf(exe);
                if (index > 0)
                {
                    _executables.Move(index, index - 1);
                    ReorderExecutables();
                }
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ExecutableItem exe)
            {
                var index = _executables.IndexOf(exe);
                if (index < _executables.Count - 1)
                {
                    _executables.Move(index, index + 1);
                    ReorderExecutables();
                }
            }
        }

        private void ReorderExecutables()
        {
            for (int i = 0; i < _executables.Count; i++)
            {
                _executables[i].Order = i;
            }
        }

        private void ChkLocked_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkLocked.IsChecked == true && string.IsNullOrEmpty(Preset?.PasswordHash))
            {
                var passwordDialog = new PasswordDialog(true);
                if (passwordDialog.ShowDialog() == true && !string.IsNullOrEmpty(passwordDialog.Password))
                {
                    if (Preset != null)
                    {
                        Preset.PasswordHash = PasswordService.HashPassword(passwordDialog.Password);
                    }
                }
                else
                {
                    ChkLocked.IsChecked = false;
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Veuillez entrer un nom pour le preset.", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Preset != null)
            {
                Preset.Name = TxtName.Text.Trim();
                Preset.Description = TxtDescription.Text.Trim();
                Preset.IsLocked = ChkLocked.IsChecked ?? false;
                Preset.LastModified = System.DateTime.Now;

                Preset.Executables.Clear();
                foreach (var exe in _executables)
                {
                    exe.ValidatePath();
                    Preset.Executables.Add(exe);
                }
            }

            DialogResult = true;
            Close();
        }
    }
}
