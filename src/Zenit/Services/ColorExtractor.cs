using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Zenit.Services;

/// <summary>Obtiene el color más vivo y dominante de una carátula para teñir la isla.</summary>
public static class ColorExtractor
{
    private static readonly Color Neutral = Color.FromRgb(0xE8, 0xE8, 0xF0);

    public static Color Extract(BitmapSource source)
    {
        try
        {
            const int sample = 24;
            var scaled = new TransformedBitmap(source,
                new ScaleTransform((double)sample / source.PixelWidth, (double)sample / source.PixelHeight));
            var bgra = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);

            int w = bgra.PixelWidth, h = bgra.PixelHeight;
            var pixels = new byte[w * h * 4];
            bgra.CopyPixels(pixels, w * 4, 0);

            // 12 cubetas de tono; cada píxel pesa más cuanto más saturado y luminoso.
            var weight = new double[12];
            var r = new double[12];
            var g = new double[12];
            var b = new double[12];

            for (var i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] < 128) continue;
                double bb = pixels[i] / 255.0, gg = pixels[i + 1] / 255.0, rr = pixels[i + 2] / 255.0;
                var (hue, sat, val) = ToHsv(rr, gg, bb);
                if (val < 0.15 || sat < 0.15) continue;

                var bin = (int)(hue / 30) % 12;
                var wt = sat * sat * val;
                weight[bin] += wt;
                r[bin] += rr * wt;
                g[bin] += gg * wt;
                b[bin] += bb * wt;
            }

            var best = Array.IndexOf(weight, weight.Max());
            if (weight[best] < 0.8) return Neutral;

            var (hh, ss, _) = ToHsv(r[best] / weight[best], g[best] / weight[best], b[best] / weight[best]);
            return FromHsv(hh, Math.Clamp(ss, 0.4, 0.85), 1.0);
        }
        catch
        {
            return Neutral;
        }
    }

    private static (double h, double s, double v) ToHsv(double r, double g, double b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        double h = 0;
        if (delta > 0)
        {
            if (max == r) h = 60 * ((g - b) / delta % 6);
            else if (max == g) h = 60 * ((b - r) / delta + 2);
            else h = 60 * ((r - g) / delta + 4);
        }
        if (h < 0) h += 360;
        return (h, max == 0 ? 0 : delta / max, max);
    }

    private static Color FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}
