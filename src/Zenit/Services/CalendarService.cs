namespace Zenit.Services;

/// <summary>
/// Próximas reuniones desde los enlaces iCal privados de Google Calendar u Outlook.
/// No pide cuentas ni permisos: el usuario pega la "dirección secreta en formato iCal" de su calendario.
/// </summary>
public sealed class CalendarService(AppSettings settings)
{
    /// <summary>Un calendario de años con muchos eventos repetidos pesa unos pocos MB; más que esto se descarta.</summary>
    private const long MaxBytes = 8 * 1024 * 1024;

    private CancellationTokenSource? _refresh;

    /// <summary>Eventos con hora de las próximas 48 horas, ordenados por inicio.</summary>
    public IReadOnlyList<CalendarEvent> Events { get; private set; } = [];

    /// <summary>Calendarios que no se pudieron leer en la última actualización.</summary>
    public int FailedSources { get; private set; }

    public DateTime LastUpdated { get; private set; }

    /// <summary>Se dispara en el hilo de la UI cuando hay datos nuevos.</summary>
    public event Action? Updated;

    public async Task RefreshAsync()
    {
        _refresh?.Cancel();
        var cts = _refresh = new CancellationTokenSource();
        var urls = settings.CalendarUrls.ToList();
        var now = DateTime.Now;

        var results = await Task.WhenAll(urls.Select(async url =>
        {
            try
            {
                var text = await Net.GetTextAsync(NormalizeUrl(url), MaxBytes, cts.Token, untrusted: true);
                if (text is null || !text.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase)) return null;
                // El análisis puede tardar con calendarios grandes: fuera del hilo de la UI.
                return await Task.Run(() => IcsCalendar.Parse(text, now.AddHours(-12), now.AddHours(48)), cts.Token);
            }
            catch
            {
                // Enlace inválido, sin conexión o actualización cancelada: ese calendario se omite.
                return null;
            }
        }));
        if (cts.IsCancellationRequested) return;

        Events = results.OfType<List<CalendarEvent>>().SelectMany(e => e).OrderBy(e => e.Start).ToList();
        FailedSources = results.Count(r => r is null);
        LastUpdated = DateTime.Now;
        Updated?.Invoke();
    }

    /// <summary>
    /// La reunión que empieza dentro de <paramref name="lead"/> o que empezó hace menos de un minuto.
    /// Los eventos que ya estaban en curso al llegar a ese punto no cuentan.
    /// </summary>
    public CalendarEvent? Upcoming(DateTime now, TimeSpan lead)
        => Events.FirstOrDefault(e => e.Start - lead <= now && now < e.Start.AddMinutes(1));

#if DEBUG
    /// <summary>Solo desarrollo: eventos de ejemplo para el modo demo, sin descargar nada.</summary>
    public void UseDemoEvents(IEnumerable<CalendarEvent> events)
    {
        Events = events.OrderBy(e => e.Start).ToList();
        LastUpdated = DateTime.Now;
        Updated?.Invoke();
    }
#endif

    /// <summary>Google y Outlook también entregan el enlace como webcal://; se descarga igual por HTTPS.</summary>
    public static string NormalizeUrl(string url)
    {
        url = url.Trim();
        return url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase) ? "https://" + url["webcal://".Length..] : url;
    }
}
