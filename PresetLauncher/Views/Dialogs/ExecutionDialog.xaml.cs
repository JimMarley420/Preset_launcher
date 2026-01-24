using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using PresetLauncher.Models;
using PresetLauncher.Services;

namespace PresetLauncher.Views.Dialogs
{
    public partial class ExecutionDialog : Window
    {
        private readonly Preset _preset;
        private readonly LauncherService _launcher;
        private readonly ObservableCollection<ExecutionResultItem> _results = new();

        public ExecutionDialog(Preset preset, LauncherService launcher)
        {
            InitializeComponent();
            _preset = preset;
            _launcher = launcher;
            
            TxtTitle.Text = $"Lancement de {preset.Name}...";
            ResultList.ItemsSource = _results;

            _launcher.StatusChanged += OnStatusChanged;

            Loaded += async (s, e) => await ExecuteAsync();
        }

        private void OnStatusChanged(object? sender, string status)
        {
            Dispatcher.Invoke(() => TxtStatus.Text = status);
        }

        private async Task ExecuteAsync()
        {
            var history = await _launcher.LaunchPresetAsync(_preset);

            foreach (var detail in history.Details)
            {
                _results.Add(new ExecutionResultItem
                {
                    Name = detail.ExecutableName,
                    Success = detail.Success,
                    Message = detail.ErrorMessage ?? string.Empty,
                    StatusIcon = detail.Success ? "✓" : "✗"
                });
            }

            TxtStatus.Text = history.FailedCount == 0
                ? App.Localization.Get("execution.success")
                : $"{history.FailedCount} échec(s) sur {history.ExecutablesCount}";

            BtnClose.IsEnabled = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _launcher.StatusChanged -= OnStatusChanged;
            Close();
        }
    }

    public class ExecutionResultItem
    {
        public string Name { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string StatusIcon { get; set; } = string.Empty;
    }
}
