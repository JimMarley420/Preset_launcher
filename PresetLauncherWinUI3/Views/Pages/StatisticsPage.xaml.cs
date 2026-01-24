using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;

namespace PresetLauncher.Views.Pages;

public sealed partial class StatisticsPage : Page
{
    public StatisticsPage()
    {
        this.InitializeComponent();
        UpdateLocalizedText();
        LoadStatistics();
    }

    private void UpdateLocalizedText()
    {
        TxtPresetsLabel.Text = App.Localization.Get("stats.presetsCount");
        TxtTotalLaunchesLabel.Text = App.Localization.Get("stats.totalLaunches");
        TxtUniqueProgramsLabel.Text = App.Localization.Get("stats.uniquePrograms");
        TxtMostUsed.Text = App.Localization.Get("stats.mostUsed");
    }

    private void LoadStatistics()
    {
        var presets = App.Data.Data.Presets;
        var usageStats = App.Data.Data.UsageStats;

        TxtPresetsCount.Text = presets.Count.ToString();
        
        var totalLaunches = presets.Sum(p => p.LaunchCount);
        TxtTotalLaunches.Text = totalLaunches.ToString();
        
        TxtUniquePrograms.Text = usageStats.Count.ToString();

        var mostUsed = usageStats
            .OrderByDescending(x => x.Value)
            .Take(10)
            .Select(x => new UsageStat { Path = x.Key, Count = x.Value })
            .ToList();

        MostUsedList.ItemsSource = mostUsed;
    }

    private class UsageStat
    {
        public string Path { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
