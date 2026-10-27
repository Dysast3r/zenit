using System.Globalization;
using System.Text.Json;

namespace Zenit.Services;

/// <summary>Cuánto vale 1 unidad de <see cref="Code"/> en la moneda local, y su variación frente al día anterior.</summary>
public sealed record CurrencyQuote(string Code, double Value, double? ChangePercent);

public sealed record CurrencyReport(string Local, DateOnly Date, IReadOnlyList<CurrencyQuote> Quotes);

/// <summary>Indicador del catálogo que se activa con un interruptor en ajustes.</summary>
public sealed record Indicator(string Code, string Name);

/// <summary>
/// Tipos de cambio de más de 300 monedas y criptomonedas (proyecto abierto fawazahmed0/exchange-api,
/// servido por jsDelivr y Cloudflare). Sin clave ni límite de uso; se actualiza a diario.
/// La UF y la UTM vienen de findic.cl o mindicador.cl (valores del Banco Central de Chile y el SII, en pesos chilenos).
/// </summary>
public sealed class CurrencyService
{
    /// <summary>Indicadores chilenos: no son monedas de currency-api y se piden a findic.cl o mindicador.cl.</summary>
    private static readonly string[] ChileanCodes = ["uf", "utm"];

    private readonly AppSettings _settings;
    private readonly Dictionary<string, (double Value, double? Change)> _chilean = [];
    private static IReadOnlyDictionary<string, string>? _names;

    /// <summary>Monedas e indicadores importantes que se ofrecen como lista de interruptores.</summary>
    public static IReadOnlyList<Indicator> Catalog =>
    [
        new("usd", Strings.T("Dólar estadounidense", "US dollar")),
        new("eur", Strings.T("Euro", "Euro")),
        new("gbp", Strings.T("Libra esterlina", "British pound")),
        new("jpy", Strings.T("Yen japonés", "Japanese yen")),
        new("cny", Strings.T("Yuan chino", "Chinese yuan")),
        new("chf", Strings.T("Franco suizo", "Swiss franc")),
        new("cad", Strings.T("Dólar canadiense", "Canadian dollar")),
        new("aud", Strings.T("Dólar australiano", "Australian dollar")),
        new("brl", Strings.T("Real brasileño", "Brazilian real")),
        new("ars", Strings.T("Peso argentino", "Argentine peso")),
        new("mxn", Strings.T("Peso mexicano", "Mexican peso")),
        new("cop", Strings.T("Peso colombiano", "Colombian peso")),
        new("pen", Strings.T("Sol peruano", "Peruvian sol")),
        new("uf", Strings.T("Unidad de Fomento (Chile)", "Unidad de Fomento (Chile)")),
        new("utm", Strings.T("Unidad Tributaria Mensual (Chile)", "Monthly Tax Unit (Chile)")),
        new("btc", Strings.T("Bitcoin", "Bitcoin")),
        new("eth", Strings.T("Ethereum", "Ethereum")),
    ];

    public static bool IsChilean(string code) => ChileanCodes.Contains(code);

    public CurrencyService(AppSettings settings) => _settings = settings;

    public CurrencyReport? Report { get; private set; }

    /// <summary>La última actualización falló (sin conexión o respuesta inválida).</summary>
    public bool Failed { get; private set; }

    /// <summary>El informe es de otra moneda local (se cambió en ajustes y aún no llega el nuevo).</summary>
    public bool IsStale => Report is { } report && report.Local != _settings.LocalCurrency;

    public event Action? Updated;

