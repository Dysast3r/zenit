using System.Globalization;
using System.Text.Json;

namespace Zenit.Services;

/// <summary>Cuánto vale 1 unidad de <see cref="Code"/> en la moneda local, y su variación frente al día anterior.</summary>
public sealed record CurrencyQuote(string Code, double Value, double? ChangePercent);

public sealed record CurrencyReport(string Local, DateOnly Date, IReadOnlyList<CurrencyQuote> Quotes);

/// <summary>
/// Tipos de cambio de más de 300 monedas y criptomonedas (proyecto abierto fawazahmed0/exchange-api,
/// servido por jsDelivr y Cloudflare). Sin clave ni límite de uso; se actualiza a diario.
/// </summary>
public sealed class CurrencyService
{
    private readonly AppSettings _settings;
    private static IReadOnlyDictionary<string, string>? _names;

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

        var quotes = new List<CurrencyQuote>();
        foreach (var code in codes)
        {
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
        ["bob"] = "Bs", ["sol"] = "◎", ["usdt"] = "₮", ["xrp"] = "✕", ["doge"] = "Ð",
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
