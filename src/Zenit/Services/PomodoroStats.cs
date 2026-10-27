using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Zenit.Services;

/// <summary>Pomodoros completados y minutos de enfoque de un día.</summary>
public sealed record PomodoroDay(DateOnly Date, int Sessions, int Minutes);

/// <summary>
/// Historial de pomodoros por día, guardado en %AppData%\Zenit\pomodoro.json.
/// Solo cuentan los períodos de enfoque que llegaron a cero (no los que se saltaron o reiniciaron).
/// </summary>
public sealed class PomodoroStats
{
    private static readonly string FilePath = Path.Combine(AppSettings.Folder, "pomodoro.json");

    /// <summary>Días que se conservan; lo más antiguo se descarta al guardar.</summary>
    private const int KeepDays = 366;

    private readonly Dictionary<DateOnly, (int Sessions, int Minutes)> _days = [];

    private PomodoroStats()
    {
    }

    public static PomodoroStats Load()
    {
        var stats = new PomodoroStats();
        try
        {
            if (!File.Exists(FilePath)) return stats;
            var days = JsonSerializer.Deserialize<Dictionary<string, int[]>>(File.ReadAllText(FilePath)) ?? [];
            foreach (var (key, value) in days)
            {
                if (value is not [var sessions, var minutes]) continue;
                if (DateOnly.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    stats._days[date] = (Math.Max(0, sessions), Math.Max(0, minutes));
            }
        }
        catch
        {
            // Archivo dañado: se empieza de cero sin impedir que la app funcione.
        }
        return stats;
    }

    public void Record(TimeSpan focus)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var (sessions, minutes) = _days.GetValueOrDefault(today);
        _days[today] = (sessions + 1, minutes + (int)Math.Round(focus.TotalMinutes));
        Save();
    }

    public PomodoroDay Today => Day(DateOnly.FromDateTime(DateTime.Today));

    /// <summary>Los últimos <paramref name="count"/> días, del más antiguo a hoy.</summary>
    public IReadOnlyList<PomodoroDay> LastDays(int count)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return Enumerable.Range(0, count).Select(i => Day(today.AddDays(i - count + 1))).ToList();
    }

    /// <summary>Días seguidos con al menos un pomodoro, terminando hoy (o ayer, si hoy aún no hay).</summary>
    public int Streak
    {
        get
        {
            var day = DateOnly.FromDateTime(DateTime.Today);
            if (Day(day).Sessions == 0) day = day.AddDays(-1);
            var streak = 0;
            while (Day(day).Sessions > 0)
            {
                streak++;
                day = day.AddDays(-1);
            }
            return streak;
        }
    }

    private PomodoroDay Day(DateOnly date)
    {
        var (sessions, minutes) = _days.GetValueOrDefault(date);
        return new PomodoroDay(date, sessions, minutes);
    }

    private void Save()
    {
        try
        {
            var oldest = DateOnly.FromDateTime(DateTime.Today).AddDays(-KeepDays);
            var data = _days.Where(p => p.Key >= oldest).OrderBy(p => p.Key)
                .ToDictionary(p => p.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), p => new[] { p.Value.Sessions, p.Value.Minutes });
            Directory.CreateDirectory(AppSettings.Folder);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(data));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch
        {
            // No es crítico: el conteo sigue en memoria durante esta sesión.
        }
    }
}
