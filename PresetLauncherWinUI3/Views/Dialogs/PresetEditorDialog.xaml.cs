using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Models;
using PresetLauncher.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PresetLauncher.Views.Dialogs;

public sealed partial class PresetEditorDialog : ContentDialog
{
    private readonly Preset? _existingPreset;
    private readonly ObservableCollection<ExecutableItem> _executables = new();
    private ObservableCollection<ProcessInfo> _allProcesses = new();
    private LauncherService _launcher = new();
    private bool _processesLoaded = false;
    private readonly Dictionary<string, ProcessInfo> _processMap = new();
    private readonly List<string> _processDisplay = new();
    
    public Preset? ResultPreset { get; private set; }

    public PresetEditorDialog(Preset? preset)
    {
        this.InitializeComponent();
        _existingPreset = preset;
        UpdateLocalizedText();
        
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
        TxtAddExe.Text = App.Localization.Get("exe.add") ?? "Ajouter un exécutable";
        
        ExeList.ItemsSource = _executables;
        // set tooltips and localized texts
        ToolTipService.SetToolTip(BtnAddFromProcess, App.Localization.Get("tooltip.addFromProcess"));
        ProcessSuggestBox.PlaceholderText = App.Localization.Get("process.title");
    }

    private void UpdateLocalizedText()
    {
        try
        {
            this.Title = App.Localization.Get(_existingPreset != null ? "preset.edit" : "preset.new");
            this.PrimaryButtonText = App.Localization.Get("dialog.save");
            this.CloseButtonText = App.Localization.Get("dialog.cancel");
            TxtName.Header = App.Localization.Get("preset.name");
            TxtName.PlaceholderText = App.Localization.Get("preset.name.placeholder");
            TxtDescription.Header = App.Localization.Get("preset.description");
            TxtDescription.PlaceholderText = App.Localization.Get("preset.description.placeholder");
            TxtAddExe.Text = App.Localization.Get("exe.add") ?? "Add executable";
            // small helper texts
            this.SetValue(ProcessSearchHintProperty, App.Localization.Get("process.search.hint"));
            this.SetValue(AddFromProcessTextProperty, App.Localization.Get("exe.addFromProcess"));
            this.SetValue(ExeAdminProperty, App.Localization.Get("exe.admin"));
            this.SetValue(ExeNormalProperty, App.Localization.Get("exe.normal"));
            this.SetValue(ExeSecProperty, App.Localization.Get("exe.sec"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateLocalizedText Error: {ex}");
        }
    }

    // Dependency properties for bindings from XAML
    public static readonly DependencyProperty ProcessSearchHintProperty = DependencyProperty.Register("ProcessSearchHint", typeof(string), typeof(PresetEditorDialog), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty AddFromProcessTextProperty = DependencyProperty.Register("AddFromProcessText", typeof(string), typeof(PresetEditorDialog), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ExeAdminProperty = DependencyProperty.Register("ExeAdmin", typeof(string), typeof(PresetEditorDialog), new PropertyMetadata("Admin"));
    public static readonly DependencyProperty ExeNormalProperty = DependencyProperty.Register("ExeNormal", typeof(string), typeof(PresetEditorDialog), new PropertyMetadata("Normal"));
    public static readonly DependencyProperty ExeSecProperty = DependencyProperty.Register("ExeSec", typeof(string), typeof(PresetEditorDialog), new PropertyMetadata("sec"));

    public string ProcessSearchHint
    {
        get => (string)GetValue(ProcessSearchHintProperty);
        set => SetValue(ProcessSearchHintProperty, value);
    }

    public string AddFromProcessText
    {
        get => (string)GetValue(AddFromProcessTextProperty);
        set => SetValue(AddFromProcessTextProperty, value);
    }

    public string ExeAdmin
    {
        get => (string)GetValue(ExeAdminProperty);
        set => SetValue(ExeAdminProperty, value);
    }

    public string ExeNormal
    {
        get => (string)GetValue(ExeNormalProperty);
        set => SetValue(ExeNormalProperty, value);
    }

    public string ExeSec
    {
        get => (string)GetValue(ExeSecProperty);
        set => SetValue(ExeSecProperty, value);
    }

    private void BtnAddFromProcess_Click(object sender, RoutedEventArgs e)
    {
        if (_processesLoaded) 
        {
            ProcessSuggestBox.Visibility = Visibility.Visible;
            ProcessSuggestBox.Focus(FocusState.Programmatic);
            return;
        }

        try
        {
            var processes = _launcher.GetRunningProcessSummaries();
            _allProcesses.Clear();
            _processMap.Clear();
            _processDisplay.Clear();

            // group by stable key: prefer full executable path, fallback to process name
            var grouped = new Dictionary<string, ProcessInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in processes)
            {
                _launcher.TryGetExecutablePath(p.Id, out var path);
                p.Path = path ?? string.Empty;

                var key = !string.IsNullOrEmpty(p.Path)
                    ? System.IO.Path.GetFullPath(p.Path).ToLowerInvariant()
                    : p.Name.ToLowerInvariant();

                if (grouped.TryGetValue(key, out var existing))
                {
                    // prefer one with an actual path
                    if (string.IsNullOrEmpty(existing.Path) && !string.IsNullOrEmpty(p.Path))
                    {
                        grouped[key] = p;
                    }
                    else if (!string.IsNullOrEmpty(existing.Path) && !string.IsNullOrEmpty(p.Path))
                    {
                        // if both have paths, prefer Program Files location
                        var existingScore = (existing.Path.IndexOf("program files", StringComparison.OrdinalIgnoreCase) >= 0) ? 1 : 0;
                        var newScore = (p.Path.IndexOf("program files", StringComparison.OrdinalIgnoreCase) >= 0) ? 1 : 0;
                        if (newScore > existingScore)
                        {
                            grouped[key] = p;
                        }
                    }
                    // else keep existing
                }
                else
                {
                    grouped[key] = p;
                }
            }

            // build display list
            foreach (var kv in grouped)
            {
                var p = kv.Value;
                _allProcesses.Add(p);
                var display = p.ToString();
                _processMap[display] = p;
                _processDisplay.Add(display);
            }

            _processesLoaded = true;

            if (_allProcesses.Count == 0)
            {
                TxtProcessError.Text = App.Localization.Get("history.empty");
                TxtProcessError.Visibility = Visibility.Visible;
                ProcessSuggestBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtProcessError.Visibility = Visibility.Collapsed;
                ProcessSuggestBox.ItemsSource = _processDisplay;
                ProcessSuggestBox.Visibility = Visibility.Visible;
                ProcessSuggestBox.Focus(FocusState.Programmatic);
            }
        }
        catch (Exception ex)
        {
            TxtProcessError.Text = ex.Message;
            TxtProcessError.Visibility = Visibility.Visible;
            ProcessSuggestBox.Visibility = Visibility.Collapsed;
        }
    }

    private void ProcessSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var text = sender.Text?.ToLower() ?? string.Empty;
            var filtered = _processDisplay.Where(d => d.ToLower().Contains(text)).ToList();
            sender.ItemsSource = filtered.Count > 0 ? filtered : new List<string>();
        }
    }

    private async void ProcessSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string key && _processMap.TryGetValue(key, out var pi))
        {
            try
            {
                string? resolvedPath = null;
                
                // Try to resolve path (with multiple fallbacks)
                if (_launcher.TryGetExecutablePath(pi.Id, out var path1) && !string.IsNullOrEmpty(path1))
                {
                    resolvedPath = path1;
                }

                if (string.IsNullOrEmpty(resolvedPath))
                {
                    // Could not resolve automatically — ask user to pick file
                    TxtProcessError.Text = App.Localization.Get("exe.invalid") + " — " + App.Localization.Get("dialog.selectFileManually");
                    TxtProcessError.Visibility = Visibility.Visible;

                    var picker = new FileOpenPicker();
                    picker.SuggestedStartLocation = PickerLocationId.Desktop;
                    picker.FileTypeFilter.Add(".exe");
                    picker.FileTypeFilter.Add(".bat");
                    picker.FileTypeFilter.Add(".cmd");
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
                    InitializeWithWindow.Initialize(picker, hwnd);
                    var file = await picker.PickSingleFileAsync();
                    if (file != null)
                    {
                        resolvedPath = file.Path;
                    }
                    else
                    {
                        ProcessSuggestBox.Text = string.Empty;
                        return; // user cancelled
                    }
                }

                if (!string.IsNullOrEmpty(resolvedPath))
                {
                    var exe = new ExecutableItem
                    {
                        Name = System.IO.Path.GetFileNameWithoutExtension(resolvedPath),
                        Path = resolvedPath,
                        Order = _executables.Count
                    };
                    exe.ValidatePath();
                    _executables.Add(exe);

                    // Reset
                    ProcessSuggestBox.Text = string.Empty;
                    ProcessSuggestBox.Visibility = Visibility.Collapsed;
                    TxtProcessError.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                TxtProcessError.Text = ex.Message;
                TxtProcessError.Visibility = Visibility.Visible;
            }
        }
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
