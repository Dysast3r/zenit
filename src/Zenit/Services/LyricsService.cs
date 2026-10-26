using System.Globalization;
using System.Text.RegularExpressions;

namespace Zenit.Services;

public sealed record LyricLine(TimeSpan Time, string Text);

/// <summary>
/// Letras sincronizadas desde LRCLIB (base abierta y gratuita, sin clave). Solo se envían
/// el título, el artista y la duración de la canción.
/// </summary>
public sealed partial class LyricsService
{
    private string _key = "";
    private CancellationTokenSource? _request;

    /// <summary>Adelanto con que se muestra cada línea: se lee mejor si aparece un instante antes de cantarse.</summary>
    public static readonly TimeSpan Lead = TimeSpan.FromMilliseconds(500);

    public IReadOnlyList<LyricLine> Lines { get; private set; } = [];
    public bool IsLoading { get; private set; }
    public bool HasPlainOnly { get; private set; }

    /// <summary>Se dispara en el hilo de la UI cuando cambian las letras.</summary>
    public event Action? Updated;

    public async Task LoadAsync(string title, string artist, TimeSpan duration)
    {
        var key = $"{title}\u0001{artist}";
        if (key == _key) return;
        _key = key;

        _request?.Cancel();
        var cts = _request = new CancellationTokenSource();
        Lines = [];
        HasPlainOnly = false;
        IsLoading = !string.IsNullOrWhiteSpace(title);
        Updated?.Invoke();
        if (!IsLoading) return;

        var url = "https://lrclib.net/api/get?track_name=" + Uri.EscapeDataString(title)
                  + "&artist_name=" + Uri.EscapeDataString(artist)
                  + (duration > TimeSpan.Zero ? "&duration=" + ((int)duration.TotalSeconds).ToString(CultureInfo.InvariantCulture) : "");
        using var json = await Net.GetJsonAsync(url, cts.Token);
        if (cts.IsCancellationRequested) return;

        IsLoading = false;
        if (json is not null)
        {
            var root = json.RootElement;
            var synced = root.Text("syncedLyrics");
            if (!string.IsNullOrWhiteSpace(synced)) Lines = Parse(synced);
            else HasPlainOnly = !string.IsNullOrWhiteSpace(root.Text("plainLyrics"));
        }
        Updated?.Invoke();
    }

    /// <summary>Índice de la línea que corresponde a la posición actual (-1 antes de la primera).</summary>
    public int IndexAt(TimeSpan position)
    {
        var lines = Lines;
        var index = -1;
        for (var i = 0; i < lines.Count && lines[i].Time <= position; i++) index = i;
        return index;
    }

    private static List<LyricLine> Parse(string lrc)
    {
        var lines = new List<LyricLine>();
        foreach (var raw in lrc.Split('\n'))
        {
            var matches = Stamp().Matches(raw);
            if (matches.Count == 0) continue;
            var text = Stamp().Replace(raw, "").Trim();
            foreach (Match m in matches)
            {
                var time = TimeSpan.FromMinutes(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                         + TimeSpan.FromSeconds(double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
                lines.Add(new LyricLine(time, text.Length == 0 ? "♪" : text));
            }
        }
        lines.Sort((a, b) => a.Time.CompareTo(b.Time));
        return lines;
    }

    [GeneratedRegex(@"\[(\d+):(\d+(?:\.\d+)?)\]")]
    private static partial Regex Stamp();
}
