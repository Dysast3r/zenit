using System.Diagnostics;
using System.Media;
using System.Windows.Threading;

namespace Zenit.Services;

public enum ClockTool { Timer, Stopwatch }

/// <summary>Temporizador y cronómetro que siguen corriendo aunque la isla esté compacta (actividad en vivo).</summary>
public sealed class TimerService
{
    private readonly DispatcherTimer _tick;
    private readonly Stopwatch _watch = new();
    private TimeSpan _accumulated;

    public TimerService(Dispatcher dispatcher)
    {
        _tick = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, (_, _) => OnTick(), dispatcher);
        // Este constructor arranca el timer: solo debe correr con el temporizador o el cronómetro en marcha (Toggle).
        _tick.Stop();
    }

    public ClockTool Mode { get; private set; } = ClockTool.Timer;
    public TimeSpan Duration { get; private set; } = TimeSpan.FromMinutes(5);
    public bool IsRunning => _watch.IsRunning;
    public bool IsActive => IsRunning || _accumulated > TimeSpan.Zero;

    public TimeSpan Elapsed => _accumulated + _watch.Elapsed;

    public TimeSpan Remaining
    {
        get
        {
            var left = Duration - Elapsed;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    /// <summary>Progreso 0..1 del temporizador.</summary>
    public double Progress => Duration > TimeSpan.Zero ? Math.Clamp(Elapsed / Duration, 0, 1) : 0;

    /// <summary>Cambió cualquier estado (inicio, pausa, modo, duración).</summary>
    public event Action? Changed;

    /// <summary>El temporizador llegó a cero.</summary>
    public event Action? Completed;

    public void SetMode(ClockTool mode)
    {
        if (mode == Mode) return;
        Reset();
        Mode = mode;
        Changed?.Invoke();
    }

    public void SetDuration(TimeSpan duration)
    {
        Reset();
        Mode = ClockTool.Timer;
        Duration = duration;
        Changed?.Invoke();
    }

    public void AddMinute()
    {
        Duration += TimeSpan.FromMinutes(1);
        Changed?.Invoke();
    }

    public void Toggle()
    {
        if (IsRunning)
        {
            _accumulated += _watch.Elapsed;
            _watch.Reset();
            _tick.Stop();
        }
        else
        {
            if (Mode == ClockTool.Timer && Remaining == TimeSpan.Zero) _accumulated = TimeSpan.Zero;
            _watch.Start();
            _tick.Start();
        }
        Changed?.Invoke();
    }

    public void Reset()
    {
        _watch.Reset();
        _accumulated = TimeSpan.Zero;
        _tick.Stop();
        Changed?.Invoke();
    }

    private void OnTick()
    {
        if (Mode != ClockTool.Timer || Remaining > TimeSpan.Zero) return;
        _accumulated = Duration;
        _watch.Reset();
        _tick.Stop();
        SystemSounds.Asterisk.Play();
        Changed?.Invoke();
        Completed?.Invoke();
    }

    public static string Format(TimeSpan time, bool centiseconds = false)
    {
        var text = time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
        return centiseconds ? $"{text},{time.Milliseconds / 10:00}" : text;
    }
}
