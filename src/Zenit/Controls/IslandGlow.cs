using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Zenit.Services;

namespace Zenit.Controls;

/// <summary>
/// Resplandor de color alrededor de la isla, dibujado como capas de contorno cada vez más anchas y tenues.
/// Reemplaza a DropShadowEffect: ese efecto se recorta en rectángulos (y parpadea) cuando la forma cambia
/// de tamaño en cada frame dentro de una ventana transparente; las capas no tienen ese problema y cuestan menos.
/// Puede ser de un color o un degradado de dos, y animarse: pulso, giro alrededor del borde o arcoíris.
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

    public static readonly DependencyProperty SecondColorProperty = DependencyProperty.Register(
        nameof(SecondColor), typeof(Color), typeof(IslandGlow),
        new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Degradado de <see cref="Color"/> a <see cref="SecondColor"/> en lugar de un solo color.</summary>
    public static readonly DependencyProperty GradientProperty = DependencyProperty.Register(
        nameof(Gradient), typeof(bool), typeof(IslandGlow),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AnimationProperty = DependencyProperty.Register(
        nameof(Animation), typeof(GlowAnimation), typeof(IslandGlow),
        new FrameworkPropertyMetadata(GlowAnimation.None, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((IslandGlow)d).RestartPhase()));

    /// <summary>Avance 0..1 de la animación, que se repite sin fin.</summary>
    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(double), typeof(IslandGlow),
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

    public Color SecondColor
    {
        get => (Color)GetValue(SecondColorProperty);
        set => SetValue(SecondColorProperty, value);
    }

    public bool Gradient
    {
        get => (bool)GetValue(GradientProperty);
        set => SetValue(GradientProperty, value);
    }

    public GlowAnimation Animation
    {
        get => (GlowAnimation)GetValue(AnimationProperty);
        set => SetValue(AnimationProperty, value);
    }

    public double Phase
    {
        get => (double)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    /// <summary>La animación corre a 30 fps: basta para que se vea fluida y redibuja solo el resplandor.</summary>
    private void RestartPhase()
    {
        if (Animation == GlowAnimation.None)
        {
            BeginAnimation(PhaseProperty, null);
            return;
        }
        var seconds = Animation switch
        {
            GlowAnimation.Pulse => 2.6,
            GlowAnimation.Spin => 3.2,
            _ => 8.0,
        };
        var cycle = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(seconds)) { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(cycle, 30);
        BeginAnimation(PhaseProperty, cycle);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var shape = Shape;
        var intensity = Math.Clamp(Intensity, 0, 1);
        if (shape is null || intensity < 0.005) return;

        var animation = Animation;
        var phase = Phase;
        // Pulso: el brillo respira entre el 55 % y el 100 %.
        if (animation == GlowAnimation.Pulse) intensity *= 0.55 + 0.45 * (0.5 + 0.5 * Math.Cos(phase * 2 * Math.PI));

        // Todas las capas se superponen en el borde: con esta opacidad por capa, ahí suman intensity / 2,
        // lo mismo que una sombra difuminada. Hacia afuera quedan menos capas y el color se desvanece.
        var layerOpacity = 1 - Math.Pow(1 - intensity / 2, 1.0 / Layers);
        var brush = BuildBrush(animation, phase);
        brush.Opacity = layerOpacity;
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

    private Brush BuildBrush(GlowAnimation animation, double phase)
    {
        var first = Color;
        var second = SecondColor;
        var gradient = Gradient;
        if (animation == GlowAnimation.Rainbow)
        {
            first = Hue(phase * 360);
            second = Hue(phase * 360 + 120);
            gradient = true;
        }
        else if (animation == GlowAnimation.Spin && !gradient)
        {
            // Un solo color que gira: una cola que se apaga recorre el borde, como un cometa.
            second = Color.FromArgb(0x28, first.R, first.G, first.B);
            gradient = true;
        }
        if (!gradient) return new SolidColorBrush(first);

        var angle = animation is GlowAnimation.Spin or GlowAnimation.Rainbow ? phase * 2 * Math.PI : 0;
        var (dx, dy) = (Math.Cos(angle) / 2, Math.Sin(angle) / 2);
        return new LinearGradientBrush(first, second, new Point(0.5 - dx, 0.5 - dy), new Point(0.5 + dx, 0.5 + dy));
    }

    /// <summary>Color saturado y brillante del tono indicado (0..360).</summary>
    private static Color Hue(double hue)
    {
        hue = (hue % 360 + 360) % 360;
        var x = 1 - Math.Abs(hue / 60 % 2 - 1);
        var (r, g, b) = (int)(hue / 60) switch
        {
            0 => (1.0, x, 0.0), 1 => (x, 1.0, 0.0), 2 => (0.0, 1.0, x), 3 => (0.0, x, 1.0), 4 => (x, 0.0, 1.0), _ => (1.0, 0.0, x),
        };
        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}
