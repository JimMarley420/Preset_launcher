using System;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Models;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PresetLauncher.Views.Dialogs;

public sealed partial class PresetEditorDialog : ContentDialog
{
    private readonly Preset? _existingPreset;
    private readonly ObservableCollection<ExecutableItem> _executables = new();
    
    public Preset? ResultPreset { get; private set; }

    public PresetEditorDialog(Preset? preset)
    {
        this.InitializeComponent();
        _existingPreset = preset;
        
        if (preset != null)
        {
            this.Title = App.Localization.Get("preset.edit");
            TxtName.Text = preset.Name;
            TxtDescription.Text = preset.Description;
            
            foreach (var exe in preset.Executables)
            {
                _executables.Add(exe);
            }
        }
        else
        {
            this.Title = App.Localization.Get("preset.new");
        }

        this.PrimaryButtonText = App.Localization.Get("dialog.save");
        this.CloseButtonText = App.Localization.Get("dialog.cancel");
        TxtName.Header = App.Localization.Get("preset.name");
        TxtDescription.Header = App.Localization.Get("preset.description");
        TxtAddExe.Text = App.Localization.Get("exe.add");
        
        ExeList.ItemsSource = _executables;
    }

    private async void BtnAddExe_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".bat");
        picker.FileTypeFilter.Add(".cmd");
        picker.FileTypeFilter.Add(".lnk");
        picker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(App.Current.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            var exe = new ExecutableItem
            {
                Name = Path.GetFileNameWithoutExtension(file.Name),
                Path = file.Path,
                Order = _executables.Count
            };
            exe.ValidatePath();
            _executables.Add(exe);
        }
    }

    private void BtnRemoveExe_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ExecutableItem exe)
        {
            _executables.Remove(exe);
        }
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            args.Cancel = true;
            return;
        }

        if (_existingPreset != null)
        {
            _existingPreset.Name = TxtName.Text.Trim();
            _existingPreset.Description = TxtDescription.Text?.Trim() ?? string.Empty;
            _existingPreset.Executables.Clear();
            foreach (var exe in _executables)
            {
                _existingPreset.Executables.Add(exe);
            }
            ResultPreset = _existingPreset;
        }
        else
        {
            ResultPreset = new Preset
            {
                Name = TxtName.Text.Trim(),
                Description = TxtDescription.Text?.Trim() ?? string.Empty
            };
            foreach (var exe in _executables)
            {
                ResultPreset.Executables.Add(exe);
            }
        }
    }
}
