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
        PresetsRepeater.ElementPrepared += PresetsRepeater_ElementPrepared;
    }

    private void PresetsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        try
        {
            if (args.Element is FrameworkElement fe)
            {
                var launchBtn = fe.FindName("BtnLaunch") as Button;
                var editBtn = fe.FindName("BtnEdit") as Button;
                var moreBtn = fe.FindName("BtnMore") as Button;

                if (launchBtn != null)
                    ToolTipService.SetToolTip(launchBtn, App.Localization.Get("tooltip.launchPreset"));
                if (editBtn != null)
                    ToolTipService.SetToolTip(editBtn, App.Localization.Get("tooltip.editPreset"));

                // For elements inside the DataTemplate (MenuFlyout items, labels), locate them in the visual tree
                try
                {
                    // Programs label
                    var programsLabel = fe.FindName("TxtProgramsLabel") as TextBlock;
                    if (programsLabel != null)
                        programsLabel.Text = App.Localization.Get("preset.programsLabel");

                    // MenuFlyout: find flyout on the 'more' button and set items text/tooltips
                    if (moreBtn != null && moreBtn.Flyout is MenuFlyout mf)
                    {
                        foreach (var item in mf.Items)
                        {
                            if (item is MenuFlyoutItem mfi)
                            {
                                // Attempt to set by known order/text
                                var txt = mfi.Text?.ToString() ?? string.Empty;
                                if (txt.Contains("Fermer") || txt.Contains("Close"))
                                {
                                    mfi.Text = App.Localization.Get("menu.closeProcesses");
                                    mfi.Tag = moreBtn.Tag;
                                }
                                else if (txt.Contains("Supprimer") || txt.Contains("Delete"))
                                {
                                    mfi.Text = App.Localization.Get("menu.delete");
                                    mfi.Tag = moreBtn.Tag;
                                }
                            }
                        }
                    }
                }
                catch { }
            }
        }
        catch
        {
        }
    }

    private void UpdateLocalizedText()
    {
        TxtNewPreset.Text = App.Localization.Get("preset.new");
        TxtImport.Text = App.Localization.Get("preset.import");
        SearchBox.PlaceholderText = App.Localization.Get("search.placeholder");
        TxtNoPresets.Text = App.Localization.Get("preset.nopresets");
        if (TxtPresetsTitle != null)
            TxtPresetsTitle.Text = App.Localization.Get("preset.title");
        if (PresetCountText != null)
            PresetCountText.Text = App.Localization.Get("preset.subtitle");
        // The per-item labels inside the DataTemplate are localized when each element is prepared
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

        EmptyStatePanel.Visibility = FilteredPresets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            RefreshPresets(sender.Text);
        }
    }

    private async void BtnCloseProcesses_Click(object sender, RoutedEventArgs e)
    {
        Preset? preset = null;

        if (sender is Button btn)
            preset = btn.Tag as Preset;
        else if (sender is MenuFlyoutItem mfi)
            preset = mfi.Tag as Preset;

        if (preset == null) return;

        // First: Show confirmation dialog
        var confirmDialog = new ContentDialog
        {
            Title = App.Localization.Get("dialog.close.title"),
            Content = App.Localization.Get("dialog.close.message"),
            PrimaryButtonText = App.Localization.Get("dialog.yes"),
            CloseButtonText = App.Localization.Get("dialog.cancel"),
            XamlRoot = this.XamlRoot
        };

        var confirmed = await confirmDialog.ShowAsync();
        if (confirmed != ContentDialogResult.Primary) return;

        // Second: Close processes
        var count = await _launcher.ClosePresetProcessesAsync(preset);

        var resultDialog = new ContentDialog
        {
            Title = App.Localization.Get("dialog.confirm"),
            Content = string.Format(App.Localization.Get("execution.processesClosed"), count),
            CloseButtonText = App.Localization.Get("dialog.ok"),
            XamlRoot = this.XamlRoot
        };

        await resultDialog.ShowAsync();
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
            // First: Ask whether to launch all or only missing
            var choiceDialog = new ContentDialog
            {
                Title = App.Localization.Get("dialog.launch.title"),
                Content = App.Localization.Get("dialog.launch.message"),
                PrimaryButtonText = App.Localization.Get("preset.launchAll"),
                SecondaryButtonText = App.Localization.Get("preset.launchMissing"),
                CloseButtonText = App.Localization.Get("dialog.cancel"),
                XamlRoot = this.XamlRoot
            };

            var choice = await choiceDialog.ShowAsync();
            if (choice == ContentDialogResult.None) return;

            // Second: Show confirmation dialog
            var confirmDialog = new ContentDialog
            {
                Title = App.Localization.Get("dialog.confirm"),
                Content = App.Localization.Get("execution.inProgress"),
                PrimaryButtonText = App.Localization.Get("dialog.yes"),
                CloseButtonText = App.Localization.Get("dialog.cancel"),
                XamlRoot = this.XamlRoot
            };

            var confirmed = await confirmDialog.ShowAsync();
            if (confirmed != ContentDialogResult.Primary) return;

            // Third: Execute the appropriate action
            if (choice == ContentDialogResult.Primary)
            {
                await _launcher.LaunchPresetAsync(preset);
            }
            else if (choice == ContentDialogResult.Secondary)
            {
                await _launcher.LaunchPresetMissingAsync(preset);
            }
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
        Preset? preset = null;

        if (sender is Button btn)
            preset = btn.Tag as Preset;
        else if (sender is MenuFlyoutItem mfi)
            preset = mfi.Tag as Preset;

        if (preset == null) return;

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
