using System.Globalization;

namespace Zenit.Services;

public static class TimeFormats
{
    public static string Time(DateTime time, AppSettings s, bool? seconds = null)
    {
        var withSeconds = seconds ?? s.ShowSeconds;
        var format = s.Use24Hour
            ? withSeconds ? "HH:mm:ss" : "H:mm"
            : withSeconds ? "h:mm:ss tt" : "h:mm tt";
        return time.ToString(format, CultureInfo.CurrentCulture);
    }

    /// <summary>Solo la hora, para columnas de pronóstico ("14" o "2 p. m." según el formato elegido).</summary>
    public static string Hour(DateTime time, AppSettings s)
        => time.ToString(s.Use24Hour ? "HH" : "h tt", CultureInfo.CurrentCulture);

    public static string Date(DateTime date, AppSettings s) => Date(date, s.DateStyle, s.CustomDateFormat);

    public static string Date(DateTime date, DateStyle style, string custom)
    {
        var culture = CultureInfo.CurrentCulture;
        var text = style switch
        {
            DateStyle.Long => $"{date.ToString("dddd", culture)}, {date.ToString("M", culture)}",
            DateStyle.Full => date.ToString("D", culture),
            DateStyle.Medium => date.ToString("ddd d MMM", culture).Replace(".", ""),
            DateStyle.Short => date.ToString("d", culture),
            DateStyle.Iso => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => TryCustom(date, custom),
        };
        return Strings.Capitalize(text);
    }

    /// <summary>Formato personalizado por defecto de los ajustes; está escrito en español.</summary>
    private const string SpanishDefaultFormat = "dddd d 'de' MMMM, yyyy";

    private static string TryCustom(DateTime date, string format)
    {
        // Quien no ha tocado el formato en otro idioma vería "Monday 26 de October": se usa la fecha larga de su idioma.
        if (format == SpanishDefaultFormat && !Strings.IsSpanish) format = "D";
        try
        {
            return string.IsNullOrWhiteSpace(format) ? date.ToString("D", CultureInfo.CurrentCulture) : date.ToString(format, CultureInfo.CurrentCulture);
        }
        catch (FormatException)
        {
            return date.ToString("D", CultureInfo.CurrentCulture);
        }
    }

    public static string DayInfo(DateTime date)
    {
        var days = DateTime.IsLeapYear(date.Year) ? 366 : 365;
        return Strings.F("Semana {0} · Día {1} de {2}", "Week {0} · Day {1} of {2}", ISOWeek.GetWeekOfYear(date), date.DayOfYear, days);
    }

    public static string Temperature(double value) => $"{Math.Round(value):0}°";
}
