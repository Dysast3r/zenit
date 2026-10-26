using System.Windows;
using System.Windows.Media;

namespace Zenit.Controls;

/// <summary>Anillo de progreso como el del temporizador de iOS.</summary>
public sealed class ArcProgress : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(ArcProgress),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(ArcProgress),
        new FrameworkPropertyMetadata(Brushes.Orange, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(ArcProgress),
        new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush Track = Freeze(new SolidColorBrush(Color.FromArgb(0x2A, 255, 255, 255)));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        var r = (size - Thickness) / 2;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);

        dc.DrawEllipse(null, new Pen(Track, Thickness), c, r, r);

        var p = Math.Clamp(Progress, 0, 1);
        if (p <= 0) return;
        var pen = new Pen(Stroke, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (p >= 0.9999)
        {
            dc.DrawEllipse(null, pen, c, r, r);
            return;
        }

        var angle = p * 2 * Math.PI;
        var start = new Point(c.X, c.Y - r);
        var end = new Point(c.X + r * Math.Sin(angle), c.Y - r * Math.Cos(angle));
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(start, false, false);
            g.ArcTo(end, new Size(r, r), 0, p > 0.5, SweepDirection.Clockwise, true, false);
        }
        dc.DrawGeometry(null, pen, geometry);
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
