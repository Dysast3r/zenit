using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Zenit.Controls;
using Zenit.Services;

namespace Zenit.Views.Pages;

public partial class WeatherPage : UserControl, IIslandPage
{
    private const int HourColumns = 7;

    private readonly WeatherService _weather;
    private readonly AppSettings _settings;
    private readonly List<(TextBlock hour, WeatherGlyph glyph, TextBlock temp)> _hours = [];
    private int _index;

    public WeatherPage(WeatherService weather, AppSettings settings)
    {
        _weather = weather;
        _settings = settings;
        InitializeComponent();

        for (var i = 0; i < HourColumns; i++)
        {
            var hour = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = IslandTheme.InkAlpha(0x99), HorizontalAlignment = HorizontalAlignment.Center };
            var glyph = new WeatherGlyph { Width = 26, Height = 26, Margin = new Thickness(0, 5, 0, 4) };
            var temp = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = IslandTheme.Ink, HorizontalAlignment = HorizontalAlignment.Center };
            var column = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            column.Children.Add(hour);
            column.Children.Add(glyph);
            column.Children.Add(temp);
            HoursGrid.Children.Add(column);
            _hours.Add((hour, glyph, temp));
        }

        // Varias ubicaciones: un clic en el nombre pasa a la siguiente.
        PlaceButton.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            if (_weather.Reports.Count < 2) return;
            _index = (_index + 1) % _weather.Reports.Count;
            Refresh();
            var fade = new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(350));
            MainBlock.BeginAnimation(OpacityProperty, fade);
            HoursGrid.BeginAnimation(OpacityProperty, fade);
        };

        _weather.Updated += Refresh;
        Refresh();
    }

    public PageKind Kind => PageKind.Weather;

    public void Refresh()
    {
        var reports = _weather.Reports;
        if (reports.Count == 0)
        {
            StatusText.Text = _weather.IsLoading || _weather.LastUpdated == default ? Strings.Loading : Strings.Offline;
            StatusText.Visibility = Visibility.Visible;
            MainBlock.Visibility = HoursGrid.Visibility = Visibility.Hidden;
            return;
        }

        StatusText.Visibility = Visibility.Collapsed;
        MainBlock.Visibility = HoursGrid.Visibility = Visibility.Visible;
        _index = Math.Clamp(_index, 0, reports.Count - 1);
        var r = reports[_index];

        MainGlyph.Kind = r.Kind;
        MainGlyph.IsDay = r.IsDay;
        TempText.Text = TimeFormats.Temperature(r.Temperature);
        PlaceText.Text = r.Place;
        PinIcon.Visibility = r.IsCurrentLocation ? Visibility.Visible : Visibility.Collapsed;
        PlaceCounter.Text = reports.Count > 1 ? $"{_index + 1}/{reports.Count}" : "";
        ConditionText.Text = Strings.F("{0} · Máx. {1} Mín. {2}", "{0} · H:{1} L:{2}",
            r.Description, TimeFormats.Temperature(r.Max), TimeFormats.Temperature(r.Min));
        DetailText.Text = Strings.F("Sensación {0} · Humedad {1:0}% · {2:0} {3}", "Feels {0} · Humidity {1:0}% · {2:0} {3}",
            TimeFormats.Temperature(r.FeelsLike), r.Humidity, r.Wind, r.WindUnit);
        AmbientStop.Color = AmbientFor(r.Kind, r.IsDay);

        for (var i = 0; i < _hours.Count; i++)
        {
            var (hour, glyph, temp) = _hours[i];
            if (i >= r.Hours.Count)
            {
                hour.Text = temp.Text = "";
                glyph.Visibility = Visibility.Hidden;
                continue;
            }
            var h = r.Hours[i];
            hour.Text = i == 0 ? Strings.T("Ahora", "Now") : TimeFormats.Hour(h.Time, _settings);
            glyph.Visibility = Visibility.Visible;
            glyph.Kind = i == 0 ? r.Kind : h.Kind;
            glyph.IsDay = i == 0 ? r.IsDay : h.IsDay;
            temp.Text = TimeFormats.Temperature(i == 0 ? r.Temperature : h.Temperature);
        }
    }

    public void Animate(double time)
    {
        if (MainBlock.Visibility != Visibility.Visible) return;
        MainGlyph.Time = time;
        MainGlyph.InvalidateVisual();
        foreach (var (_, glyph, _) in _hours)
        {
            glyph.Time = time;
            glyph.InvalidateVisual();
        }
    }

    private static Color AmbientFor(WeatherKind kind, bool day) => kind switch
    {
        WeatherKind.Clear or WeatherKind.PartlyCloudy when day => Color.FromArgb(0x55, 0xFF, 0x9F, 0x1C),
        WeatherKind.Clear or WeatherKind.PartlyCloudy => Color.FromArgb(0x50, 0x5B, 0x5F, 0xE8),
        WeatherKind.Rain or WeatherKind.Drizzle => Color.FromArgb(0x55, 0x2F, 0x80, 0xED),
        WeatherKind.Storm => Color.FromArgb(0x55, 0x8B, 0x5C, 0xF6),
        WeatherKind.Snow => Color.FromArgb(0x45, 0xB8, 0xE1, 0xFF),
        _ => Color.FromArgb(0x40, 0x94, 0xA3, 0xB8),
    };
}
