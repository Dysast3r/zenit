using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Zenit.Interop;

namespace Zenit.Services;

/// <summary>Una aplicación que reproduce audio, con todas sus sesiones (un navegador puede tener varias).</summary>
public sealed class AudioApp
{
    private readonly List<object> _sessions;
    private Guid _context = Guid.NewGuid();

    internal AudioApp(uint processId, string name, BitmapSource? icon, bool active, List<object> sessions)
    {
        ProcessId = processId;
        Name = name;
        Icon = icon;
        IsActive = active;
        _sessions = sessions;
    }

    public uint ProcessId { get; }
    public string Name { get; }
    public BitmapSource? Icon { get; }

    /// <summary>Está sonando ahora (y no solo abierta con audio en pausa).</summary>
    public bool IsActive { get; }

    public float Volume
    {
        get
        {
            foreach (var volume in _sessions.OfType<ISimpleAudioVolume>())
                if (volume.GetMasterVolume(out var level) == 0) return level;
            return 0;
        }
        set
        {
            foreach (var volume in _sessions.OfType<ISimpleAudioVolume>()) volume.SetMasterVolume(Math.Clamp(value, 0f, 1f), ref _context);
        }
    }

    public bool Muted
    {
        get => _sessions.OfType<ISimpleAudioVolume>().Any(v => v.GetMute(out var muted) == 0 && muted);
        set
        {
            foreach (var volume in _sessions.OfType<ISimpleAudioVolume>()) volume.SetMute(value, ref _context);
        }
    }

    internal void Release()
    {
        foreach (var session in _sessions) CoreAudio.Release(session);
        _sessions.Clear();
    }
}

/// <summary>Mezclador de volumen: las aplicaciones con audio en la salida predeterminada.</summary>
public sealed class AudioSessions
{
    private readonly Dictionary<string, BitmapSource?> _icons = [];
    private List<AudioApp> _apps = [];

    /// <summary>
    /// Vuelve a leer las sesiones. Primero las que están sonando; se omiten "Sonidos del sistema"
    /// y la propia Zenit. Las anteriores se liberan.
    /// </summary>
    public IReadOnlyList<AudioApp> Refresh()
    {
        foreach (var app in _apps) app.Release();
        _apps = [];

        var manager = CoreAudio.ActivateOnDefaultOutput<IAudioSessionManager2>();
        if (manager is null) return _apps;
        IAudioSessionEnumerator? enumerator = null;
        try
        {
            if (manager.GetSessionEnumerator(out enumerator) != 0 || enumerator.GetCount(out var count) != 0) return _apps;

            var own = (uint)Environment.ProcessId;
            var byProcess = new Dictionary<uint, (List<object> sessions, bool active, string? name)>();
            for (var i = 0; i < count; i++)
            {
                if (enumerator.GetSession(i, out var session) != 0) continue;
                // 0 = inactiva, 1 = activa, 2 = expirada (la app ya cerró su audio).
                if (session.IsSystemSoundsSession() == 0 || session.GetState(out var state) != 0 || state == 2
                    || session.GetProcessId(out var pid) != 0 || pid == 0 || pid == own)
                {
                    CoreAudio.Release(session);
                    continue;
                }
                session.GetDisplayName(out var display);
                if (!byProcess.TryGetValue(pid, out var entry)) entry = ([], false, null);
                entry.sessions.Add(session);
                byProcess[pid] = (entry.sessions, entry.active || state == 1,
                    entry.name ?? (string.IsNullOrWhiteSpace(display) || display.StartsWith('@') ? null : display));
            }

            foreach (var (pid, (sessions, active, display)) in byProcess)
            {
                var (name, path) = Describe(pid);
                _apps.Add(new AudioApp(pid, display ?? name, IconOf(path), active, sessions));
            }
            _apps = _apps.OrderByDescending(a => a.IsActive).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        catch
        {
            // El dispositivo cambió mientras se leía: se mostrará en la próxima lectura.
        }
        finally
        {
            CoreAudio.Release(enumerator);
            CoreAudio.Release(manager);
        }
        return _apps;
    }

    /// <summary>Nombre amigable ("Google Chrome" en vez de "chrome") y ruta del ejecutable, si se puede leer.</summary>
    private static (string name, string? path) Describe(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            var path = NativeMethods.ProcessPath(pid);
            var description = path is null ? null : FileVersionInfo.GetVersionInfo(path).FileDescription;
            return (string.IsNullOrWhiteSpace(description) ? process.ProcessName : description.Trim(), path);
        }
        catch
        {
            return (Strings.T("Aplicación", "App"), null);
        }
    }

    private BitmapSource? IconOf(string? path)
    {
        if (path is null) return null;
        if (_icons.TryGetValue(path, out var cached)) return cached;
        BitmapSource? image = null;
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is not null)
            {
                image = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                image.Freeze();
            }
        }
        catch
        {
            // Sin ícono: la página muestra uno genérico.
        }
        return _icons[path] = image;
    }
}
