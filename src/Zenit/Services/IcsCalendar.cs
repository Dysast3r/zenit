using System.Globalization;

namespace Zenit.Services;

/// <summary>Evento de calendario ya expandido, en hora local.</summary>
public sealed record CalendarEvent(string Title, DateTime Start, DateTime End);

/// <summary>
/// Lector mínimo de iCalendar (RFC 5545) para los enlaces privados de Google Calendar y Outlook.
/// Cubre lo que esos servicios publican: zonas horarias (TZID de Windows o IANA), eventos de todo el día
/// (se omiten), repeticiones DAILY/WEEKLY/MONTHLY/YEARLY con INTERVAL, COUNT, UNTIL, BYDAY y BYMONTHDAY,
/// fechas excluidas (EXDATE), ocurrencias modificadas (RECURRENCE-ID) y eventos cancelados.
/// </summary>
public static class IcsCalendar
{
    /// <summary>Tope de iteraciones por evento repetido, por si un RRULE no termina nunca dentro del rango.</summary>
    private const int MaxIterations = 20000;

    private sealed record Property(string Name, Dictionary<string, string> Params, string Value);

    private readonly record struct Stamp(DateTime Wall, TimeZoneInfo Zone)
    {
        public DateTime Utc => Zone == TimeZoneInfo.Utc ? Wall : TimeZoneInfo.ConvertTimeToUtc(Clamp(Wall, Zone), Zone);
        public DateTime Local => TimeZoneInfo.ConvertTimeFromUtc(Utc, TimeZoneInfo.Local);

        // Una hora que no existe por el cambio de horario (02:30 al adelantar el reloj) se corre una hora.
        private static DateTime Clamp(DateTime wall, TimeZoneInfo zone)
            => zone.IsInvalidTime(wall) ? wall.AddHours(1) : wall;
    }

    /// <summary>Eventos que se cruzan con [<paramref name="from"/>, <paramref name="to"/>), ordenados por inicio.</summary>
    public static List<CalendarEvent> Parse(string ics, DateTime from, DateTime to)
    {
        var fromUtc = from.ToUniversalTime();
        var toUtc = to.ToUniversalTime();
        var events = ReadEvents(ics);

        // Ocurrencias que se movieron o cancelaron: la serie original no debe generarlas.
        var overridden = new HashSet<(string, DateTime)>();
        foreach (var e in events)
            if (Get(e, "RECURRENCE-ID") is { } id && ParseStamp(id) is { } original)
                overridden.Add((Get(e, "UID")?.Value ?? "", original.Utc));

        var result = new List<CalendarEvent>();
        foreach (var e in events)
        {
            if (string.Equals(Get(e, "STATUS")?.Value, "CANCELLED", StringComparison.OrdinalIgnoreCase)) continue;
            if (Get(e, "DTSTART") is not { } startProperty || ParseStamp(startProperty) is not { } start) continue;

            var duration = Get(e, "DTEND") is { } endProperty && ParseStamp(endProperty) is { } end
                ? end.Utc - start.Utc
                : ParseDuration(Get(e, "DURATION")?.Value) ?? TimeSpan.Zero;
            var title = Unescape(Get(e, "SUMMARY")?.Value ?? "");
            var uid = Get(e, "UID")?.Value ?? "";

            var rule = Get(e, "RRULE");
            if (rule is null || Get(e, "RECURRENCE-ID") is not null)
            {
                Add(start);
                continue;
            }

            var excluded = e.Where(p => p.Name == "EXDATE").SelectMany(ExpandList).Select(s => s.Utc).ToHashSet();
            foreach (var occurrence in Expand(start, rule.Value, toUtc))
            {
                var utc = occurrence.Utc;
                if (excluded.Contains(utc) || overridden.Contains((uid, utc))) continue;
                Add(occurrence);
            }

            void Add(Stamp s)
            {
                var startUtc = s.Utc;
                if (startUtc + duration <= fromUtc || startUtc >= toUtc) return;
                var local = s.Local;
                result.Add(new CalendarEvent(title, local, local + duration));
            }
        }
        result.Sort((a, b) => a.Start.CompareTo(b.Start));
        return result;
    }

    // ───────────────────────── Lectura ─────────────────────────

