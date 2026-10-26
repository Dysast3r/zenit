using System.Windows;
using System.Windows.Media;

namespace Zenit.Controls;

/// <summary>Esfera analógica como la del reloj mundial de iOS: clara de día y oscura de noche.</summary>
public sealed class AnalogClock : FrameworkElement
{
    private static readonly Brush DayFace = Freeze(new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF5)));
    private static readonly Brush NightFace = Freeze(new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x30)));
    private static readonly Brush SecondBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A)));

    private DateTime _time;

    public DateTime Time
    {
        get => _time;
        set
        {
            if (value == _time) return;
            _time = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        var night = _time.Hour is < 6 or >= 19;
        var ink = night ? Brushes.White : Brushes.Black;
        var r = size / 2;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);

        dc.DrawEllipse(night ? NightFace : DayFace, null, c, r, r);

        var tick = new Pen(ink, Math.Max(1, size * 0.025)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        tick.Freeze();
        for (var i = 0; i < 12; i++)
        {
            var a = i * Math.PI / 6;
            var inner = i % 3 == 0 ? 0.74 : 0.8;
            dc.PushOpacity(i % 3 == 0 ? 0.9 : 0.45);
            dc.DrawLine(tick, At(c, a, r * inner), At(c, a, r * 0.88));
            dc.Pop();
        }

        var minutes = _time.Minute + _time.Second / 60.0;
        var hours = _time.Hour % 12 + minutes / 60.0;
        Hand(dc, ink, c, hours / 12 * Math.PI * 2, r * 0.48, size * 0.06);
        Hand(dc, ink, c, minutes / 60 * Math.PI * 2, r * 0.72, size * 0.045);
        Hand(dc, SecondBrush, c, _time.Second / 60.0 * Math.PI * 2, r * 0.78, size * 0.02);
        dc.DrawEllipse(SecondBrush, null, c, size * 0.045, size * 0.045);
    }

    private static void Hand(DrawingContext dc, Brush brush, Point c, double angle, double length, double thickness)
    {
        var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(pen, At(c, angle + Math.PI, length * 0.15), At(c, angle, length));
    }

    /// <summary>Punto a un ángulo medido en sentido horario desde las 12.</summary>
    private static Point At(Point c, double angle, double distance)
        => new(c.X + Math.Sin(angle) * distance, c.Y - Math.Cos(angle) * distance);

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
