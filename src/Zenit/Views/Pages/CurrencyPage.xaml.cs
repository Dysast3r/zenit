using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Zenit.Services;

namespace Zenit.Views.Pages;

public partial class CurrencyPage : UserControl, IIslandPage
{
    private static readonly Brush Up = Frozen(Color.FromRgb(0x34, 0xD3, 0x99));
    private static readonly Brush Down = Frozen(Color.FromRgb(0xF8, 0x71, 0x71));
    private static readonly Brush Muted = Frozen(Color.FromArgb(0x8C, 255, 255, 255));
    private static readonly Brush TileBackground = Frozen(Color.FromArgb(0x14, 255, 255, 255));

    private readonly CurrencyService _currency;
    private CurrencyReport? _shown;

    public CurrencyPage(CurrencyService currency)
    {
        _currency = currency;
        InitializeComponent();
        _currency.Updated += Refresh;
        Refresh();
    }

    public PageKind Kind => PageKind.Currency;

    public void Refresh()
    {
        var report = _currency.Report;
        if (report is null || report.Quotes.Count == 0 || _currency.IsStale)
        {
            // Sin datos, o con los de otra moneda local: no se muestran valores viejos.
            StatusText.Text = _currency.Failed ? Strings.Offline : Strings.Loading;
            StatusText.Visibility = Visibility.Visible;
            HeaderText.Text = DateText.Text = "";
            Tiles.Children.Clear();
            _shown = null;
            return;
        }
        if (ReferenceEquals(report, _shown)) return;
        _shown = report;
        StatusText.Visibility = Visibility.Collapsed;

        var local = report.Local.ToUpperInvariant();
        HeaderText.Text = Strings.F("Tipo de cambio · {0}", "Exchange rates · {0}", local);
        DateText.Text = report.Date.ToString("d MMM", CultureInfo.CurrentCulture).Replace(".", "");

        Tiles.Children.Clear();
        foreach (var quote in report.Quotes) Tiles.Children.Add(BuildTile(quote, report.Local));
    }

    private static Border BuildTile(CurrencyQuote quote, string local)
    {
        var code = quote.Code.ToUpperInvariant();

        var badge = new Grid { Width = 26, Height = 26 };
        badge.Children.Add(new Border { CornerRadius = new CornerRadius(13), Background = BadgeBrush(quote.Code) });
        badge.Children.Add(new TextBlock
        {
            Text = CurrencyService.SymbolOf(quote.Code),
            FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        });

        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(badge);
        header.Children.Add(new TextBlock
        {
            Text = code, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0),
        });

        // El valor se encoge si no cabe (p. ej. BTC en pesos chilenos).
        var value = new Viewbox
        {
            StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 12, 0, 0), Height = 26,
            Child = new TextBlock
            {
                Text = CurrencyService.FormatValue(quote.Value),
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
            },
        };

        var change = new TextBlock { FontSize = 11.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) };
        if (quote.ChangePercent is { } pct)
        {
            change.Text = $"{(pct >= 0 ? "▲" : "▼")} {Math.Abs(pct).ToString("0.00", CultureInfo.CurrentCulture)}%";
            change.Foreground = Math.Abs(pct) < 0.005 ? Muted : pct > 0 ? Up : Down;
        }
        else
        {
            change.Text = "—";
            change.Foreground = Muted;
        }

        var unit = new TextBlock
        {
            Text = Strings.F("1 {0} en {1}", "1 {0} in {1}", code, local.ToUpperInvariant()),
            FontSize = 10.5, Foreground = Muted, Margin = new Thickness(0, 2, 0, 0),
        };

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(value);
        stack.Children.Add(unit);
        stack.Children.Add(change);

        return new Border
        {
            CornerRadius = new CornerRadius(16), Background = TileBackground,
            Padding = new Thickness(12, 11, 12, 10), Margin = new Thickness(5, 0, 5, 0), Child = stack,
        };
    }

    /// <summary>Degradado único por moneda, derivado de su código.</summary>
    private static Brush BadgeBrush(string code)
    {
        var hue = (uint)code.GetHashCode(StringComparison.Ordinal) % 360;
        if (code == "btc") hue = 30;
        if (code == "eur") hue = 220;
        if (code == "usd") hue = 145;
        var brush = new LinearGradientBrush(Hsv(hue, 0.65, 0.95), Hsv((hue + 40) % 360, 0.75, 0.7), 45);
        brush.Freeze();
        return brush;
    }

    private static Color Hsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0), 1 => (x, c, 0.0), 2 => (0.0, c, x), 3 => (0.0, x, c), 4 => (x, 0.0, c), _ => (c, 0.0, x),
        };
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
