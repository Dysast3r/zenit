using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;

namespace Zenit.Services;

public enum AppLanguage { System, Spanish, English, Portuguese }

/// <summary>
/// Textos de la interfaz. Español e inglés viven en el código; los demás idiomas son catálogos JSON
/// (clave = texto en inglés) en Assets/i18n, traducidos por la comunidad con Crowdin. Un archivo en
/// %AppData%\Zenit\i18n\{código}.json permite probar o corregir traducciones sin recompilar.
/// </summary>
public static class Strings
{
    /// <summary>Idiomas con catálogo incluido, además de español e inglés.</summary>
    private static readonly string[] Catalogs = ["pt"];

    /// <summary>Idioma de Windows, capturado antes de que la app cambie la cultura del proceso.</summary>
    public static readonly CultureInfo WindowsCulture = CultureInfo.CurrentUICulture;

    private static Dictionary<string, string>? _catalog;

    public static string Language { get; private set; } = Resolve(AppLanguage.System);
    public static bool IsSpanish => Language == "es";

    private static string Resolve(AppLanguage language) => language switch
    {
        AppLanguage.Spanish => "es",
        AppLanguage.English => "en",
        AppLanguage.Portuguese => "pt",
        _ => WindowsCulture.TwoLetterISOLanguageName switch
        {
            "es" => "es",
            var code when Catalogs.Contains(code) => code,
            _ => "en",
        },
    };

    /// <summary>Aplica el idioma elegido a los textos y a los formatos de fecha y hora. Llamar antes de crear ventanas.</summary>
    public static void Initialize(AppLanguage language)
    {
        Language = Resolve(language);
        _catalog = Language is "es" or "en" ? null : LoadCatalog(Language);
        if (WindowsCulture.TwoLetterISOLanguageName == Language) return;

        // Conserva la región de Windows si existe esa combinación (p. ej. en-CL); si no, usa el idioma base.
        var region = CultureInfo.CurrentCulture.Name.Split('-').ElementAtOrDefault(1);
        CultureInfo culture;
        try
        {
            culture = region is null ? CultureInfo.GetCultureInfo(Language) : CultureInfo.GetCultureInfo($"{Language}-{region}");
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.GetCultureInfo(Language);
        }

        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    /// <summary>¿El idioma efectivo cambiaría con esta opción? (si cambia, la app se reinicia)</summary>
    public static bool WouldChange(AppLanguage language) => Resolve(language) != Language;

    private static Dictionary<string, string> LoadCatalog(string code)
    {
        var catalog = new Dictionary<string, string>();
        try
        {
            var resource = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/i18n/{code}.json"));
            if (resource is not null)
            {
                using var stream = resource.Stream;
                Merge(catalog, JsonSerializer.Deserialize<Dictionary<string, string>>(stream));
            }

            var local = Path.Combine(AppSettings.Folder, "i18n", $"{code}.json");
            if (File.Exists(local)) Merge(catalog, JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(local)));
        }
        catch
        {
            // Catálogo dañado: se usa inglés.
        }
        return catalog;
    }

    private static void Merge(Dictionary<string, string> target, Dictionary<string, string>? source)
    {
        if (source is null) return;
        foreach (var (key, value) in source)
            if (!string.IsNullOrWhiteSpace(value)) target[key] = value;
    }

    public static string T(string es, string en)
    {
        if (Language == "es") return es;
        if (_catalog is not null && _catalog.TryGetValue(en, out var translated)) return translated;
        return en;
    }

    /// <summary>Como <see cref="T"/>, con una plantilla {0}, {1}… para textos con valores.</summary>
    public static string F(string es, string en, params object?[] args)
    {
        var template = T(es, en);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            // Traducción de la comunidad con marcadores mal escritos: se usa la plantilla original.
            try
            {
                return string.Format(CultureInfo.CurrentCulture, Language == "es" ? es : en, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }
    }

    public static string AppName => "Zenit";
    public static string Settings => T("Configuración…", "Settings…");
    public static string Exit => T("Salir", "Quit");
    public static string NothingPlaying => T("Nada en reproducción", "Nothing playing");
    public static string OpenSpotifyHint => T("Abre Spotify para empezar", "Open Spotify to start");
    public static string Loading => T("Cargando…", "Loading…");
    public static string Offline => T("Sin conexión", "Offline");

    public static string Capitalize(string text)
        => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
}

/// <summary>Uso en XAML: Text="{s:Tr Es='Hola', En='Hello'}".</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public string Es { get; set; } = "";
    public string En { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.T(Es, En);
}
