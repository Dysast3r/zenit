using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Zenit.Services;

/// <summary>Apps de música que Zenit reconoce: tienen prioridad sobre el navegador y los demás reproductores.</summary>
public enum MusicApp { Other, Spotify, Tidal, YouTubeMusic }

/// <summary>Estado inmutable de lo que se está reproduciendo.</summary>
public sealed record MediaSnapshot(
    string AppId,
    string Title,
    string Artist,
    BitmapSource? Art,
    Color Accent,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset PositionStamp,
    bool CanPrevious,
    bool CanNext,
    bool CanSeek)
{
    public string TrackKey => Title + "\u0001" + Artist;

    /// <summary>Orden de publicación: crece con cada estado leído.</summary>
    public long Sequence { get; init; }

    public MusicApp App => MediaService.Identify(AppId);

    /// <summary>Posición interpolada: las apps solo informan la posición de vez en cuando.</summary>
    public TimeSpan CurrentPosition
    {
        get
        {
            var position = IsPlaying ? Position + (DateTimeOffset.Now - PositionStamp) : Position;
            if (position < TimeSpan.Zero) return TimeSpan.Zero;
            return Duration > TimeSpan.Zero && position > Duration ? Duration : position;
        }
    }
}

/// <summary>
/// Lee la sesión multimedia del sistema (la misma que usa el panel de Windows 11).
/// No requiere cuenta, claves ni conexión: Spotify, TIDAL y YouTube Music publican sus datos localmente.
/// </summary>
public sealed class MediaService : IDisposable
{
    private readonly object _gate = new();
    private readonly object _publishGate = new();
    private long _sequence, _published;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private int _mediaVersion;

    private string _appId = "", _title = "", _artist = "";
    private BitmapSource? _art;
    private Color _accent = Colors.White;
    private bool _playing, _canPrevious, _canNext, _canSeek;
    private TimeSpan _position, _duration;
    private DateTimeOffset _positionStamp = DateTimeOffset.Now, _playingSince = DateTimeOffset.Now;

    /// <summary>Se dispara desde hilos de fondo; null significa que no hay nada reproduciéndose.</summary>
    public event Action<MediaSnapshot?>? Changed;

    /// <summary>Ignora el navegador y los demás reproductores: solo Spotify, TIDAL y YouTube Music.</summary>
    public bool MusicAppsOnly { get; set; }

    /// <summary>
    /// Reconoce la app por su AppUserModelID: "Spotify.exe", el de TIDAL ("…TIDAL…") y los de YouTube Music
    /// (la app instalada desde el navegador y las de escritorio de la comunidad). Una pestaña del navegador
    /// no se distingue de cualquier otro video, así que cuenta como reproductor genérico.
    /// </summary>
    public static MusicApp Identify(string appId)
    {
        if (appId.Contains("Spotify", StringComparison.OrdinalIgnoreCase)) return MusicApp.Spotify;
        if (appId.Contains("TIDAL", StringComparison.OrdinalIgnoreCase)) return MusicApp.Tidal;
        return YouTubeMusicIds.Any(id => appId.Contains(id, StringComparison.OrdinalIgnoreCase)) ? MusicApp.YouTubeMusic : MusicApp.Other;
    }

    private static readonly string[] YouTubeMusicIds =
        ["youtube-music", "youtubemusic", "youtube music", "ytmusic", "ytmd", "cinhimbnkkaeohfgghhklpoalkkpdjmlno"];

    /// <summary>Vuelve a elegir la sesión si otra empezó a sonar (Windows no avisa de las que no están enganchadas).</summary>
    public void Refresh() => SelectSession();

