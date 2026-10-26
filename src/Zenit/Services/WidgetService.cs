using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace Zenit.Services;

/// <summary>
/// Widget de la comunidad: un archivo JSON que declara una API pública y qué campos mostrar.
/// No ejecuta código: solo hace una petición HTTPS y muestra valores como texto.
/// </summary>
public sealed class WidgetDefinition
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "★";
    public string Accent { get; set; } = "#0A84FF";
    public string Url { get; set; } = "";
    public int RefreshMinutes { get; set; } = 15;
    public List<WidgetField> Fields { get; set; } = [];
}

public sealed class WidgetField
{
    public string Label { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Format { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
}

public sealed record WidgetValue(string Label, string Text);

public sealed class WidgetState(WidgetDefinition definition, string file)
{
    public WidgetDefinition Definition { get; } = definition;
    public string File { get; } = file;
    public IReadOnlyList<WidgetValue> Values { get; set; } = [];
    public string? Error { get; set; }
    public DateTime LastRefresh { get; set; }
}

public sealed class WidgetService
{
    public const int MaxWidgets = 8;
    private const long MaxFileBytes = 64 * 1024;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly string[] Examples = ["github-stars.json", "bitcoin.json", "aire-santiago.json"];

    private readonly DispatcherTimer _timer;

    public static string Folder => System.IO.Path.Combine(AppSettings.Folder, "widgets");

    public IReadOnlyList<WidgetState> Widgets { get; private set; } = [];

    /// <summary>Se dispara en el hilo de la UI al recargar o actualizar valores.</summary>
    public event Action? Updated;

    public WidgetService(Dispatcher dispatcher)
    {
        _timer = new DispatcherTimer(TimeSpan.FromMinutes(1), DispatcherPriority.Background, async (_, _) => await RefreshDueAsync(), dispatcher);
    }

    public void Start()
    {
        Reload();
        _timer.Start();
    }

    /// <summary>Lee la carpeta de widgets. Ignora archivos inválidos, grandes o sin HTTPS.</summary>
    public void Reload()
    {
        var list = new List<WidgetState>();
        try
        {
            if (Directory.Exists(Folder))
            {
                foreach (var file in Directory.EnumerateFiles(Folder, "*.json").Order().Take(MaxWidgets))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.Length > MaxFileBytes) continue;
                        var definition = JsonSerializer.Deserialize<WidgetDefinition>(File.ReadAllText(file), Json);
                        if (definition is null || string.IsNullOrWhiteSpace(definition.Name)) continue;
                        // Un "null" explícito en el JSON pisa los valores por defecto.
                        definition.Icon ??= "★";
                        definition.Accent ??= "#0A84FF";
                        definition.Url ??= "";
                        definition.Fields = definition.Fields?.Where(f => f?.Path is not null).ToList() ?? [];
                        foreach (var field in definition.Fields) field.Label ??= "";
                        var state = new WidgetState(definition, info.Name);
                        if (!definition.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            state.Error = Strings.T("La URL debe usar HTTPS", "The URL must use HTTPS");
                        definition.RefreshMinutes = Math.Clamp(definition.RefreshMinutes, 1, 1440);
                        list.Add(state);
                    }
                    catch (Exception)
                    {
                        // Archivo mal formado o ilegible: se omite.
                    }
                }
            }
        }
        catch (Exception)
        {
            // Carpeta inaccesible.
        }
        Widgets = list;
        Updated?.Invoke();
        _ = RefreshDueAsync(force: true);
    }

    /// <summary>Copia los widgets de ejemplo incluidos en la app a la carpeta del usuario.</summary>
    public void InstallExamples()
    {
        Directory.CreateDirectory(Folder);
        foreach (var name in Examples)
        {
            var target = System.IO.Path.Combine(Folder, name);
            if (File.Exists(target)) continue;
            var resource = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/widgets/{name}"));
            if (resource is null) continue;
            using var input = resource.Stream;
            using var output = File.Create(target);
            input.CopyTo(output);
        }
        Reload();
    }

    private async Task RefreshDueAsync(bool force = false)
    {
        var now = DateTime.Now;
        var due = Widgets.Where(w => w.Definition.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                         .Where(w => force || now - w.LastRefresh >= TimeSpan.FromMinutes(w.Definition.RefreshMinutes))
                         .ToList();
        if (due.Count == 0) return;
        await Task.WhenAll(due.Select(SafeRefreshAsync));
        Updated?.Invoke();
    }

    /// <summary>Un widget que falla no impide actualizar los demás.</summary>
    private static async Task SafeRefreshAsync(WidgetState widget)
    {
        try
        {
            await RefreshAsync(widget);
        }
        catch (Exception ex)
        {
            App.Log(ex);
            widget.Error = Strings.T("No disponible", "Unavailable");
        }
    }

    private static async Task RefreshAsync(WidgetState widget)
    {
        widget.LastRefresh = DateTime.Now;
        using var json = await Net.GetJsonAsync(widget.Definition.Url, untrusted: true);
        if (json is null)
        {
            widget.Error = Strings.T("No disponible", "Unavailable");
            return;
        }

        var values = new List<WidgetValue>();
        foreach (var field in widget.Definition.Fields.Take(4))
        {
            var element = Resolve(json.RootElement, field.Path);
            values.Add(new WidgetValue(field.Label, element is null ? "—" : FormatValue(element.Value, field)));
        }
        widget.Values = values;
        widget.Error = null;
    }

    /// <summary>Ruta tipo "data.items[0].price" sobre el JSON.</summary>
    private static JsonElement? Resolve(JsonElement root, string path)
    {
        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = segment;
            var index = -1;
            var bracket = segment.IndexOf('[');
            if (bracket >= 0 && segment.EndsWith(']'))
            {
                name = segment[..bracket];
                if (!int.TryParse(segment[(bracket + 1)..^1], out index)) return null;
            }
            if (name.Length > 0)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current)) return null;
            }
            if (index >= 0)
            {
                if (current.ValueKind != JsonValueKind.Array || index >= current.GetArrayLength()) return null;
                current = current[index];
            }
        }
        return current;
    }

    private static string FormatValue(JsonElement element, WidgetField field)
    {
        var text = element.ValueKind switch
        {
            JsonValueKind.Number when field.Format is { Length: > 0 } format => SafeFormat(element.GetDouble(), format),
            JsonValueKind.Number => element.GetDouble().ToString("#,0.##", CultureInfo.CurrentCulture),
            // Muchas APIs envían números como texto ("79849.50"): también se formatean.
            JsonValueKind.String when field.Format is { Length: > 0 } format
                && double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => SafeFormat(number, format),
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.True => Strings.T("Sí", "Yes"),
            JsonValueKind.False => "No",
            _ => "—",
        };
        if (text.Length > 40) text = text[..40] + "…";
        return $"{field.Prefix}{text}{field.Suffix}";
    }

    private static string SafeFormat(double value, string format)
    {
        try
        {
            return value.ToString(format, CultureInfo.CurrentCulture);
        }
        catch (FormatException)
        {
            return value.ToString(CultureInfo.CurrentCulture);
        }
    }
}
