using System.Windows.Threading;

namespace Zenit.Services;

/// <summary>Todos los servicios de la app, creados una vez y compartidos por la isla y la configuración.</summary>
public sealed class ZenitServices(AppSettings settings, Dispatcher dispatcher) : IDisposable
{
    public AppSettings Settings { get; } = settings;
    public MediaService Media { get; } = new() { SpotifyOnly = settings.SpotifyOnly };
    public WeatherService Weather { get; } = new(settings);
    public CurrencyService Currency { get; } = new(settings);
    public VolumeService Volume { get; } = new(dispatcher);
    public SystemIndicators Indicators { get; } = new(dispatcher);
    public TimerService Timer { get; } = new(dispatcher);
    public LyricsService Lyrics { get; } = new();
    public NotificationService Notifications { get; } = new();
    public WidgetService Widgets { get; } = new(dispatcher);
    public CalendarService Calendar { get; } = new(settings);

    public void Dispose()
    {
        Notifications.Stop();
        Indicators.Dispose();
        Volume.Dispose();
        Media.Dispose();
    }
}
