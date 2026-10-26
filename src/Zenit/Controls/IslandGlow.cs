using System.Windows;
using System.Windows.Media;

namespace Zenit.Controls;

/// <summary>
/// Resplandor de color alrededor de la isla, dibujado como capas de contorno cada vez más anchas y tenues.
/// Reemplaza a DropShadowEffect: ese efecto se recorta en rectángulos (y parpadea) cuando la forma cambia
/// de tamaño en cada frame dentro de una ventana transparente; las capas no tienen ese problema y cuestan menos.
/// </summary>
public sealed class IslandGlow : FrameworkElement
{
    private const int Layers = 12;

    /// <summary>Hasta dónde llega el resplandor fuera del borde, en unidades lógicas.</summary>
    private const double Reach = 30;

    public static readonly DependencyProperty ShapeProperty = DependencyProperty.Register(
        nameof(Shape), typeof(Geometry), typeof(IslandGlow),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Color), typeof(IslandGlow),
        new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Intensidad 0..1: la opacidad del resplandor justo en el borde de la isla es la mitad de este valor.</summary>
    public static readonly DependencyProperty IntensityProperty = DependencyProperty.Register(
        nameof(Intensity), typeof(double), typeof(IslandGlow),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public Geometry? Shape
    {
        get => (Geometry?)GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public double Intensity
    {
        get => (double)GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var shape = Shape;
        var intensity = Math.Clamp(Intensity, 0, 1);
        if (shape is null || intensity < 0.005) return;

        // Todas las capas se superponen en el borde: con esta opacidad por capa, ahí suman intensity / 2,
        // lo mismo que una sombra difuminada. Hacia afuera quedan menos capas y el color se desvanece.
        var layerOpacity = 1 - Math.Pow(1 - intensity / 2, 1.0 / Layers);
        var brush = new SolidColorBrush(Color) { Opacity = layerOpacity };
        brush.Freeze();

        for (var i = Layers; i >= 1; i--)
        {
            // Capas más juntas cerca del borde: el brillo cae rápido y deja una cola suave, como un desenfoque.
            var spread = Reach * Math.Pow((double)i / Layers, 1.6);
            var pen = new Pen(brush, 2 * spread) { LineJoin = PenLineJoin.Round };
            pen.Freeze();
            dc.DrawGeometry(null, pen, shape);
        }
    }
}
