using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Zenit.Services;

namespace Zenit.Controls;

/// <summary>
/// Colores de la isla: negra con tinta blanca (oscuro) o clara con tinta casi negra (claro).
/// Los colores de acento (naranja, verde, rojo, azul) y la carátula no cambian con el tema.
/// Se aplica una vez al iniciar, antes de crear las ventanas: cambiar de tema reinicia la app.
/// </summary>
public static class IslandTheme
{
    /// <summary>Transparencias de la tinta que usa el XAML como {DynamicResource InkXX}.</summary>
    private static readonly byte[] Alphas = [0x14, 0x1A, 0x26, 0x2A, 0x33, 0x40, 0x4D, 0x66, 0x73, 0x80, 0x8C, 0x99, 0xA3, 0xCC, 0xE6];

    private static readonly Dictionary<byte, Brush> InkCache = [];

    public static bool IsLight { get; private set; }

    /// <summary>Color del texto y los íconos.</summary>
    public static Color InkColor => IsLight ? Color.FromRgb(0x1C, 0x1C, 0x1E) : Colors.White;

    /// <summary>Relleno de la isla.</summary>
    public static Color BackgroundColor => IsLight ? Color.FromRgb(0xF5, 0xF5, 0xF7) : Colors.Black;

    public static Brush Ink => InkAlpha(0xFF);

    public static Brush Background { get; private set; } = Brushes.Black;

    /// <summary>La tinta con transparencia (0x8C ≈ 55 %), para textos secundarios, pistas y fondos de fichas.</summary>
    public static Brush InkAlpha(byte alpha)
    {
        if (InkCache.TryGetValue(alpha, out var brush)) return brush;
        var color = InkColor;
        brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();
        InkCache[alpha] = brush;
        return brush;
    }

    public static void Apply(IslandColorScheme scheme, ResourceDictionary resources)
    {
        IsLight = scheme == IslandColorScheme.Light || (scheme == IslandColorScheme.System && WindowsUsesLightTheme());
        InkCache.Clear();
        var background = new SolidColorBrush(BackgroundColor);
        background.Freeze();
        Background = background;

        resources["IslandBackground"] = Background;
        resources["Ink"] = Ink;
        foreach (var alpha in Alphas) resources[$"Ink{alpha:X2}"] = InkAlpha(alpha);
        // Sombra sobre la carátula de fondo de la página de música, para que el texto se lea.
        var shade = BackgroundColor;
        resources["Shade33"] = Color.FromArgb(0x33, shade.R, shade.G, shade.B);
        resources["ShadeB3"] = Color.FromArgb(0xB3, shade.R, shade.G, shade.B);
    }

    /// <summary>"Modo de las aplicaciones" de Windows (Configuración → Personalización → Colores).</summary>
    private static bool WindowsUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }
}
