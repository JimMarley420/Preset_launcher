using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Models;
using PresetLauncher.Services;
using PresetLauncher.Views.Dialogs;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PresetLauncher.Views.Pages;

public sealed partial class PresetsPage : Page
{
    public ObservableCollection<Preset> FilteredPresets { get; } = new();
    private readonly LauncherService _launcher = new();

    public PresetsPage()
    {
        this.InitializeComponent();
        UpdateLocalizedText();
        RefreshPresets();
    }

    private void UpdateLocalizedText()
    {
        TxtNewPreset.Text = App.Localization.Get("preset.new");
        TxtImport.Text = App.Localization.Get("preset.import");
        SearchBox.PlaceholderText = App.Localization.Get("search.placeholder");
        TxtNoPresets.Text = App.Localization.Get("preset.nopresets");
    }

    private void RefreshPresets(string? searchText = null)
    {
        FilteredPresets.Clear();
        
        var presets = App.Data.Data.Presets.AsEnumerable();
        
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            presets = presets.Where(p => 
                p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var preset in presets)
        {
            FilteredPresets.Add(preset);
        }

        TxtNoPresets.Visibility = FilteredPresets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            RefreshPresets(sender.Text);
        }
    }

    private async void BtnNewPreset_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PresetEditorDialog(null)
        {
            XamlRoot = this.XamlRoot
        };
        
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.ResultPreset != null)
        {
            App.Data.AddPreset(dialog.ResultPreset);
            RefreshPresets();
        }
    }

    private async void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".json");

        var hwnd = WindowNative.GetWindowHandle(App.Current.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            var json = await Windows.Storage.FileIO.ReadTextAsync(file);
            var preset = App.Data.ImportPreset(json);
            if (preset != null)
            {
                App.Data.AddPreset(preset);
                RefreshPresets();
            }
        }
    }

    private async void BtnLaunch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Preset preset)
        {
            if (App.Settings.CurrentSettings.ConfirmBeforeLaunch)
            {
                var dialog = new ContentDialog
                {
                    Title = App.Localization.Get("dialog.launch.title"),
                    Content = App.Localization.Get("dialog.launch.message"),
                    PrimaryButtonText = App.Localization.Get("dialog.yes"),
                    CloseButtonText = App.Localization.Get("dialog.no"),
                    XamlRoot = this.XamlRoot
                };
                
                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary) return;
            }

            await _launcher.LaunchPresetAsync(preset);
        }
    }

    private async void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Preset preset)
        {
            var dialog = new PresetEditorDialog(preset)
            {
                XamlRoot = this.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                App.Data.Save();
                RefreshPresets();
            }
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Preset preset)
        {
            var dialog = new ContentDialog
            {
                Title = App.Localization.Get("dialog.delete.title"),
                Content = App.Localization.Get("dialog.delete.message"),
                PrimaryButtonText = App.Localization.Get("dialog.yes"),
                CloseButtonText = App.Localization.Get("dialog.no"),
                XamlRoot = this.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                App.Data.RemovePreset(preset);
                RefreshPresets();
            }
        }
    }
}
