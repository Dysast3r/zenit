using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Foundation.Metadata;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Zenit.Services;

public enum NotificationAccess { Unknown, Allowed, Denied, RequiresPackage }

/// <summary>Notificación lista para la isla. <see cref="Glyph"/> y <see cref="Brand"/> se usan cuando viene de un servicio web reconocido.</summary>
public sealed record IslandNotification(string App, string Title, string Body, BitmapSource? Icon, string? Glyph = null, System.Windows.Media.Color? Brand = null);

/// <summary>
/// Muestra en la isla las notificaciones de Windows (WhatsApp, correo, Teams...).
/// Windows solo permite leerlas a apps con identidad de paquete (MSIX / Microsoft Store),
/// así que la versión portable informa el estado y no hace nada más.
/// </summary>
public sealed class NotificationService
{
    private readonly HashSet<uint> _seen = [];
    private DispatcherTimer? _poll;
    private UserNotificationListener? _listener;
    private bool _starting, _wanted;

    public NotificationAccess Access { get; private set; } = NotificationAccess.Unknown;

    /// <summary>Llegó una notificación nueva. Se dispara en el hilo de la UI.</summary>
    public event Action<IslandNotification>? Received;

    /// <summary>Terminó un intento de inicio y <see cref="Access"/> puede haber cambiado.</summary>
    public event Action? AccessChanged;

    public async Task StartAsync(Dispatcher dispatcher)
    {
        // Guardar ajustes llama aquí cada vez: un solo inicio a la vez, y Stop() durante la espera lo cancela.
        _wanted = true;
        if (_poll is not null || _starting) return;
        _starting = true;
        try
        {
            if (!ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener"))
            {
                Access = NotificationAccess.RequiresPackage;
                return;
            }

            _listener = UserNotificationListener.Current;
            var status = await _listener.RequestAccessAsync();
            if (status != UserNotificationListenerAccessStatus.Allowed)
            {
                Access = NotificationAccess.Denied;
                return;
            }

            Access = NotificationAccess.Allowed;
            foreach (var existing in await _listener.GetNotificationsAsync(NotificationKinds.Toast)) _seen.Add(existing.Id);
            if (!_wanted) return;
            _poll = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, async (_, _) => await PollAsync(), dispatcher);
            _poll.Start();
        }
        catch
        {
            // Sin identidad de paquete, Windows rechaza la API con una excepción.
            Access = NotificationAccess.RequiresPackage;
        }
        finally
        {
            _starting = false;
            AccessChanged?.Invoke();
        }
    }

    public void Stop()
    {
        _wanted = false;
        _poll?.Stop();
        _poll = null;
    }

    private async Task PollAsync()
    {
        if (_listener is null) return;
        try
        {
            foreach (var item in await _listener.GetNotificationsAsync(NotificationKinds.Toast))
            {
                if (!_seen.Add(item.Id)) continue;

                var binding = item.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
                var raw = binding?.GetTextElements().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? [];
                var (source, texts) = NotificationSources.Match(raw);
                var app = source?.Name ?? item.AppInfo.DisplayInfo.DisplayName;
                Received?.Invoke(new IslandNotification(
                    app,
                    texts.FirstOrDefault() ?? app,
                    string.Join(" · ", texts.Skip(1)),
                    source is null ? await LoadLogoAsync(item) : null,
                    source?.Glyph,
                    source?.Brand));
            }
        }
        catch
        {
            // Una notificación puede desaparecer mientras se lee.
        }
    }

    private static async Task<BitmapSource?> LoadLogoAsync(UserNotification item)
    {
        try
        {
            var reference = item.AppInfo.DisplayInfo.GetLogo(new Windows.Foundation.Size(64, 64));
            using var stream = await reference.OpenReadAsync();
            var buffer = new MemoryStream();
            await stream.AsStream().CopyToAsync(buffer);
            buffer.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = buffer;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
