using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Zenit.Controls;
using Zenit.Services;

namespace Zenit.Views.Pages;

public partial class WorldClockPage : UserControl, IIslandPage
{
    private readonly AppSettings _settings;
    private readonly List<(TimeZoneInfo zone, AnalogClock clock, TextBlock time, TextBlock relative)> _items = [];
    private string _signature = "";

    public WorldClockPage(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        Refresh();
    }

    public PageKind Kind => PageKind.WorldClock;

    public void Refresh()
    {
        var signature = string.Join('|', _settings.WorldClocks.Select(w => w.TimeZoneId + w.Label)) + _settings.Use24Hour;
        if (signature != _signature) Rebuild(signature);

        var now = DateTime.UtcNow;
        var localNow = DateTime.Now;
        foreach (var (zone, clock, time, relative) in _items)
        {
            var there = TimeZoneInfo.ConvertTimeFromUtc(now, zone);
            clock.Time = there;
            time.Text = TimeFormats.Time(there, _settings, seconds: false);

            var hours = (there - localNow).TotalHours;
            var offset = Math.Round(hours * 2) / 2;
            var day = (there.Date - localNow.Date).Days switch
            {
                0 => Strings.T("Hoy", "Today"),
                1 => Strings.T("Mañana", "Tomorrow"),
                -1 => Strings.T("Ayer", "Yesterday"),
                _ => there.ToString("ddd"),
            };
            relative.Text = offset == 0 ? day : $"{day}, {(offset > 0 ? "+" : "−")}{Math.Abs(offset):0.#} h";
        }
    }

    private void Rebuild(string signature)
    {
        _signature = signature;
        _items.Clear();
        Clocks.Children.Clear();

        foreach (var entry in _settings.WorldClocks.Take(4))
        {
            TimeZoneInfo zone;
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(entry.TimeZoneId);
            }
            catch
            {
                continue;
            }

            var clock = new AnalogClock { Width = 62, Height = 62, HorizontalAlignment = HorizontalAlignment.Center };
            var label = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(entry.Label) ? zone.StandardName : entry.Label,
                FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(4, 10, 4, 0),
            };
            var time = new TextBlock
            {
                FontSize = 12.5, Foreground = new SolidColorBrush(Color.FromArgb(0xE6, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var relative = new TextBlock
            {
                FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(0x80, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0),
            };

            var column = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            column.Children.Add(clock);
            column.Children.Add(label);
            column.Children.Add(time);
            column.Children.Add(relative);
            Clocks.Children.Add(column);
            _items.Add((zone, clock, time, relative));
        }

        EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
