using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace PresetLauncher.Views.Pages
{
    public partial class StatisticsPage : Page
    {
        public StatisticsPage()
        {
            InitializeComponent();
            UpdateLocalizedText();
            LoadStatistics();
        }

        private void UpdateLocalizedText()
        {
            TxtTitle.Text = App.Localization.Get("stats.title");
            TxtPresetsLabel.Text = App.Localization.Get("stats.presetsCount");
            TxtLaunchesLabel.Text = App.Localization.Get("stats.totalLaunches");
            TxtMostUsed.Text = App.Localization.Get("stats.mostUsed");
        }

        private void LoadStatistics()
        {
            TxtPresetsCount.Text = App.Data.Data.Presets.Count.ToString();
            
            var totalLaunches = App.Data.Data.Presets.Sum(p => p.LaunchCount);
            TxtLaunchesCount.Text = totalLaunches.ToString();

            TxtProgramsCount.Text = App.Data.Data.UsageStats.Count.ToString();

            var mostUsed = App.Data.Data.UsageStats
                .OrderByDescending(kv => kv.Value)
                .Take(10)
                .Select(kv => new UsageItem
                {
                    Name = Path.GetFileNameWithoutExtension(kv.Key),
                    Count = kv.Value
                })
                .ToList();

            MostUsedList.ItemsSource = mostUsed;
        }
    }

    public class UsageItem
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
