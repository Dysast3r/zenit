using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Zenit.Animation;
using Zenit.Controls;
using Zenit.Interop;
using Zenit.Services;
using Zenit.Views.Pages;
using Microsoft.Win32;

namespace Zenit.Views;

/// <summary>Solo en Debug: estados de ejemplo y el recorrido que se graba para el GIF del README.</summary>
public partial class IslandWindow
{
#if DEBUG
    /// <summary>Solo desarrollo: muestra un estado con datos de ejemplo (Zenit.exe --demo expanded).</summary>
    public void ShowDemo(string mode)
    {
        _demo = true;
        if (_hwnd != 0) NativeMethods.ExcludeFromCapture(_hwnd, false);
        _media.Changed -= OnMediaChangedFromService;

        if (mode == "meeting")
        {
            // Reunión que empieza en 4 minutos: aviso y cuenta regresiva en la isla compacta.
            var start = DateTime.Now.AddMinutes(4).AddSeconds(-1);
            _services.Calendar.UseDemoEvents([new CalendarEvent("Revisión de diseño · Zenit", start, start.AddMinutes(30))]);
            return;
        }

        if (mode == "tour")
        {
            _ = RunTourAsync();
            return;
        }

        // "marquee": título largo para ver la marquesina del centro de la isla compacta.
        var title = mode == "marquee" ? "Midnight Drive Through Neon Avenue (Extended Mix)" : "Blinding Lights";
        if (mode != "idle") OnMediaChanged(DemoSong(title, "The Weeknd", Color.FromRgb(0xF9, 0x73, 0x16), Color.FromRgb(0xDB, 0x27, 0x77), 200));
        if (mode == "peek")
        {
            _peekUntil = DateTime.MaxValue;
            _peekTimer.Stop();
        }
        if (mode == "compact-lyrics")
        {
            // Solo en memoria: la isla compacta muestra la letra si la última página fue Letras.
            _settings.Pages.First(p => p.Kind == PageKind.Lyrics).Enabled = true;
            RebuildPages();
            LoadLyricsIfNeeded();
        }
        if (mode == "widgets")
        {
            // Solo en memoria: no se guarda en los ajustes del usuario.
            _settings.Pages.First(p => p.Kind == PageKind.Widgets).Enabled = true;
            RebuildPages();
            _services.Widgets.InstallExamples();
        }
        if (mode == "expanded-volume")
        {
            // Volumen simulado (no cambia el del sistema) con la isla expandida: la barra reemplaza a los puntos.
            RequestExpand(true);
            _ = Task.Delay(1200).ContinueWith(_ => Dispatcher.Invoke(() => OnVolumeChanged(0.42f, false)));
            return;
        }
                if (mode == "volume")
        {
            OnVolumeChanged(0.64f, false);
            _peekUntil = DateTime.MaxValue;
            _peekTimer.Stop();
            return;
        }
        if (mode == "live")
        {
            _timer.SetDuration(TimeSpan.FromMinutes(25));
            _timer.Toggle();
            return;
        }
        if (mode == "timer")
        {
            _timer.SetDuration(TimeSpan.FromMinutes(5));
            _timer.Toggle();
        }

        var kind = mode switch
        {
            "clock" => PageKind.Clock,
            "weather" => PageKind.Weather,
            "currency" => PageKind.Currency,
            "world" => PageKind.WorldClock,
            "timer" => PageKind.Timer,
            "lyrics" or "compact-lyrics" => PageKind.Lyrics,
            "widgets" => PageKind.Widgets,
            "mixer" => PageKind.Volume,
            _ => PageKind.Music,
        };
        var index = _pages.IndexOf(_allPages[kind]);
        if (index >= 0) GoToPage(index, 0);
        RequestExpand(mode is not ("idle" or "compact" or "peek" or "marquee" or "compact-lyrics"));
    }

