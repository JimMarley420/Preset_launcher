using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Services;
using static PresetLauncher.Views.DesktopUi;

namespace PresetLauncher.Views;

internal sealed class ProcessSelector
{
    private readonly LauncherService _launcher = new();
    private readonly TextBox _search = new();
    private readonly ListView _results = new() { Height = 180 };
    private readonly TextBlock _status = Text("");
    private ProcessInfo[] _processes = [];
    private bool _loading;

    public async Task<string?> PickAsync(XamlRoot root)
    {
        using var cancellation = new CancellationTokenSource();
        _search.PlaceholderText = T("process.search");
        Label(_search, _search.PlaceholderText);
        Label(_results, T("process.title"));
        var refresh = Button("process.refresh", async (_, _) => await LoadAsync(cancellation.Token));
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(AdaptivePair(_search, refresh, 300));
        panel.Children.Add(_status);
        panel.Children.Add(_results);
        var dialog = Dialog(root, "process.title", panel, "process.select");
        dialog.IsPrimaryButtonEnabled = false;
        _search.TextChanged += (_, _) => Filter();
        _results.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = _results.SelectedItem is ListViewItem;
        dialog.Opened += async (_, _) =>
        {
            _search.Focus(FocusState.Programmatic);
            await LoadAsync(cancellation.Token);
        };
        dialog.Closing += (_, _) => cancellation.Cancel();
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary && _results.SelectedItem is ListViewItem item ? item.Tag as string : null;
    }

    private async Task LoadAsync(CancellationToken cancellation)
    {
        if (_loading) return;
        _loading = true;
        _status.Text = T("process.loading");
        _results.Items.Clear();
        try
        {
            _processes = await Task.Run(() => _launcher.GetRunningProcesses()
                .DistinctBy(process => process.Path, StringComparer.OrdinalIgnoreCase)
                .OrderBy(process => process.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(process => process.Path, StringComparer.OrdinalIgnoreCase).ToArray(), cancellation);
            if (!cancellation.IsCancellationRequested)
            {
                _loading = false;
                Filter();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (System.ComponentModel.Win32Exception)
        {
            if (!cancellation.IsCancellationRequested)
                _status.Text = T("process.error");
        }
        finally { _loading = false; }
    }

    private void Filter()
    {
        if (_loading) return;
        var filter = _search.Text.Trim();
        _results.Items.Clear();
        foreach (var process in _processes.Where(p => p.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                                                     p.Path.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            var row = new StackPanel();
            row.Children.Add(Text(process.Name, "DesktopSectionTitle"));
            row.Children.Add(PathText(process.Path));
            _results.Items.Add(new ListViewItem { Content = row, Tag = process.Path, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        }
        _status.Text = _results.Items.Count == 0
            ? T(_processes.Length == 0 ? "process.empty" : "process.noMatches")
            : "";
    }
}
