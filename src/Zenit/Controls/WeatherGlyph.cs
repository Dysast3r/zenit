using System.Windows;
using System.Windows.Media;
using Zenit.Services;

namespace Zenit.Controls;

/// <summary>Ícono de clima vectorial y animado. Se dibuja en un espacio unitario 1×1 escalado al tamaño.</summary>
public sealed class WeatherGlyph : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(WeatherKind), typeof(WeatherGlyph),
        new FrameworkPropertyMetadata(WeatherKind.Clear, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDayProperty = DependencyProperty.Register(
        nameof(IsDay), typeof(bool), typeof(WeatherGlyph),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public WeatherKind Kind
    {
        get => (WeatherKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public bool IsDay
    {
        get => (bool)GetValue(IsDayProperty);
        set => SetValue(IsDayProperty, value);
    }

    /// <summary>Segundos de animación; los actualiza el bucle de la isla mientras la página está visible.</summary>
    public double Time { get; set; }

    private static readonly Brush SunBrush = Freeze(new RadialGradientBrush(Color.FromRgb(0xFF, 0xE0, 0x6B), Color.FromRgb(0xFF, 0x9F, 0x1C)));
    private static readonly Pen RayPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x3D)), 0.055) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
    // Con el tema claro, las nubes, la nieve y la luna se oscurecen un poco para no perderse en el fondo.
    private static readonly Brush MoonBrush = Freeze(IslandTheme.IsLight
        ? new LinearGradientBrush(Color.FromRgb(0xC9, 0xCF, 0xE8), Color.FromRgb(0x8E, 0x98, 0xBE), 45)
        : new LinearGradientBrush(Color.FromRgb(0xF4, 0xF1, 0xE8), Color.FromRgb(0xC9, 0xCF, 0xE8), 45));
    private static readonly Brush CloudBrush = Freeze(IslandTheme.IsLight
        ? new LinearGradientBrush(Color.FromRgb(0xDD, 0xE3, 0xEC), Color.FromRgb(0x9C, 0xA8, 0xBA), 90)
        : new LinearGradientBrush(Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0xC7, 0xD0, 0xDD), 90));
    private static readonly Brush DarkCloudBrush = Freeze(new LinearGradientBrush(Color.FromRgb(0xB4, 0xBC, 0xC8), Color.FromRgb(0x6B, 0x74, 0x82), 90));
    private static readonly Pen RainPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(0x5A, 0xB4, 0xFF)), 0.05) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
    private static readonly Pen FogPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(0xDD, 0xD5, 0xDB, 0xE5)), 0.07) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
    private static readonly Brush SnowBrush = Freeze(new SolidColorBrush(IslandTheme.IsLight ? Color.FromRgb(0x8F, 0xA7, 0xC4) : Colors.White));
    private static readonly Brush BoltBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x3F)));
    private static readonly Brush StarBrush = Freeze(new SolidColorBrush(IslandTheme.IsLight ? Color.FromRgb(0x8E, 0x98, 0xBE) : Color.FromRgb(0xF4, 0xF1, 0xE8)));

    private static readonly Geometry Cloud = Freeze(new GeometryGroup
    {
        FillRule = FillRule.Nonzero,
        Children =
        {
            new EllipseGeometry(new Point(0.36, 0.58), 0.17, 0.17),
            new EllipseGeometry(new Point(0.56, 0.48), 0.22, 0.22),
            new EllipseGeometry(new Point(0.74, 0.61), 0.14, 0.14),
            new RectangleGeometry(new Rect(0.2, 0.58, 0.68, 0.17), 0.085, 0.085),
        },
    });

    private static readonly Geometry Moon = Freeze(new CombinedGeometry(GeometryCombineMode.Exclude,
        new EllipseGeometry(new Point(0.5, 0.5), 0.27, 0.27),
        new EllipseGeometry(new Point(0.64, 0.38), 0.23, 0.23)));

    private static readonly Geometry Bolt = Freeze(Geometry.Parse("M 0.55,0.6 L 0.42,0.82 L 0.52,0.82 L 0.46,1.0 L 0.66,0.74 L 0.55,0.74 L 0.62,0.6 Z"));

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        dc.PushTransform(new TranslateTransform((ActualWidth - size) / 2, (ActualHeight - size) / 2));
        dc.PushTransform(new ScaleTransform(size, size));
        var t = Time;

        switch (Kind)
        {
            case WeatherKind.Clear:
                DrawSky(dc, t, 1.0, 0, 0);
                break;
            case WeatherKind.PartlyCloudy:
                DrawSky(dc, t, 0.72, -0.13, -0.14);
                dc.PushTransform(new TranslateTransform(0.06 + Math.Sin(t * 0.8) * 0.015, 0.06));
                dc.DrawGeometry(CloudBrush, null, Cloud);
                dc.Pop();
                break;
            case WeatherKind.Cloudy:
                dc.PushTransform(new TranslateTransform(-0.08 + Math.Sin(t * 0.6) * 0.02, -0.14));
                dc.PushTransform(new ScaleTransform(0.75, 0.75, 0.5, 0.5));
                dc.DrawGeometry(DarkCloudBrush, null, Cloud);
                dc.Pop();
                dc.Pop();
                dc.PushTransform(new TranslateTransform(0.04 + Math.Sin(t * 0.8 + 1) * 0.02, 0.02));
                dc.DrawGeometry(CloudBrush, null, Cloud);
                dc.Pop();
                break;
            case WeatherKind.Fog:
                for (var i = 0; i < 4; i++)
                {
                    var y = 0.3 + i * 0.14;
                    var shift = Math.Sin(t * 0.9 + i * 1.3) * 0.06;
                    var inset = i % 2 == 0 ? 0.15 : 0.25;
                    dc.DrawLine(FogPen, new Point(inset + shift, y), new Point(1 - inset + shift, y));
                }
                break;
            case WeatherKind.Drizzle:
            case WeatherKind.Rain:
                DrawPrecipitation(dc, t, Kind == WeatherKind.Rain ? 4 : 3, snow: false);
                break;
            case WeatherKind.Snow:
                DrawPrecipitation(dc, t, 4, snow: true);
                break;
            case WeatherKind.Storm:
                // Destello breve cada ~3 segundos.
                var phase = t % 3.2;
                var flash = phase < 0.12 || phase is > 0.22 and < 0.3;
                if (flash) dc.PushOpacity(1); else dc.PushOpacity(0.55);
                dc.DrawGeometry(BoltBrush, null, Bolt);
                dc.Pop();
                dc.PushTransform(new TranslateTransform(0, -0.16));
                dc.DrawGeometry(DarkCloudBrush, null, Cloud);
                dc.Pop();
                break;
        }

        dc.Pop();
        dc.Pop();
    }

    private void DrawSky(DrawingContext dc, double t, double scale, double dx, double dy)
    {
        dc.PushTransform(new TranslateTransform(dx, dy));
        dc.PushTransform(new ScaleTransform(scale, scale, 0.5, 0.5));
        if (IsDay)
        {
            dc.PushTransform(new RotateTransform(t * 18 % 360, 0.5, 0.5));
            for (var i = 0; i < 8; i++)
            {
                var a = i * Math.PI / 4;
                var pulse = 0.015 * Math.Sin(t * 2.2 + i);
                dc.DrawLine(RayPen,
                    new Point(0.5 + Math.Cos(a) * 0.3, 0.5 + Math.Sin(a) * 0.3),
                    new Point(0.5 + Math.Cos(a) * (0.41 + pulse), 0.5 + Math.Sin(a) * (0.41 + pulse)));
            }
            dc.Pop();
            dc.DrawEllipse(SunBrush, null, new Point(0.5, 0.5), 0.2, 0.2);
        }
        else
        {
            dc.DrawGeometry(MoonBrush, null, Moon);
            for (var i = 0; i < 3; i++)
            {
                var twinkle = 0.5 + 0.5 * Math.Sin(t * 2.5 + i * 2.1);
                dc.PushOpacity(0.35 + 0.65 * twinkle);
                dc.DrawEllipse(StarBrush, null, new Point(0.78 - i * 0.07, 0.2 + i * 0.13), 0.022, 0.022);
                dc.Pop();
            }
        }
        dc.Pop();
        dc.Pop();
    }

    private static void DrawPrecipitation(DrawingContext dc, double t, int count, bool snow)
    {
        for (var i = 0; i < count; i++)
        {
            var x = 0.3 + i * (0.42 / Math.Max(1, count - 1));
            var cycle = (t * (snow ? 0.45 : 1.3) + i * 0.37) % 1.0;
            var y = 0.62 + cycle * 0.34;
            dc.PushOpacity(1 - cycle);
            if (snow) dc.DrawEllipse(SnowBrush, null, new Point(x + Math.Sin(t * 2 + i) * 0.03, y), 0.035, 0.035);
            else dc.DrawLine(RainPen, new Point(x, y), new Point(x - 0.035, y + 0.1));
            dc.Pop();
        }
        dc.PushTransform(new TranslateTransform(0, -0.16));
        dc.DrawGeometry(CloudBrush, null, Cloud);
        dc.Pop();
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