    private static MediaSnapshot DemoSong(string title, string artist, Color from, Color to, int seconds = 214)
    {
        var art = new RenderTargetBitmap(256, 256, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(from, to, 45), null, new Rect(0, 0, 256, 256));
            dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(0x90, 255, 255, 255), Colors.Transparent), null, new Point(70, 60), 120, 120);
        }
        art.Render(visual);
        art.Freeze();
        return new MediaSnapshot("Spotify.exe", title, artist, art, ColorExtractor.Extract(art), true,
            TimeSpan.FromSeconds(71), TimeSpan.FromSeconds(seconds), DateTimeOffset.Now, true, true, true);
    }

    /// <summary>Letra inventada para el recorrido: cubre la posición de las canciones de ejemplo (desde 1:11).</summary>
    private static IEnumerable<LyricLine> DemoLyrics()
    {
        string[] lines =
        [
            "Neon lights across the avenue", "Midnight drive, just me and you", "The city hums a quiet tune",
            "We're chasing echoes of the moon", "Hold the moment, don't let go", "Every heartbeat soft and slow",
        ];
        for (var i = 0; i < 40; i++)
            yield return new LyricLine(TimeSpan.FromSeconds(66 + i * 2.6), lines[i % lines.Length]);
    }

    /// <summary>Recorrido automático para grabar el GIF del README sobre un fondo neutro.</summary>
    private async Task RunTourAsync()
    {
        var area = SystemParameters.WorkArea;
        var backdrop = new Window
        {
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, ShowActivated = false,
            Width = 900, Height = 420, Left = area.Left + (area.Width - 900) / 2, Top = area.Top,
            Background = new LinearGradientBrush(
                [new GradientStop(Color.FromRgb(0x0A, 0x17, 0x30), 0), new GradientStop(Color.FromRgb(0x17, 0x46, 0xD8), 0.6), new GradientStop(Color.FromRgb(0x22, 0xD3, 0xEE), 1)],
                new Point(0, 0), new Point(1, 1)),
        };
        // El fondo va siempre encima del escritorio (así nunca se graba lo que hay detrás) y la isla, sobre él.
        backdrop.Topmost = true;
        backdrop.Show();
        Topmost = false;
        Topmost = true;

        // Solo en memoria: la página de letras con una letra de ejemplo, sin consultar LRCLIB.
        _settings.Pages.First(p => p.Kind == PageKind.Lyrics).Enabled = true;
        RebuildPages();
        _services.Lyrics.UseDemoLines(DemoLyrics());

        async Task Wait(double seconds) => await Task.Delay(TimeSpan.FromSeconds(seconds));
        int PageOf(PageKind kind) => _pages.IndexOf(_allPages[kind]);

        await Wait(1.6);
        OnMediaChanged(DemoSong("Midnight Drive", "Neon Avenue", Color.FromRgb(0xF9, 0x73, 0x16), Color.FromRgb(0xDB, 0x27, 0x77)));
        await Wait(3.4);
        RequestExpand(true);
        await Wait(2.2);
        OnMediaChanged(DemoSong("Ocean Lights", "Polar Sky", Color.FromRgb(0x22, 0xD3, 0xEE), Color.FromRgb(0x63, 0x66, 0xF1)));
        await Wait(1.8);
        // Letras: la página, y al cerrar la isla la compacta sigue mostrando la línea.
        if (PageOf(PageKind.Lyrics) >= 0) GoToPage(PageOf(PageKind.Lyrics), -900);
        await Wait(2.6);
        RequestExpand(false);
        await Wait(3.6);
        RequestExpand(true);
        await Wait(0.6);
        // Las páginas desactivadas en los ajustes se saltan sin esperar.
        foreach (var kind in new[] { PageKind.Weather, PageKind.Currency, PageKind.WorldClock })
        {
            if (PageOf(kind) < 0) continue;
            GoToPage(PageOf(kind), -900);
            await Wait(1.7);
        }
        GoToPage(0, 1500);
        await Wait(1.0);
        // Volumen con la isla expandida: la barra reemplaza un momento a los puntos de página.
        for (var level = 0.46f; level <= 0.72f; level += 0.04f)
        {
            OnVolumeChanged(level, false);
            await Wait(0.09);
        }
        await Wait(1.6);
        RequestExpand(false);
        await Wait(1.4);
        _timer.SetDuration(TimeSpan.FromMinutes(25));
        _timer.Toggle();
        await Wait(3);
        // Fin del recorrido: la app se cierra y así tools/record-demo.py sabe cuándo dejar de grabar.
        Application.Current.Shutdown();
    }
#endif
}