    public async Task StartAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += (_, _) => SelectSession();
        _manager.CurrentSessionChanged += (_, _) => SelectSession();
        SelectSession();
    }

    public void Reselect() => SelectSession(force: true);

    /// <summary>Resincroniza la línea de tiempo (algunas apps no avisan cada cambio).</summary>
    public void PollTimeline()
    {
        var session = _session;
        if (session is null) return;
        try
        {
            lock (_gate) ReadTimeline(session);
        }
        catch
        {
            // La sesión puede cerrarse entre llamadas.
        }
    }

    public Task TogglePlayPauseAsync() => Run(s => s.TryTogglePlayPauseAsync().AsTask());
    public Task NextAsync() => Run(s => s.TrySkipNextAsync().AsTask());
    public Task PreviousAsync() => Run(s => s.TrySkipPreviousAsync().AsTask());

    public async Task SeekAsync(TimeSpan position)
    {
        lock (_gate)
        {
            _position = position;
            _positionStamp = DateTimeOffset.Now;
        }
        await Run(s => s.TryChangePlaybackPositionAsync(position.Ticks).AsTask());
    }

    private static async Task Run(GlobalSystemMediaTransportControlsSession? session, Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action)
    {
        if (session is null) return;
        try
        {
            await action(session);
        }
        catch
        {
            // La app de origen rechazó el comando o se cerró.
        }
    }

    private Task Run(Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action) => Run(_session, action);

    private void SelectSession(bool force = false)
    {
        var manager = _manager;
        if (manager is null) return;

        GlobalSystemMediaTransportControlsSession? chosen = null;
        try
        {
            // Primero lo que está sonando, y entre eso las apps de música; a igualdad se mantiene la sesión
            // actual (para no saltar entre dos que suenan a la vez) y luego la que Windows considera principal.
            var current = manager.GetCurrentSession();
            var best = int.MaxValue;
            foreach (var session in manager.GetSessions())
            {
                var music = Identify(session.SourceAppUserModelId) != MusicApp.Other;
                if (MusicAppsOnly && !music) continue;
                var playing = session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                var rank = ((playing ? 0 : 2) + (music ? 0 : 1)) * 4
                           + (ReferenceEquals(session, _session) ? 0 : 1) * 2
                           + (ReferenceEquals(session, current) ? 0 : 1);
                if (rank >= best) continue;
                best = rank;
                chosen = session;
            }
        }
        catch
        {
            // Sesiones en transición: se reintenta en el próximo evento.
        }

        lock (_gate)
        {
            // Se compara la sesión y no la app: dos pestañas del mismo navegador comparten AUMID.
            if (!force && _session is not null && ReferenceEquals(chosen, _session)) return;
            Attach(chosen);
        }
        Publish();
    }

    private void Attach(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_session is not null) Unsubscribe(_session);

        _session = session;
        _title = _artist = "";
        _art = null;
        _playing = false;

        if (session is null)
        {
            _appId = "";
            return;
        }

        try
        {
            _appId = session.SourceAppUserModelId;
            session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        }
        catch
        {
            // La sesión se cerró mientras se enganchaba: se queda sin sesión hasta el próximo evento.
            Unsubscribe(session);
            _session = null;
            _appId = "";
            return;
        }

        try
        {
            ReadPlayback(session);
            ReadTimeline(session);
        }
        catch
        {
            // Se completa con los eventos siguientes.
        }
        _ = RefreshMediaAsync(session);
    }

    private void Unsubscribe(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        }
        catch
        {
            // La sesión ya no existe.
        }
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        => _ = RefreshMediaAsync(sender);

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        try
        {
            lock (_gate) ReadPlayback(sender);
        }
        catch
        {
            return;
        }
        Publish();
    }

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        try
        {
            lock (_gate) ReadTimeline(sender);
        }
        catch
        {
            return;
        }
        Publish();
    }

    private void ReadPlayback(GlobalSystemMediaTransportControlsSession session)
    {
        var info = session.GetPlaybackInfo();
        var playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var controls = info.Controls;
        _canPrevious = controls.IsPreviousEnabled;
        _canNext = controls.IsNextEnabled;
        _canSeek = controls.IsPlaybackPositionEnabled;

        if (playing != _playing)
        {
            // Congela la posición en el cambio para que la interpolación no salte.
            _position = CurrentPositionUnsafe();
            _positionStamp = DateTimeOffset.Now;
            if (playing) _playingSince = _positionStamp;
            _playing = playing;
        }
    }

    private void ReadTimeline(GlobalSystemMediaTransportControlsSession session)
    {
        var timeline = session.GetTimelineProperties();
        var duration = timeline.EndTime - timeline.StartTime;
        _duration = duration > TimeSpan.Zero ? duration : TimeSpan.Zero;

        var stamp = timeline.LastUpdatedTime;
        var now = DateTimeOffset.Now;
        if (stamp.Year < 2000 || stamp > now) stamp = now;
        // Una marca anterior a la reanudación contaría la pausa como tiempo reproducido.
        if (_playing && stamp < _playingSince) stamp = _playingSince;

        _position = timeline.Position;
        _positionStamp = stamp;
    }

    private async Task RefreshMediaAsync(GlobalSystemMediaTransportControlsSession session)
    {
        var version = Interlocked.Increment(ref _mediaVersion);
        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            if (props is null) return;

            var title = props.Title ?? "";
            var artist = string.IsNullOrWhiteSpace(props.Artist) ? props.AlbumArtist ?? "" : props.Artist;

            BitmapSource? art = null;
            var accent = Colors.White;
            if (props.Thumbnail is not null)
            {
                art = await LoadArtAsync(props.Thumbnail);
                if (art is not null) accent = ColorExtractor.Extract(art);
            }

            if (version != Volatile.Read(ref _mediaVersion)) return;

            lock (_gate)
            {
                var sameTrack = title == _title && artist == _artist;
                _title = title;
                _artist = artist;
                // Spotify a veces publica primero los textos y luego la carátula: no la perdemos.
                if (art is not null || !sameTrack)
                {
                    _art = art;
                    _accent = accent;
                }
            }
            Publish();
        }
        catch
        {
            // Propiedades no disponibles momentáneamente.
        }
    }

    private static async Task<BitmapSource?> LoadArtAsync(IRandomAccessStreamReference reference)
    {
        try
        {
            using var stream = await reference.OpenReadAsync();
            using var source = stream.AsStream();
            var buffer = new MemoryStream();
            await source.CopyToAsync(buffer);
            buffer.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 256;
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

    private TimeSpan CurrentPositionUnsafe()
        => _playing ? _position + (DateTimeOffset.Now - _positionStamp) : _position;

    private void Publish()
    {
        MediaSnapshot? snapshot;
        long sequence;
        lock (_gate)
        {
            sequence = Interlocked.Increment(ref _sequence);
            snapshot = _session is null || string.IsNullOrWhiteSpace(_title)
                ? null
                : new MediaSnapshot(_appId, _title, _artist, _art, _accent, _playing,
                    _position, _duration, _positionStamp, _canPrevious, _canNext, _canSeek) { Sequence = sequence };
        }
        // Los eventos llegan desde varios hilos: se publica en orden y se descarta un estado más viejo que el último enviado.
        lock (_publishGate)
        {
            if (sequence <= _published) return;
            _published = sequence;
            Changed?.Invoke(snapshot);
        }
    }

    public void Dispose()
    {
        lock (_gate) Attach(null);
    }
}
