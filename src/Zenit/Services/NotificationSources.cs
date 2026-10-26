using System.Windows.Media;

namespace Zenit.Services;

/// <summary>Servicio web reconocido por el sitio de origen que el navegador incluye en la notificación.</summary>
public sealed record NotificationSource(string Name, string Glyph, Color Brand);

/// <summary>
/// Chrome, Edge y otros navegadores envían las notificaciones de las webs a su propio nombre ("Google Chrome")
/// y agregan el sitio como una línea más ("web.whatsapp.com"). Aquí se reconoce ese sitio para mostrar
/// el nombre y el color del servicio, y se quita esa línea del texto.
/// </summary>
public static class NotificationSources
{
    private static readonly (string[] Hosts, NotificationSource Source)[] Known =
    [
        (["web.whatsapp.com"], new("WhatsApp", "", Color.FromRgb(0x25, 0xD3, 0x66))),
        (["web.telegram.org"], new("Telegram", "", Color.FromRgb(0x22, 0x9E, 0xD9))),
        (["mail.google.com"], new("Gmail", "", Color.FromRgb(0xEA, 0x43, 0x35))),
        (["calendar.google.com"], new("Google Calendar", "", Color.FromRgb(0x1A, 0x73, 0xE8))),
        (["outlook.live.com", "outlook.office.com", "outlook.office365.com"], new("Outlook", "", Color.FromRgb(0x0F, 0x6C, 0xBD))),
        (["teams.microsoft.com", "teams.live.com"], new("Teams", "", Color.FromRgb(0x5B, 0x5F, 0xC7))),
        (["discord.com"], new("Discord", "", Color.FromRgb(0x58, 0x65, 0xF2))),
        (["app.slack.com"], new("Slack", "", Color.FromRgb(0x61, 0x1F, 0x69))),
        (["messenger.com", "www.messenger.com"], new("Messenger", "", Color.FromRgb(0x00, 0x84, 0xFF))),
        (["instagram.com", "www.instagram.com"], new("Instagram", "", Color.FromRgb(0xE1, 0x30, 0x6C))),
        (["linkedin.com", "www.linkedin.com"], new("LinkedIn", "", Color.FromRgb(0x0A, 0x66, 0xC2))),
    ];

    /// <summary>
    /// El servicio del que viene la notificación (o null) y los textos sin la línea del sitio.
    /// Solo se reconoce una línea que sea el sitio en sí ("web.whatsapp.com" o "https://web.whatsapp.com/"),
    /// para que un mensaje que menciona el dominio no cambie de remitente.
    /// </summary>
    public static (NotificationSource? source, List<string> texts) Match(IReadOnlyList<string> texts)
    {
        for (var i = 0; i < texts.Count; i++)
        {
            var host = HostOf(texts[i]);
            if (host is null) continue;
            foreach (var (hosts, source) in Known)
            {
                if (!hosts.Contains(host, StringComparer.OrdinalIgnoreCase)) continue;
                var rest = texts.Where((_, j) => j != i).ToList();
                return (source, rest);
            }
        }
        return (null, texts.ToList());
    }

    /// <summary>El host si la línea es solo una dirección web; null si es texto normal.</summary>
    private static string? HostOf(string line)
    {
        var text = line.Trim();
        if (text.Length == 0 || text.Contains(' ')) return null;
        if (!text.Contains("://")) text = "https://" + text;
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Host.Contains('.') ? uri.Host : null;
    }
}
