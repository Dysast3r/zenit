using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Zenit.Services;

namespace Zenit.Views.Pages;

/// <summary>Hasta cuatro widgets de la comunidad como tarjetas.</summary>
public partial class WidgetsPage : UserControl, IIslandPage
{
    private static readonly Brush TileBackground = Frozen(Color.FromArgb(0x14, 255, 255, 255));
    private static readonly Brush Muted = Frozen(Color.FromArgb(0x8C, 255, 255, 255));

    private readonly WidgetService _widgets;
    private bool _dirty = true;

    public WidgetsPage(WidgetService widgets)
    {
        _widgets = widgets;
        InitializeComponent();
        _widgets.Updated += () =>
        {
            _dirty = true;
            Refresh();
        };
        Refresh();
    }

    public PageKind Kind => PageKind.Widgets;

    public void Refresh()
    {
        // La isla llama a Refresh cada segundo; las tarjetas solo se rehacen cuando llegan datos nuevos.
        if (!_dirty) return;
        _dirty = false;

        Tiles.Children.Clear();
        foreach (var widget in _widgets.Widgets.Take(4)) Tiles.Children.Add(BuildTile(widget));
        EmptyText.Visibility = Tiles.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Border BuildTile(WidgetState widget)
    {
        var accent = ParseColor(widget.Definition.Accent);
        var badge = new Grid { Width = 26, Height = 26 };
        badge.Children.Add(new Border { CornerRadius = new CornerRadius(13), Background = new SolidColorBrush(accent) });
        badge.Children.Add(new TextBlock
        {
            Text = widget.Definition.Icon, FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        });

        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(badge);
        header.Children.Add(new TextBlock
        {
            Text = widget.Definition.Name, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 64,
        });

        var stack = new StackPanel();
        stack.Children.Add(header);

        if (widget.Values.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = widget.Error ?? Strings.Loading, FontSize = 11.5, Foreground = Muted,
                Margin = new Thickness(0, 14, 0, 0), TextWrapping = TextWrapping.Wrap,
            });
        }
        else
        {
            var main = widget.Values[0];
            stack.Children.Add(new Viewbox
            {
                StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 10, 0, 0), Height = 26,
                Child = new TextBlock
                {
                    Text = main.Text, FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                    FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
                },
            });
            stack.Children.Add(new TextBlock { Text = main.Label, FontSize = 10.5, Foreground = Muted });
            foreach (var extra in widget.Values.Skip(1).Take(2))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"{extra.Label}  {extra.Text}", FontSize = 11, Foreground = Brushes.White,
                    Margin = new Thickness(0, 3, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis,
                });
            }
        }

        return new Border
        {
            CornerRadius = new CornerRadius(16), Background = TileBackground,
            Padding = new Thickness(12, 11, 12, 10), Margin = new Thickness(5, 0, 5, 0), Child = stack,
        };
    }

    private static Color ParseColor(string text)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(text);
        }
        catch
        {
            return Color.FromRgb(0xEC, 0x48, 0x99);
        }
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
