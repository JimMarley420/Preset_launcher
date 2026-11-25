using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static PresetLauncher.Views.DesktopUi;

namespace PresetLauncher.Views.Pages;

public sealed class StatisticsPage : DesktopPage
{
    private readonly Grid _metrics = new() { ColumnSpacing = 16, RowSpacing = 6 };
    private readonly StackPanel _ranking = new() { Spacing = 4 };
    private readonly TextBlock _title;
    private readonly TextBlock[] _labels = new TextBlock[3];
    private readonly TextBlock[] _counts = new TextBlock[3];
    private int _columns;

    public StatisticsPage()
    {
        _title = Heading("stats.title", page: true);
        Header.Children.Add(_title);
        Body.Children.Add(_metrics);
        Body.Children.Add(_ranking);
        for (int index = 0; index < _counts.Length; index++)
        {
            var cell = new StackPanel { Spacing = 4 };
            _labels[index] = Text("", "DesktopSecondaryText");
            _counts[index] = Text("", "DesktopMetric");
            cell.Children.Add(_labels[index]);
            cell.Children.Add(_counts[index]);
            _metrics.Children.Add(Row(cell));
        }
        _metrics.SizeChanged += (_, args) => ArrangeMetrics(args.NewSize.Width);
        ArrangeMetrics(0);
    }

    private void ArrangeMetrics(double width)
    {
        var columns = width >= 590 ? 3 : width >= 370 ? 2 : 1;
        if (_columns == columns)
            return;
        _columns = columns;
        _metrics.ColumnDefinitions.Clear();
        _metrics.RowDefinitions.Clear();
        for (int i = 0; i < columns; i++)
            _metrics.ColumnDefinitions.Add(new ColumnDefinition());
        for (int i = 0; i < (3 + columns - 1) / columns; i++)
            _metrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < 3; i++)
        {
            Grid.SetRow((FrameworkElement)_metrics.Children[i], i / columns);
            Grid.SetColumn((FrameworkElement)_metrics.Children[i], i % columns);
        }
    }

    internal override void Refresh()
    {
        base.Refresh();
        _title.Text = T("stats.title");
        var data = App.Data.Data;
        var culture = App.Localization.Culture;
        _labels[0].Text = T("stats.presetsCount");
        _labels[1].Text = T("stats.totalLaunches");
        _labels[2].Text = T("stats.uniquePrograms");
        _counts[0].Text = data.Presets.Count.ToString("N0", culture);
        _counts[1].Text = data.Presets.Sum(preset => (long)preset.LaunchCount).ToString("N0", culture);
        _counts[2].Text = data.UsageStats.Count.ToString("N0", culture);
        _ranking.Children.Clear();
        _ranking.Children.Add(Heading("stats.mostUsed"));

        var ranking = data.UsageStats.OrderByDescending(entry => entry.Value).Take(10).ToArray();
        if (ranking.Length == 0)
        {
            _ranking.Children.Add(Text(T("stats.empty"), "DesktopSecondaryText"));
            return;
        }
        foreach (var entry in ranking)
        {
            var line = new Grid { ColumnSpacing = 10 };
            line.ColumnDefinitions.Add(new ColumnDefinition());
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Place(line, PathText(entry.Key), 0, 0);
            Place(line, Text(T(entry.Value == 1 ? "stats.once" : "stats.times", entry.Value), "DesktopSecondaryText"), 0, 1);
            _ranking.Children.Add(Row(line));
        }
    }
}