    /// <summary>Propiedades de cada VEVENT, con las líneas largas ya desplegadas.</summary>
    private static List<List<Property>> ReadEvents(string ics)
    {
        var text = ics.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n ", "").Replace("\n\t", "");
        var events = new List<List<Property>>();
        List<Property>? current = null;
        var depth = 0; // VALARM y otros bloques dentro del evento no aportan propiedades.
        foreach (var line in text.Split('\n'))
        {
            if (line.Length == 0) continue;
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current = [];
                depth = 0;
                continue;
            }
            if (current is null) continue;
            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(current);
                current = null;
                continue;
            }
            if (line.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase)) depth++;
            else if (line.StartsWith("END:", StringComparison.OrdinalIgnoreCase)) depth--;
            else if (depth == 0 && ParseLine(line) is { } property) current.Add(property);
        }
        return events;
    }

    private static Property? ParseLine(string line)
    {
        // El ":" que separa el valor es el primero fuera de comillas (un parámetro puede llevar ":" entre comillas).
        var quoted = false;
        var colon = -1;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') quoted = !quoted;
            else if (line[i] == ':' && !quoted)
            {
                colon = i;
                break;
            }
        }
        if (colon <= 0) return null;

        var head = line[..colon].Split(';');
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in head.Skip(1))
        {
            var eq = part.IndexOf('=');
            if (eq > 0) parameters[part[..eq]] = part[(eq + 1)..].Trim('"');
        }
        return new Property(head[0].ToUpperInvariant(), parameters, line[(colon + 1)..]);
    }

    private static Property? Get(List<Property> e, string name) => e.FirstOrDefault(p => p.Name == name);

    private static string Unescape(string value)
        => value.Replace("\\n", " ").Replace("\\N", " ").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\").Trim();

    // ───────────────────────── Fechas ─────────────────────────

    /// <summary>Fecha con hora en su zona; null para eventos de todo el día (VALUE=DATE) o valores ilegibles.</summary>
    private static Stamp? ParseStamp(Property property) => ParseStamp(property.Value, property.Params);

    private static Stamp? ParseStamp(string value, Dictionary<string, string> parameters)
    {
        value = value.Trim();
        if (value.Length == 8 || string.Equals(parameters.GetValueOrDefault("VALUE"), "DATE", StringComparison.OrdinalIgnoreCase)) return null;

        var utc = value.EndsWith('Z');
        if (!DateTime.TryParseExact(utc ? value[..^1] : value, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var wall))
            return null;
        if (utc) return new Stamp(DateTime.SpecifyKind(wall, DateTimeKind.Unspecified), TimeZoneInfo.Utc);
        return new Stamp(wall, FindZone(parameters.GetValueOrDefault("TZID")));
    }

    private static IEnumerable<Stamp> ExpandList(Property property)
        => property.Value.Split(',').Select(v => ParseStamp(v, property.Params)).OfType<Stamp>();

    private static readonly Dictionary<string, TimeZoneInfo> Zones = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Zona por su id de Windows o IANA; sin zona (hora "flotante") o desconocida, la local.</summary>
    private static TimeZoneInfo FindZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Local;
        lock (Zones)
        {
            if (Zones.TryGetValue(id, out var cached)) return cached;
            TimeZoneInfo zone;
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch
            {
                zone = TimeZoneInfo.Local;
            }
            Zones[id] = zone;
            return zone;
        }
    }

    /// <summary>Duración ISO 8601 de iCal: P1D, PT1H30M, P1W…</summary>
    private static TimeSpan? ParseDuration(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var negative = value.StartsWith('-');
        var total = TimeSpan.Zero;
        var number = 0;
        foreach (var c in value.TrimStart('+', '-'))
        {
            if (char.IsDigit(c))
            {
                number = number * 10 + (c - '0');
                continue;
            }
            total += c switch
            {
                'W' => TimeSpan.FromDays(7 * number),
                'D' => TimeSpan.FromDays(number),
                'H' => TimeSpan.FromHours(number),
                'M' => TimeSpan.FromMinutes(number),
                'S' => TimeSpan.FromSeconds(number),
                _ => TimeSpan.Zero,
            };
            number = 0;
        }
        return negative ? -total : total;
    }

    // ───────────────────────── Repeticiones ─────────────────────────

    private static readonly Dictionary<string, DayOfWeek> Days = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MO"] = DayOfWeek.Monday, ["TU"] = DayOfWeek.Tuesday, ["WE"] = DayOfWeek.Wednesday, ["TH"] = DayOfWeek.Thursday,
        ["FR"] = DayOfWeek.Friday, ["SA"] = DayOfWeek.Saturday, ["SU"] = DayOfWeek.Sunday,
    };

    /// <summary>Ocurrencias de la serie desde su inicio hasta <paramref name="limitUtc"/>, en la hora de pared de su zona.</summary>
    private static IEnumerable<Stamp> Expand(Stamp start, string rrule, DateTime limitUtc)
    {
        var rule = rrule.Split(';')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].ToUpperInvariant(), p => p[1], StringComparer.OrdinalIgnoreCase);

        var frequency = rule.GetValueOrDefault("FREQ", "").ToUpperInvariant();
        var interval = Math.Max(1, int.TryParse(rule.GetValueOrDefault("INTERVAL"), out var i) ? i : 1);
        int? count = int.TryParse(rule.GetValueOrDefault("COUNT"), out var c) ? c : null;
        var until = rule.GetValueOrDefault("UNTIL") is { } u ? UntilUtc(u, start.Zone) : null;
        var byDay = (rule.GetValueOrDefault("BYDAY") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseByDay).OfType<(int ordinal, DayOfWeek day)>().ToList();
        var byMonthDay = (rule.GetValueOrDefault("BYMONTHDAY") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => int.TryParse(d, out var n) ? n : 0).Where(n => n != 0).ToList();

        var time = start.Wall.TimeOfDay;
        var emitted = 0;
        for (var period = 0; period < MaxIterations; period++)
        {
            List<DateTime> candidates = frequency switch
            {
                "DAILY" => [start.Wall.Date.AddDays(period * interval)],
                "WEEKLY" => WeeklyDates(start.Wall.Date, period * interval, byDay),
                "MONTHLY" => MonthlyDates(start.Wall.Date, period * interval, byDay, byMonthDay),
                "YEARLY" => YearlyDate(start.Wall.Date, period * interval),
                _ => period == 0 ? [start.Wall.Date] : [],
            };
            if (frequency is not ("DAILY" or "WEEKLY" or "MONTHLY" or "YEARLY") && period > 0) yield break;

            foreach (var date in candidates.Order())
            {
                var stamp = new Stamp(date + time, start.Zone);
                if (stamp.Wall < start.Wall) continue;
                var utc = stamp.Utc;
                if (until is { } last && utc > last) yield break;
                if (count is { } max && emitted >= max) yield break;
                if (utc >= limitUtc) yield break;
                emitted++;
                yield return stamp;
            }
        }
    }

    private static DateTime? UntilUtc(string value, TimeZoneInfo zone)
    {
        if (value.Length == 8 && DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return new Stamp(day.AddDays(1).AddTicks(-1), zone).Utc;
        return ParseStamp(value, new Dictionary<string, string>()) is { } stamp
            ? (value.EndsWith('Z') ? stamp.Utc : new Stamp(stamp.Wall, zone).Utc)
            : null;
    }

    /// <summary>"MO", "1MO" (primer lunes) o "-1FR" (último viernes).</summary>
    private static (int ordinal, DayOfWeek day)? ParseByDay(string value)
    {
        value = value.Trim();
        if (value.Length < 2 || !Days.TryGetValue(value[^2..], out var day)) return null;
        var prefix = value[..^2];
        return (prefix.Length == 0 ? 0 : int.TryParse(prefix, out var n) ? n : 0, day);
    }

    private static List<DateTime> WeeklyDates(DateTime first, int weeks, List<(int ordinal, DayOfWeek day)> byDay)
    {
        // Semanas de lunes a domingo (WKST=MO, el valor por defecto).
        var monday = first.AddDays(-(((int)first.DayOfWeek + 6) % 7)).AddDays(7 * weeks);
        IEnumerable<DayOfWeek> days = byDay.Count > 0 ? byDay.Select(d => d.day) : [first.DayOfWeek];
        return days.Distinct().Select(d => monday.AddDays(((int)d + 6) % 7)).ToList();
    }

    private static List<DateTime> MonthlyDates(DateTime first, int months, List<(int ordinal, DayOfWeek day)> byDay, List<int> byMonthDay)
    {
        var month = new DateTime(first.Year, first.Month, 1).AddMonths(months);
        var length = DateTime.DaysInMonth(month.Year, month.Month);
        var dates = new List<DateTime>();

        if (byDay.Count > 0)
        {
            foreach (var (ordinal, day) in byDay)
            {
                var matches = Enumerable.Range(0, length).Select(d => month.AddDays(d)).Where(d => d.DayOfWeek == day).ToList();
                if (ordinal == 0) dates.AddRange(matches);
                else if (ordinal > 0 && ordinal <= matches.Count) dates.Add(matches[ordinal - 1]);
                else if (ordinal < 0 && -ordinal <= matches.Count) dates.Add(matches[matches.Count + ordinal]);
            }
            return dates;
        }

        foreach (var day in byMonthDay.Count > 0 ? byMonthDay : [first.Day])
        {
            var target = day > 0 ? day : length + day + 1;
            if (target >= 1 && target <= length) dates.Add(month.AddDays(target - 1));
        }
        return dates;
    }

    private static List<DateTime> YearlyDate(DateTime first, int years)
    {
        var year = first.Year + years;
        // El 29 de febrero solo existe en años bisiestos.
        return first.Day <= DateTime.DaysInMonth(year, first.Month) ? [new DateTime(year, first.Month, first.Day)] : [];
    }
}
