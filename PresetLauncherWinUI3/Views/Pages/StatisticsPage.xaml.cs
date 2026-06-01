using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using PresetLauncher.Models;

namespace PresetLauncher.Views.Pages;

public sealed partial class StatisticsPage : Page
{
    public StatisticsPage()
    {
        this.InitializeComponent();
        try
        {
            UpdateLocalizedText();
            LoadStatistics();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StatisticsPage Error: {ex}");
        }
    }

    private void UpdateLocalizedText()
    {
        try
        {
            TxtPresetsLabel.Text = App.Localization.Get("stats.presetsCount");
            TxtTotalLaunchesLabel.Text = App.Localization.Get("stats.totalLaunches");
            TxtUniqueProgramsLabel.Text = App.Localization.Get("stats.uniquePrograms");
            TxtMostUsed.Text = App.Localization.Get("stats.mostUsed");
            
            if (TxtMostUsedDescription != null)
                TxtMostUsedDescription.Text = App.Localization.Get("stats.mostUsedDescription");

            if (TxtStatisticsTitle != null)
                TxtStatisticsTitle.Text = App.Localization.Get("stats.title");
            if (TxtStatisticsSubtitle != null)
                TxtStatisticsSubtitle.Text = App.Localization.Get("stats.subtitle");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateLocalizedText Error: {ex}");
        }
    }

    private void LoadStatistics()
    {
        try
        {
            if (App.Data?.Data == null)
            {
                return;
            }

            var presets = App.Data.Data.Presets ?? new();
            var usageStats = App.Data.Data.UsageStats ?? new();

            if (TxtPresetsCount != null)
                TxtPresetsCount.Text = presets.Count.ToString();
            
            var totalLaunches = presets.Sum(p => p.LaunchCount);
            if (TxtTotalLaunches != null)
                TxtTotalLaunches.Text = totalLaunches.ToString();
            
            if (TxtUniquePrograms != null)
                TxtUniquePrograms.Text = usageStats.Count.ToString();

            if (usageStats.Count == 0)
            {
                if (MostUsedList != null)
                    MostUsedList.ItemsSource = new List<UsageStat>();
                return;
            }

            var timesSuffix = App.Localization.Get("stats.times.suffix") ?? "fois";
            var mostUsedList = usageStats
                .OrderByDescending(x => x.Value)
                .Take(10)
                .ToList();

            if (!mostUsedList.Any())
            {
                if (MostUsedList != null)
                    MostUsedList.ItemsSource = new List<UsageStat>();
                return;
            }

            var maxCount = mostUsedList.First().Value;
            if (maxCount <= 0) maxCount = 1;

            var mostUsed = mostUsedList
                .Select(x => new UsageStat 
                { 
                    Path = x.Key ?? "Unknown", 
                    Count = x.Value, 
                    TimesText = $"{x.Value} {timesSuffix}",
                    Percentage = (double)x.Value / maxCount * 100,
                    PercentageText = $"{(double)x.Value / maxCount * 100:F0}%"
                })
                .ToList();

            if (MostUsedList != null)
                MostUsedList.ItemsSource = mostUsed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadStatistics Error: {ex}");
        }
    }
}
