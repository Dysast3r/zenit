using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zenit.Services;

public enum IslandStyle { Notch, Floating }
public enum IdleContent { Nothing, Time, Date, TimeAndWeather, Currency, Battery }
public enum CompactCenter { Nothing, Time, Date, Temperature, Track }
public enum PageKind { Music, Clock, Weather, Currency, WorldClock, Lyrics, Timer, Widgets, Volume }
public enum DateStyle { Long, Full, Medium, Short, Iso, Custom }
public enum AccentMode { Artwork, Custom }
public enum TemperatureUnit { Celsius, Fahrenheit }

public sealed class PageEntry
{
    public PageKind Kind { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class SavedCity
{
    public string Name { get; set; } = "";
    public string Country { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public sealed class WorldClockEntry
{
    public string TimeZoneId { get; set; } = "";
    public string Label { get; set; } = "";
}

public sealed class AppSettings
{
    public static readonly string Folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zenit");

    private static readonly string FilePath = Path.Combine(Folder, "settings.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new LenientEnumConverter() },
    };

    /// <summary>
    /// Enums como texto. Un nombre desconocido (p. ej. una página de una versión más nueva) queda como
    /// valor no definido en vez de invalidar todo el archivo; <see cref="Normalize"/> lo corrige.
    /// </summary>
    private sealed class LenientEnumConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type type) => type.IsEnum;

        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
            => (JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(type))!;

        private sealed class Converter<T> : JsonConverter<T> where T : struct, Enum
        {
            public override bool HandleNull => true;

            public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String && Enum.TryParse<T>(reader.GetString(), ignoreCase: true, out var value) && Enum.IsDefined(value))
                    return value;
                if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number))
                    return (T)Enum.ToObject(typeof(T), number);
                reader.Skip();
                return (T)Enum.ToObject(typeof(T), -1);
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
                => writer.WriteStringValue(value.ToString());
        }
    }

    /// <summary>Se dispara después de cada cambio guardado; la isla se reconfigura en vivo.</summary>
    public event Action? Changed;

    // General
    public AppLanguage Language { get; set; } = AppLanguage.System;
    public IslandStyle Style { get; set; } = IslandStyle.Notch;
    public bool ExpandOnHover { get; set; } = true;
    public bool HideWhenIdle { get; set; }
    public bool HideInFullscreen { get; set; } = true;
    public bool HideFromCapture { get; set; } = true;
    public double Scale { get; set; } = 1.0;
    public double TopOffset { get; set; }
    public AccentMode AccentMode { get; set; } = AccentMode.Artwork;
    public string CustomAccent { get; set; } = "#0A84FF";
    public IdleContent Idle { get; set; } = IdleContent.TimeAndWeather;

    /// <summary>Pantalla de la isla: "" = la principal, "*" = todas, o el nombre del dispositivo (p. ej. DISPLAY2).</summary>
    public string Monitor { get; set; } = "";

    // Páginas del carrusel, en orden
    public List<PageEntry> Pages { get; set; } = DefaultPages();

    // Música
    // Conserva el nombre del ajuste de cuando solo se reconocía Spotify.
    [JsonPropertyName("SpotifyOnly")]
    public bool MusicAppsOnly { get; set; }
    public bool ShowTrackPeek { get; set; } = true;
    public double PeekSeconds { get; set; } = 3;
    public bool ReactiveEqualizer { get; set; } = true;
    public CompactCenter CompactCenter { get; set; } = CompactCenter.Time;

    // Reloj y fecha
    public bool Use24Hour { get; set; } = true;
    public bool ShowSeconds { get; set; }
    public DateStyle DateStyle { get; set; } = DateStyle.Long;
    public string CustomDateFormat { get; set; } = "dddd d 'de' MMMM, yyyy";
    public bool ShowWeekNumber { get; set; } = true;
    public List<WorldClockEntry> WorldClocks { get; set; } =
    [
        new() { TimeZoneId = "Eastern Standard Time", Label = "Nueva York" },
        new() { TimeZoneId = "Romance Standard Time", Label = "Madrid" },
        new() { TimeZoneId = "Tokyo Standard Time", Label = "Tokio" },
    ];

    // Clima
    public bool UseCurrentLocation { get; set; } = true;
    public List<SavedCity> Cities { get; set; } = [];
    public TemperatureUnit Unit { get; set; } = TemperatureUnit.Celsius;
    public int WeatherRefreshMinutes { get; set; } = 15;

    // Monedas
    public string LocalCurrency { get; set; } = DefaultLocalCurrency();
    public List<string> Currencies { get; set; } = [];
    public int CurrencyRefreshMinutes { get; set; } = 60;

    // Sistema
    public bool ShowNotifications { get; set; } = true;
    public bool VolumeHud { get; set; } = true;
    public bool WheelControlsVolume { get; set; } = true;
    public bool BatteryAlerts { get; set; } = true;
    public bool PrivacyIndicators { get; set; } = true;
    public bool TimerLiveActivity { get; set; } = true;
    public bool RespectDoNotDisturb { get; set; } = true;

    // Calendario (enlaces iCal privados de Google Calendar u Outlook)
    public List<string> CalendarUrls { get; set; } = [];
    public bool CalendarLiveActivity { get; set; } = true;
    public int CalendarLeadMinutes { get; set; } = 5;

    // Actualizaciones
    public bool AutoCheckUpdates { get; set; } = true;
    public DateTime LastUpdateCheck { get; set; }

    /// <summary>Orden inicial del carrusel. Los widgets empiezan apagados hasta que el usuario instale alguno.</summary>
    private static readonly PageKind[] DefaultOrder =
        [PageKind.Music, PageKind.Lyrics, PageKind.Clock, PageKind.Timer, PageKind.Weather, PageKind.Currency, PageKind.WorldClock, PageKind.Volume, PageKind.Widgets];

    public static List<PageEntry> DefaultPages() =>
        DefaultOrder.Select(k => new PageEntry { Kind = k, Enabled = k != PageKind.Widgets }).ToList();

    private static string DefaultLocalCurrency()
    {
        try
        {
            return new RegionInfo(CultureInfo.CurrentCulture.Name).ISOCurrencySymbol.ToLowerInvariant();
        }
        catch
        {
            return "usd";
        }
    }

    public static AppSettings Load()
    {
        MigrateLegacyFolder();
        AppSettings settings;
        try
        {
            settings = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new AppSettings()
                : new AppSettings();
            settings.Normalize();
        }
        catch
        {
            // Archivo dañado: se guarda una copia para poder revisarlo y se vuelve a los valores por defecto.
            try
            {
                File.Copy(FilePath, Path.Combine(Folder, "settings.bad.json"), overwrite: true);
            }
            catch
            {
                // Sin copia: no impide arrancar.
            }
            settings = new AppSettings();
            settings.Normalize();
        }
        return settings;
    }

    /// <summary>La app se llamó "Isla Dinámica" antes de 1.0: conserva los ajustes de esa versión.</summary>
    private static void MigrateLegacyFolder()
    {
        try
        {
            var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IslaDinamica");
            if (Directory.Exists(legacy) && !Directory.Exists(Folder)) Directory.Move(legacy, Folder);
        }
        catch
        {
            // Si no se puede mover, se empieza con valores por defecto.
        }
    }

    /// <summary>Corrige valores fuera de rango y añade páginas nuevas de versiones futuras.</summary>
    private void Normalize()
    {
        // Un JSON editado a mano puede traer null en listas, entradas o textos.
        Pages = Pages?.Where(p => p is not null && Enum.IsDefined(p.Kind)).DistinctBy(p => p.Kind).ToList() ?? DefaultPages();
        WorldClocks = WorldClocks?.Where(c => c is not null).ToList() ?? [];
        foreach (var clock in WorldClocks)
        {
            clock.TimeZoneId ??= "";
            clock.Label ??= "";
        }
        Cities = Cities?.Where(c => c is not null).ToList() ?? [];
        foreach (var city in Cities)
        {
            city.Name ??= "";
            city.Country ??= "";
        }
        Currencies = Currencies?.Where(c => !string.IsNullOrWhiteSpace(c)).ToList() ?? [];
        CalendarUrls = CalendarUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct().ToList() ?? [];
        Monitor ??= "";
        CustomAccent ??= "#0A84FF";
        CustomDateFormat ??= "dddd d 'de' MMMM, yyyy";

        if (!Enum.IsDefined(Language)) Language = AppLanguage.System;
        if (!Enum.IsDefined(Style)) Style = IslandStyle.Notch;
        if (!Enum.IsDefined(AccentMode)) AccentMode = AccentMode.Artwork;
        if (!Enum.IsDefined(Idle)) Idle = IdleContent.TimeAndWeather;
        if (!Enum.IsDefined(CompactCenter)) CompactCenter = CompactCenter.Time;
        if (!Enum.IsDefined(DateStyle)) DateStyle = DateStyle.Long;
        if (!Enum.IsDefined(Unit)) Unit = TemperatureUnit.Celsius;

        foreach (var kind in Enum.GetValues<PageKind>())
            if (Pages.All(p => p.Kind != kind)) Pages.Add(new PageEntry { Kind = kind, Enabled = kind != PageKind.Widgets });

        Scale = Math.Clamp(Scale, 0.6, 1.6);
        TopOffset = Math.Clamp(TopOffset, 0, 80);
        PeekSeconds = Math.Clamp(PeekSeconds, 1, 10);
        WeatherRefreshMinutes = Math.Clamp(WeatherRefreshMinutes, 5, 180);
        CurrencyRefreshMinutes = Math.Clamp(CurrencyRefreshMinutes, 15, 1440);
        CalendarLeadMinutes = Math.Clamp(CalendarLeadMinutes, 1, 30);
        LocalCurrency = string.IsNullOrWhiteSpace(LocalCurrency) ? "usd" : LocalCurrency.ToLowerInvariant();

        if (Currencies.Count == 0)
        {
            Currencies = LocalCurrency == "usd" ? ["eur", "gbp", "jpy", "btc"] : ["usd", "eur", "btc"];
        }
        Currencies = Currencies.Select(c => c.ToLowerInvariant()).Where(c => c != LocalCurrency).Distinct().Take(4).ToList();
    }

    /// <summary>Copia todos los valores de otra instancia (la isla y el panel comparten esta).</summary>
    public void CopyFrom(AppSettings other)
    {
        foreach (var property in typeof(AppSettings).GetProperties())
            if (property.CanWrite) property.SetValue(this, property.GetValue(other));
    }

    public void Save()
    {
        Normalize();
        try
        {
            Directory.CreateDirectory(Folder);
            // Se escribe aparte y se reemplaza de una vez: un corte a medio guardar no deja el archivo truncado.
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch
        {
            // No es crítico: los ajustes siguen activos en esta sesión.
        }
        Changed?.Invoke();
    }
}
