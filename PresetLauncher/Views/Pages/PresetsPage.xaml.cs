using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PresetLauncher.Models;
using PresetLauncher.Services;
using PresetLauncher.Views.Dialogs;

namespace PresetLauncher.Views.Pages
{
    public partial class PresetsPage : Page
    {
        private readonly LauncherService _launcher = new();
        private ObservableCollection<Preset> _filteredPresets = new();

        public PresetsPage()
        {
            InitializeComponent();
            UpdateLocalizedText();
            LoadPresets();
        }

        private void UpdateLocalizedText()
        {
            TxtTitle.Text = App.Localization.Get("nav.presets");
            TxtNewPreset.Text = App.Localization.Get("preset.new");
            TxtImport.Text = App.Localization.Get("preset.import");
            TxtSearch.Text = "";
            TxtNoPresets.Text = App.Localization.Get("preset.nopresets");
        }

        private void LoadPresets()
        {
            _filteredPresets = new ObservableCollection<Preset>(App.Data.Data.Presets);
            PresetsList.ItemsSource = _filteredPresets;
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            TxtNoPresets.Visibility = _filteredPresets.Count == 0 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = TxtSearch.Text.ToLower();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _filteredPresets = new ObservableCollection<Preset>(App.Data.Data.Presets);
            }
            else
            {
                _filteredPresets = new ObservableCollection<Preset>(
                    App.Data.Data.Presets.Where(p => 
                        p.Name.ToLower().Contains(searchText) ||
                        p.Description.ToLower().Contains(searchText) ||
                        p.Executables.Any(ex => ex.Name.ToLower().Contains(searchText))
                    )
                );
            }
            PresetsList.ItemsSource = _filteredPresets;
            UpdateEmptyState();
        }

        private void BtnNewPreset_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new PresetEditorDialog(null);
            if (dialog.ShowDialog() == true && dialog.Preset != null)
            {
                App.Data.AddPreset(dialog.Preset);
                LoadPresets();
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Preset preset)
            {
                if (preset.IsLocked)
                {
                    var passwordDialog = new PasswordDialog(false);
                    if (passwordDialog.ShowDialog() != true ||
                        !PasswordService.VerifyPassword(passwordDialog.Password, preset.PasswordHash))
                    {
                        MessageBox.Show(App.Localization.Get("dialog.password.wrong"), 
                            App.Localization.Get("dialog.password"), 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                var dialog = new PresetEditorDialog(preset);
                if (dialog.ShowDialog() == true)
                {
                    App.Data.Save();
                    LoadPresets();
                }
            }
        }

        private void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Preset preset)
            {
                if (preset.IsLocked)
                {
                    var passwordDialog = new PasswordDialog(false);
                    if (passwordDialog.ShowDialog() != true ||
                        !PasswordService.VerifyPassword(passwordDialog.Password, preset.PasswordHash))
                    {
                        MessageBox.Show(App.Localization.Get("dialog.password.wrong"),
                            App.Localization.Get("dialog.password"),
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (App.Settings.CurrentSettings.ConfirmBeforeLaunch)
                {
                    var result = MessageBox.Show(
                        App.Localization.Get("dialog.launch.message"),
                        App.Localization.Get("dialog.launch.title"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                btn.IsEnabled = false;
                try
                {
                    var executionDialog = new ExecutionDialog(preset, _launcher);
                    executionDialog.ShowDialog();
                }
                finally
                {
                    btn.IsEnabled = true;
                    LoadPresets();
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Preset preset)
            {
                var result = MessageBox.Show(
                    App.Localization.Get("dialog.delete.message"),
                    App.Localization.Get("dialog.delete.title"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    App.Data.RemovePreset(preset);
                    LoadPresets();
                }
            }
        }

        private void BtnDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Preset preset)
            {
                var clone = preset.Clone();
                App.Data.AddPreset(clone);
                LoadPresets();
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Preset preset)
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    FileName = $"{preset.Name}.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    var json = App.Data.ExportPreset(preset);
                    System.IO.File.WriteAllText(dialog.FileName, json);
                    MessageBox.Show(App.Localization.Get("preset.exported"), 
                        App.Localization.Get("dialog.export"), 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.IO.File.ReadAllText(dialog.FileName);
                    var preset = App.Data.ImportPreset(json);
                    if (preset != null)
                    {
                        App.Data.AddPreset(preset);
                        LoadPresets();
                        MessageBox.Show(App.Localization.Get("preset.imported"), 
                            App.Localization.Get("dialog.import"),
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(App.Localization.Get("preset.importError"), 
                            App.Localization.Get("dialog.error"),
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{App.Localization.Get("dialog.error")}: {ex.Message}", 
                        App.Localization.Get("dialog.error"),
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
