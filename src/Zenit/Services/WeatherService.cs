using System.Globalization;
using System.Text.Json;

namespace Zenit.Services;

public enum WeatherKind { Clear, PartlyCloudy, Cloudy, Fog, Drizzle, Rain, Snow, Storm }

public sealed record WeatherHour(DateTime Time, double Temperature, WeatherKind Kind, bool IsDay);

public sealed record WeatherReport(
    string Place,
    bool IsCurrentLocation,
    double Temperature,
    double FeelsLike,
    WeatherKind Kind,
    string Description,
    bool IsDay,
    double Humidity,
    double Wind,
    string WindUnit,
    double Max,
    double Min,
    IReadOnlyList<WeatherHour> Hours);

/// <summary>Clima actual y próximas horas desde Open-Meteo (gratis, sin clave, sin cuenta).</summary>
public sealed class WeatherService
{
    private readonly AppSettings _settings;
    private CancellationTokenSource? _refresh;

    public WeatherService(AppSettings settings) => _settings = settings;

    public IReadOnlyList<WeatherReport> Reports { get; private set; } = [];
    public DateTime LastUpdated { get; private set; }
    public bool IsLoading { get; private set; }

    /// <summary>Se dispara en el hilo de la UI cuando hay datos nuevos.</summary>
    public event Action? Updated;

    public async Task RefreshAsync()
    {
        _refresh?.Cancel();
        var cts = _refresh = new CancellationTokenSource();
        IsLoading = true;
        try
        {
            var places = new List<(Place place, bool current)>();
            if (_settings.UseCurrentLocation && await LocationService.GetCurrentAsync() is { } here)
                places.Add((here, true));
            places.AddRange(_settings.Cities.Select(c => (new Place(c.Name, c.Latitude, c.Longitude), false)));

            var reports = await Task.WhenAll(places.Select(p => FetchAsync(p.place, p.current, cts.Token)));
            if (cts.IsCancellationRequested) return;

            Reports = reports.OfType<WeatherReport>().ToList();
            LastUpdated = DateTime.Now;
        }
        finally
        {
            if (_refresh == cts) IsLoading = false;
        }
        Updated?.Invoke();
    }

    private async Task<WeatherReport?> FetchAsync(Place place, bool current, CancellationToken cancellation)
    {
        var fahrenheit = _settings.Unit == TemperatureUnit.Fahrenheit;
        var lat = place.Latitude.ToString("0.####", CultureInfo.InvariantCulture);
        var lon = place.Longitude.ToString("0.####", CultureInfo.InvariantCulture);
        var url =
            $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
            "&current=temperature_2m,apparent_temperature,weather_code,is_day,wind_speed_10m,relative_humidity_2m" +
            "&hourly=temperature_2m,weather_code,is_day&forecast_hours=7" +
            "&daily=temperature_2m_max,temperature_2m_min&forecast_days=1&timezone=auto" +
            (fahrenheit ? "&temperature_unit=fahrenheit&wind_speed_unit=mph" : "");

        using var json = await Net.GetJsonAsync(url, cancellation);
        if (json is null) return null;

        try
        {
            var root = json.RootElement;
            var now = root.GetProperty("current");
            var code = (int)(now.Number("weather_code") ?? 0);
            var isDay = (now.Number("is_day") ?? 1) > 0;

            var hours = new List<WeatherHour>();
            if (root.TryGetProperty("hourly", out var hourly))
            {
                var times = hourly.GetProperty("time").EnumerateArray().ToArray();
                var temps = hourly.GetProperty("temperature_2m").EnumerateArray().ToArray();
                var codes = hourly.GetProperty("weather_code").EnumerateArray().ToArray();
                var days = hourly.GetProperty("is_day").EnumerateArray().ToArray();
                for (var i = 0; i < times.Length && i < temps.Length; i++)
                {
                    if (temps[i].ValueKind != JsonValueKind.Number) continue;
                    hours.Add(new WeatherHour(
                        DateTime.Parse(times[i].GetString()!, CultureInfo.InvariantCulture),
                        temps[i].GetDouble(),
                        KindOf(codes[i].ValueKind == JsonValueKind.Number ? codes[i].GetInt32() : 0),
                        days[i].ValueKind != JsonValueKind.Number || days[i].GetInt32() > 0));
                }
            }

            var daily = root.GetProperty("daily");
            return new WeatherReport(
                place.Name,
                current,
                now.Number("temperature_2m") ?? 0,
                now.Number("apparent_temperature") ?? 0,
                KindOf(code),
                Describe(code),
                isDay,
                now.Number("relative_humidity_2m") ?? 0,
                now.Number("wind_speed_10m") ?? 0,
                fahrenheit ? "mph" : "km/h",
                daily.GetProperty("temperature_2m_max")[0].GetDouble(),
                daily.GetProperty("temperature_2m_min")[0].GetDouble(),
                hours);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Códigos WMO → tipo de ícono.</summary>
    public static WeatherKind KindOf(int code) => code switch
    {
        0 => WeatherKind.Clear,
        1 or 2 => WeatherKind.PartlyCloudy,
        3 => WeatherKind.Cloudy,
        45 or 48 => WeatherKind.Fog,
        >= 51 and <= 57 => WeatherKind.Drizzle,
        >= 61 and <= 67 or >= 80 and <= 82 => WeatherKind.Rain,
        >= 71 and <= 77 or 85 or 86 => WeatherKind.Snow,
        >= 95 => WeatherKind.Storm,
        _ => WeatherKind.Cloudy,
    };

    private static string Describe(int code) => code switch
    {
        0 => Strings.T("Despejado", "Clear sky"),
        1 => Strings.T("Mayormente despejado", "Mainly clear"),
        2 => Strings.T("Parcialmente nublado", "Partly cloudy"),
        3 => Strings.T("Nublado", "Overcast"),
        45 or 48 => Strings.T("Niebla", "Fog"),
        >= 51 and <= 57 => Strings.T("Llovizna", "Drizzle"),
        >= 61 and <= 65 => Strings.T("Lluvia", "Rain"),
        66 or 67 => Strings.T("Lluvia helada", "Freezing rain"),
        >= 71 and <= 77 => Strings.T("Nieve", "Snow"),
        >= 80 and <= 82 => Strings.T("Chubascos", "Showers"),
        85 or 86 => Strings.T("Chubascos de nieve", "Snow showers"),
        >= 95 => Strings.T("Tormenta eléctrica", "Thunderstorm"),
        _ => Strings.T("Variable", "Variable"),
    };
}