    public async Task RefreshAsync()
    {
        var local = _settings.LocalCurrency;
        var codes = _settings.Currencies.ToList();
        Failed = false;

        var chileanTasks = codes.Where(IsChilean).ToDictionary(c => c, GetChileanAsync);
        using var today = await GetRatesAsync(local, "latest");
        if (today is null)
        {
            Fail();
            return;
        }

        var root = today.RootElement;
        var date = DateOnly.TryParse(root.Text("date"), CultureInfo.InvariantCulture, out var d) ? d : DateOnly.FromDateTime(DateTime.Today);
        if (!root.TryGetProperty(local, out var rates))
        {
            Fail();
            return;
        }

        using var yesterday = await GetRatesAsync(local, date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        JsonElement previous = default;
        var hasPrevious = yesterday is not null && yesterday.RootElement.TryGetProperty(local, out previous);

        // Pesos chilenos por unidad local, para pasar la UF y la UTM a la moneda local.
        var clpPerLocal = local == "clp" ? 1 : rates.Number("clp");

        Publish();

        // Las monedas no esperan a mindicador.cl: la UF y la UTM se agregan cuando llegan.
        if (chileanTasks.Values.Any(t => !t.IsCompleted))
        {
            await Task.WhenAll(chileanTasks.Values);
            if (local == _settings.LocalCurrency) Publish();
        }

        void Publish()
        {
            var quotes = new List<CurrencyQuote>();
            foreach (var code in codes)
            {
                if (chileanTasks.TryGetValue(code, out var task))
                {
                    // mindicador.cl a veces no responde: se mantiene el último valor conocido.
                    if (task.IsCompletedSuccessfully && task.Result is { } fresh) _chilean[code] = fresh;
                    // La variación es la del indicador en pesos; no incluye el movimiento del tipo de cambio.
                    if (_chilean.TryGetValue(code, out var known) && clpPerLocal is > 0)
                        quotes.Add(new CurrencyQuote(code, known.Value / clpPerLocal.Value, known.Change));
                    continue;
                }

                // El archivo da "1 local = x código"; se invierte para mostrar "1 código = y local".
                var rate = rates.Number(code);
                if (rate is not > 0) continue;
                var value = 1 / rate.Value;

                double? change = null;
                if (hasPrevious && previous.Number(code) is > 0 and var old)
                    change = (value / (1 / old) - 1) * 100;
                quotes.Add(new CurrencyQuote(code, value, change));
            }

            Report = new CurrencyReport(local, date, quotes);
            Updated?.Invoke();
        }
    }

    private void Fail()
    {
        Failed = true;
        Updated?.Invoke();
    }

    private static async Task<JsonDocument?> GetRatesAsync(string currency, string version)
    {
        var file = $"v1/currencies/{currency}.min.json";
        return await Net.GetJsonAsync($"https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@{version}/{file}")
            ?? await Net.GetJsonAsync($"https://{version}.currency-api.pages.dev/{file}");
    }

    /// <summary>Valor en pesos chilenos de la UF o la UTM y su variación frente al valor anterior (día o mes).</summary>
    private static async Task<(double Value, double? Change)?> GetChileanAsync(string code)
    {
        // findic.cl y mindicador.cl publican la misma serie; mindicador.cl a veces rechaza las conexiones.
        foreach (var host in new[] { "findic.cl", "mindicador.cl" })
        {
            using var series = await Net.GetJsonAsync($"https://{host}/api/{code}");
            if (series is null || !series.RootElement.TryGetProperty("serie", out var serie) || serie.ValueKind != JsonValueKind.Array) continue;

            // Más reciente primero; se descartan fechas futuras por si la serie trae valores ya publicados.
            var values = serie.EnumerateArray()
                .Select(p => (Date: DateTimeOffset.TryParse(p.Text("fecha"), CultureInfo.InvariantCulture, out var d) ? d : default, Value: p.Number("valor")))
                .Where(p => p.Value is > 0 && p.Date != default && p.Date <= DateTimeOffset.UtcNow)
                .OrderByDescending(p => p.Date)
                .Select(p => p.Value!.Value)
                .Take(2)
                .ToList();
            if (values.Count > 0)
                return (values[0], values.Count > 1 ? (values[0] / values[1] - 1) * 100 : null);
        }

        // Respaldo: el resumen diario de mindicador.cl trae solo el valor actual.
        using var summary = await Net.GetJsonAsync("https://mindicador.cl/api");
        if (summary is not null && summary.RootElement.TryGetProperty(code, out var indicator) && indicator.Number("valor") is > 0 and var value)
            return (value, null);
        return null;
    }

    /// <summary>Nombre del indicador en el catálogo, o null si es otra moneda.</summary>
    public static string? CatalogName(string code) => Catalog.FirstOrDefault(i => i.Code == code)?.Name;

    /// <summary>Código → nombre (en inglés) de todas las monedas disponibles.</summary>
    public static async Task<IReadOnlyDictionary<string, string>> GetNamesAsync()
    {
        if (_names is not null) return _names;
        const string file = "v1/currencies.min.json";
        using var json = await Net.GetJsonAsync($"https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/{file}")
            ?? await Net.GetJsonAsync($"https://latest.currency-api.pages.dev/{file}");
        if (json is null) return new Dictionary<string, string>();

        _names = json.RootElement.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(p => p.Name, p => string.IsNullOrWhiteSpace(p.Value.GetString()) ? p.Name.ToUpperInvariant() : p.Value.GetString()!);
        return _names;
    }

    private static readonly Dictionary<string, string> Symbols = new()
    {
        ["usd"] = "$", ["eur"] = "€", ["gbp"] = "£", ["jpy"] = "¥", ["cny"] = "¥", ["btc"] = "₿", ["eth"] = "Ξ",
        ["clp"] = "$", ["mxn"] = "$", ["ars"] = "$", ["cop"] = "$", ["brl"] = "R$", ["pen"] = "S/", ["uyu"] = "$",
        ["krw"] = "₩", ["inr"] = "₹", ["rub"] = "₽", ["try"] = "₺", ["chf"] = "Fr", ["cad"] = "$", ["aud"] = "$",
        ["ils"] = "₪", ["php"] = "₱", ["thb"] = "฿", ["ngn"] = "₦", ["uah"] = "₴", ["vnd"] = "₫", ["pyg"] = "₲",
        ["bob"] = "Bs", ["sol"] = "◎", ["usdt"] = "₮", ["xrp"] = "✕", ["doge"] = "Ð", ["uf"] = "UF", ["utm"] = "UT",
    };

    public static string SymbolOf(string code) => Symbols.TryGetValue(code, out var s) ? s : code[..1].ToUpperInvariant();

    /// <summary>Formato legible: miles sin decimales, unidades con 2 y fracciones con hasta 6.</summary>
    public static string FormatValue(double value) => value switch
    {
        >= 1000 => value.ToString("N0", CultureInfo.CurrentCulture),
        >= 1 => value.ToString("N2", CultureInfo.CurrentCulture),
        >= 0.01 => value.ToString("N4", CultureInfo.CurrentCulture),
        _ => value.ToString("0.######", CultureInfo.CurrentCulture),
    };
}
