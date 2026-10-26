using System.Windows;
using System.Windows.Media;

namespace Zenit.Controls;

/// <summary>Contorno de la isla: notch pegado al borde (iPhone 13) o píldora flotante (iPhone 14 Pro).</summary>
public static class IslandShape
{
    /// <summary>Radio de las curvas cóncavas que unen el notch con el borde de la pantalla.</summary>
    public const double NotchEar = 10;

    /// <param name="ear">Margen reservado a cada lado para las "orejas" (0 en modo flotante).</param>
    public static Geometry Build(double w, double h, double radius, double ear)
    {
        if (ear <= 0)
        {
            var r = Math.Min(radius, Math.Min(w, h) / 2);
            return new RectangleGeometry(new Rect(0, 0, w, h), r, r);
        }

        // Solo las esquinas inferiores van redondeadas; las superiores se funden con el borde.
        var bottom = Math.Min(radius, Math.Min(w / 2, h));
        var e = Math.Clamp(h - bottom, 0, ear);
        double left = ear, right = ear + w;

        var geometry = new StreamGeometry();
        using (var c = geometry.Open())
        {
            c.BeginFigure(new Point(left - e, 0), true, true);
            c.LineTo(new Point(right + e, 0), true, false);
            if (e > 0) c.ArcTo(new Point(right, e), new Size(e, e), 0, false, SweepDirection.Counterclockwise, true, false);
            c.LineTo(new Point(right, h - bottom), true, false);
            c.ArcTo(new Point(right - bottom, h), new Size(bottom, bottom), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(left + bottom, h), true, false);
            c.ArcTo(new Point(left, h - bottom), new Size(bottom, bottom), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(left, e), true, false);
            if (e > 0) c.ArcTo(new Point(left - e, 0), new Size(e, e), 0, false, SweepDirection.Counterclockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }
}
