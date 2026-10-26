using System.Runtime.InteropServices;
using System.Windows.Threading;
using Zenit.Interop;

namespace Zenit.Services;

/// <summary>Volumen del sistema: avisa cuando cambia (para el indicador en pantalla) y permite ajustarlo.</summary>
public sealed class VolumeService : IAudioEndpointVolumeCallback, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _refresh;
    private IAudioEndpointVolume? _endpoint;
    private Guid _ownContext = Guid.NewGuid();

    /// <summary>Volumen 0..1 y silencio. Se dispara en el hilo de la UI.</summary>
    public event Action<float, bool>? Changed;

    public VolumeService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        Attach();
        // El dispositivo predeterminado puede cambiar (audífonos, Bluetooth): se reengancha cada cierto tiempo.
        _refresh = new DispatcherTimer(TimeSpan.FromSeconds(10), DispatcherPriority.Background, (_, _) => Attach(), dispatcher);
    }

    public bool IsAvailable => _endpoint is not null;

    public (float level, bool muted) Current
    {
        get
        {
            if (_endpoint is null) return (0, false);
            _endpoint.GetMasterVolumeLevelScalar(out var level);
            _endpoint.GetMute(out var muted);
            return (level, muted);
        }
    }

    /// <summary>Sube o baja el volumen; <paramref name="delta"/> en fracción (0.02 = 2 %).</summary>
    public void Adjust(float delta)
    {
        if (_endpoint is null) return;
        try
        {
            var (level, muted) = Current;
            var next = Math.Clamp(level + delta, 0f, 1f);
            if (muted && delta > 0) _endpoint.SetMute(false, ref _ownContext);
            _endpoint.SetMasterVolumeLevelScalar(next, ref _ownContext);
        }
        catch
        {
            Detach();
        }
    }

    /// <summary>Fija el volumen del sistema (0..1); subirlo quita el silencio.</summary>
    public void Set(float level)
    {
        if (_endpoint is null) return;
        try
        {
            if (Current.muted && level > 0) _endpoint.SetMute(false, ref _ownContext);
            _endpoint.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref _ownContext);
        }
        catch
        {
            Detach();
        }
    }

    public void ToggleMute()
    {
        if (_endpoint is null) return;
        try
        {
            _endpoint.SetMute(!Current.muted, ref _ownContext);
        }
        catch
        {
            Detach();
        }
    }

    int IAudioEndpointVolumeCallback.OnNotify(IntPtr notifyData)
    {
        // AUDIO_VOLUME_NOTIFICATION_DATA: Guid (16) · BOOL muted (4) · float master (4) · ...
        var muted = Marshal.ReadInt32(notifyData, 16) != 0;
        var level = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(notifyData, 20));
        _dispatcher.BeginInvoke(() => Changed?.Invoke(level, muted));
        return 0;
    }

    private void Attach()
    {
        Detach();
        _endpoint = CoreAudio.ActivateOnDefaultOutput<IAudioEndpointVolume>();
        try
        {
            _endpoint?.RegisterControlChangeNotify(this);
        }
        catch
        {
            CoreAudio.Release(_endpoint);
            _endpoint = null;
        }
    }

    private void Detach()
    {
        if (_endpoint is null) return;
        try
        {
            _endpoint.UnregisterControlChangeNotify(this);
        }
        catch
        {
            // El dispositivo ya no existe.
        }
        CoreAudio.Release(_endpoint);
        _endpoint = null;
    }

    public void Dispose()
    {
        _refresh.Stop();
        Detach();
    }
}
