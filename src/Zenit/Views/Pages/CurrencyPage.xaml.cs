using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Zenit.Controls;
using Zenit.Services;

namespace Zenit.Views.Pages;

public partial class CurrencyPage : UserControl, IIslandPage
{
    private static readonly Brush Up = Frozen(Color.FromRgb(0x34, 0xD3, 0x99));
    private static readonly Brush Down = Frozen(Color.FromRgb(0xF8, 0x71, 0x71));
    private static readonly Brush Muted = IslandTheme.InkAlpha(0x8C);
    private static readonly Brush TileBackground = IslandTheme.InkAlpha(0x14);

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

        // Hasta 4 caben en una fila con todo el detalle; con más se usan dos filas de fichas compactas.
        var compact = report.Quotes.Count > 4;
        Tiles.Rows = compact ? 2 : 1;
        Tiles.Margin = compact ? new Thickness(18, 40, 18, 17) : new Thickness(18, 46, 18, 28);
        Tiles.Children.Clear();
        foreach (var quote in report.Quotes)
            Tiles.Children.Add(compact ? BuildCompactTile(quote) : BuildTile(quote, report.Local));
    }

    private static Border BuildCompactTile(CurrencyQuote quote)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(Badge(quote.Code, 18, 9));
        header.Children.Add(new TextBlock
        {
            Text = quote.Code.ToUpperInvariant(), FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = IslandTheme.Ink,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0),
        });

        var value = new Viewbox
        {
            StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0), Height = 18,
            Child = new TextBlock
            {
                Text = CurrencyService.FormatValue(quote.Value),
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = IslandTheme.Ink,
            },
        };

        var change = ChangeText(quote, 10);
        change.Margin = new Thickness(0, 1, 0, 0);

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(header);
        stack.Children.Add(value);
        stack.Children.Add(change);

        return new Border
        {
            CornerRadius = new CornerRadius(12), Background = TileBackground,
            Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(4, 3, 4, 3), Child = stack,
        };
    }

    private static Grid Badge(string code, double size, double fontSize)
    {
        var badge = new Grid { Width = size, Height = size };
        badge.Children.Add(new Border { CornerRadius = new CornerRadius(size / 2), Background = BadgeBrush(code) });
        badge.Children.Add(new TextBlock
        {
            Text = CurrencyService.SymbolOf(code),
            FontSize = fontSize, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        });
        return badge;
    }

    private static TextBlock ChangeText(CurrencyQuote quote, double fontSize)
    {
        var change = new TextBlock { FontSize = fontSize, FontWeight = FontWeights.SemiBold };
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
        return change;
    }

    private static Border BuildTile(CurrencyQuote quote, string local)
    {
        var code = quote.Code.ToUpperInvariant();

        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(Badge(quote.Code, 26, 12));
        header.Children.Add(new TextBlock
        {
            Text = code, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = IslandTheme.Ink,
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
                FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = IslandTheme.Ink,
            },
        };

        var change = ChangeText(quote, 11.5);
        change.Margin = new Thickness(0, 4, 0, 0);

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
        if (code == "uf") hue = 0;
        if (code == "utm") hue = 270;
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
