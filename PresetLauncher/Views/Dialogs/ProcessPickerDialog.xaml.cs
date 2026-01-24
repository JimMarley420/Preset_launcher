using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PresetLauncher.Services;

namespace PresetLauncher.Views.Dialogs
{
    public partial class ProcessPickerDialog : Window
    {
        private readonly LauncherService _launcher = new();
        private readonly List<SelectableProcess> _allProcesses = new();
        private ObservableCollection<SelectableProcess> _filteredProcesses = new();

        public List<ProcessInfo>? SelectedProcesses { get; private set; }

        public ProcessPickerDialog()
        {
            InitializeComponent();
            UpdateLocalizedText();
            LoadProcesses();
        }

        private void UpdateLocalizedText()
        {
            Title = App.Localization.Get("process.title");
        }

        private void LoadProcesses()
        {
            var processes = _launcher.GetRunningProcesses();
            _allProcesses.Clear();
            
            foreach (var process in processes.DistinctBy(p => p.Path).OrderBy(p => p.Name))
            {
                _allProcesses.Add(new SelectableProcess
                {
                    Name = process.Name,
                    Path = process.Path,
                    Id = process.Id,
                    IsSelected = false
                });
            }

            _filteredProcesses = new ObservableCollection<SelectableProcess>(_allProcesses);
            ProcessList.ItemsSource = _filteredProcesses;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = TxtSearch.Text.ToLower();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _filteredProcesses = new ObservableCollection<SelectableProcess>(_allProcesses);
            }
            else
            {
                _filteredProcesses = new ObservableCollection<SelectableProcess>(
                    _allProcesses.Where(p => 
                        p.Name.ToLower().Contains(searchText) ||
                        p.Path.ToLower().Contains(searchText)
                    )
                );
            }
            ProcessList.ItemsSource = _filteredProcesses;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            SelectedProcesses = _allProcesses
                .Where(p => p.IsSelected)
                .Select(p => new ProcessInfo
                {
                    Name = p.Name,
                    Path = p.Path,
                    Id = p.Id
                })
                .ToList();

            DialogResult = true;
            Close();
        }
    }

    public class SelectableProcess
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int Id { get; set; }
        public bool IsSelected { get; set; }
    }
}
