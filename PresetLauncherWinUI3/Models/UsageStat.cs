namespace PresetLauncher.Models;

public class UsageStat
{
    public string Path { get; set; } = string.Empty;
    public int Count { get; set; }
    public string TimesText { get; set; } = string.Empty;
    public double Percentage { get; set; }
    public string PercentageText { get; set; } = string.Empty;
}
