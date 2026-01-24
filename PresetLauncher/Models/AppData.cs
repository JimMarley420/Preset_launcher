using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PresetLauncher.Models
{
    public class AppData
    {
        public ObservableCollection<Preset> Presets { get; set; } = new();
        public List<ExecutionHistory> History { get; set; } = new();
        public Dictionary<string, int> UsageStats { get; set; } = new();
    }
}
