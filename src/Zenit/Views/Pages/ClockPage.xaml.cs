using System.Windows;
using System.Windows.Controls;
using Zenit.Services;

namespace Zenit.Views.Pages;

public partial class ClockPage : UserControl, IIslandPage
{
    private readonly AppSettings _settings;

    public ClockPage(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        Refresh();
    }

    public PageKind Kind => PageKind.Clock;

    public void Refresh()
    {
        var now = DateTime.Now;
        var full = TimeFormats.Time(now, _settings, seconds: false);
        TimeRun.Text = full;
        SecondsRun.Text = _settings.ShowSeconds ? $":{now:ss}" : "";
        DateText.Text = TimeFormats.Date(now, _settings);
        DayInfoText.Text = TimeFormats.DayInfo(now);
        DayInfoText.Visibility = _settings.ShowWeekNumber ? Visibility.Visible : Visibility.Collapsed;
        DayFill.Width = 200 * now.TimeOfDay.TotalHours / 24;
    }
}
