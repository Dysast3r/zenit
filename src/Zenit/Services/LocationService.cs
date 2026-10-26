using System.Globalization;
using Windows.Devices.Geolocation;

namespace Zenit.Services;

public sealed record Place(string Name, double Latitude, double Longitude);

/// <summary>
/// Ubicación actual: primero la de Windows y, si no está disponible (sin sensor, sin respuesta), una aproximación
/// por IP. Si el usuario negó el acceso a la ubicación en Windows, no se usa ninguna de las dos.
/// Se guarda en memoria 30 minutos para no consultar de más.
/// </summary>
public static class LocationService
{
    private static Place? _cached;
    private static DateTime _cachedAt;

    public static async Task<Place?> GetCurrentAsync()
    {
        if (_cached is not null && DateTime.UtcNow - _cachedAt < TimeSpan.FromMinutes(30)) return _cached;

        // Si el usuario negó la ubicación en Windows, se respeta: nada de aproximarla por IP.
        var access = await RequestAccessAsync();
        if (access == GeolocationAccessStatus.Denied)
        {
            _cached = null;
            return null;
        }

        var place = (access == GeolocationAccessStatus.Allowed ? await FromWindowsAsync() : null) ?? await FromIpAsync();
        if (place is not null)
        {
            _cached = place;
            _cachedAt = DateTime.UtcNow;
        }
        return place ?? _cached;
    }

    /// <summary>Denied = el usuario o la directiva lo bloquean; Unspecified = no se pudo saber (sin sensor, error).</summary>
    private static async Task<GeolocationAccessStatus> RequestAccessAsync()
    {
        try
        {
            return await Geolocator.RequestAccessAsync();
        }
        catch
        {
            return GeolocationAccessStatus.Unspecified;
        }
    }

    private static async Task<Place?> FromWindowsAsync()
    {
        try
        {
            var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
            var position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(8)).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(10));
            var point = position.Coordinate.Point.Position;
            var name = await ReverseGeocodeAsync(point.Latitude, point.Longitude);
            return new Place(name ?? Strings.T("Mi ubicación", "My location"), point.Latitude, point.Longitude);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<Place?> FromIpAsync()
    {
        using var json = await Net.GetJsonAsync("https://ipapi.co/json/");
        if (json is null) return null;
        var root = json.RootElement;
        var lat = root.Number("latitude");
        var lon = root.Number("longitude");
        if (lat is null || lon is null) return null;
        return new Place(root.Text("city") ?? Strings.T("Mi ubicación", "My location"), lat.Value, lon.Value);
    }

    private static async Task<string?> ReverseGeocodeAsync(double lat, double lon)
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var url = string.Create(CultureInfo.InvariantCulture,
            $"https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={lat}&longitude={lon}&localityLanguage={language}");
        using var json = await Net.GetJsonAsync(url);
        if (json is null) return null;
        var root = json.RootElement;
        return new[] { root.Text("city"), root.Text("locality"), root.Text("principalSubdivision") }
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
    }

    /// <summary>Busca ciudades por nombre (Open-Meteo Geocoding).</summary>
    public static async Task<IReadOnlyList<SavedCity>> SearchAsync(string query, CancellationToken cancellation)
    {
        if (query.Trim().Length < 2) return [];
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query.Trim())}&count=8&language={language}&format=json";
        using var json = await Net.GetJsonAsync(url, cancellation);
        if (json is null || !json.RootElement.TryGetProperty("results", out var results)) return [];

        var cities = new List<SavedCity>();
        foreach (var item in results.EnumerateArray())
        {
            var lat = item.Number("latitude");
            var lon = item.Number("longitude");
            if (lat is null || lon is null) continue;
            var region = item.Text("admin1");
            var country = item.Text("country") ?? "";
            cities.Add(new SavedCity
            {
                Name = item.Text("name") ?? "",
                Country = string.IsNullOrEmpty(region) ? country : $"{region}, {country}",
                Latitude = lat.Value,
                Longitude = lon.Value,
            });
        }
        return cities;
    }
}
