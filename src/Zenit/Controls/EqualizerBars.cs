using System.Windows;
using System.Windows.Media;

namespace Zenit.Controls;

/// <summary>Barras del ecualizador dibujadas directamente (sin árbol visual) para que cueste poco por frame.</summary>
public sealed class EqualizerBars : FrameworkElement
{
    public const int BarCount = 4;

    /// <summary>Niveles 0..1 compartidos por todas las instancias; los actualiza el bucle de animación.</summary>
    public static readonly double[] Levels = new double[BarCount];

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(EqualizerBars),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var gap = w * 0.16;
        var barWidth = (w - gap * (BarCount - 1)) / BarCount;
        var radius = barWidth / 2;

        for (var i = 0; i < BarCount; i++)
        {
            var barHeight = Math.Max(barWidth, Levels[i] * h);
            var rect = new Rect(i * (barWidth + gap), (h - barHeight) / 2, barWidth, barHeight);
            dc.DrawRoundedRectangle(BarBrush, null, rect, radius, radius);
        }
    }
}
