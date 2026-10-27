using System.Diagnostics;
using System.Media;
using System.Windows.Threading;

namespace Zenit.Services;

public enum ClockTool { Timer, Stopwatch, Pomodoro }

public enum PomodoroPhase { Focus, ShortBreak, LongBreak }

/// <summary>
/// Temporizador, cronómetro y Pomodoro, que siguen corriendo aunque la isla esté compacta (actividad en vivo).
/// El Pomodoro alterna 25 min de enfoque con 5 de descanso, y uno largo de 15 cada 4 pomodoros.
/// </summary>
public sealed class TimerService
{
    public const int PomodorosPerCycle = 4;
    private static readonly TimeSpan FocusTime = TimeSpan.FromMinutes(25);
    private static readonly TimeSpan ShortBreakTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LongBreakTime = TimeSpan.FromMinutes(15);

    private readonly DispatcherTimer _tick;
    private readonly Stopwatch _watch = new();
    private TimeSpan _accumulated;
    private TimeSpan _timerDuration = TimeSpan.FromMinutes(5);

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

    /// <summary>El temporizador o una fase del Pomodoro llegó a cero.</summary>
    public event Action? Completed;

    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Focus;

    /// <summary>Fase del Pomodoro que acaba de terminar (para el aviso de <see cref="Completed"/>).</summary>
    public PomodoroPhase CompletedPhase { get; private set; }

    /// <summary>Pomodoros de enfoque terminados en el ciclo actual (0..3); al cuarto viene el descanso largo.</summary>
    public int CycleCount { get; private set; }

    public PomodoroStats Stats { get; } = PomodoroStats.Load();

    public static TimeSpan DurationOf(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.ShortBreak => ShortBreakTime,
        PomodoroPhase.LongBreak => LongBreakTime,
        _ => FocusTime,
    };

    public void SetMode(ClockTool mode)
    {
        if (mode == Mode) return;
        Reset();
        // El Pomodoro usa sus propias duraciones; el temporizador recupera la que tenía.
        if (Mode == ClockTool.Timer) _timerDuration = Duration;
        Mode = mode;
        Duration = mode == ClockTool.Pomodoro ? DurationOf(Phase) : _timerDuration;
        Changed?.Invoke();
    }

    public void SetDuration(TimeSpan duration)
    {
        Reset();
        Mode = ClockTool.Timer;
        Duration = _timerDuration = duration;
        Changed?.Invoke();
    }

    /// <summary>Pasa a la fase siguiente del Pomodoro sin contar la actual.</summary>
    public void SkipPhase()
    {
        if (Mode != ClockTool.Pomodoro) return;
        Reset();
        AdvancePhase(completed: false);
        Changed?.Invoke();
    }

    private void AdvancePhase(bool completed)
    {
        if (Phase == PomodoroPhase.Focus)
        {
            if (completed) Stats.Record(Duration);
            CycleCount++;
            Phase = CycleCount >= PomodorosPerCycle ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak;
        }
        else
        {
            if (Phase == PomodoroPhase.LongBreak) CycleCount = 0;
            Phase = PomodoroPhase.Focus;
        }
        Duration = DurationOf(Phase);
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
            if (Mode != ClockTool.Stopwatch && Remaining == TimeSpan.Zero) _accumulated = TimeSpan.Zero;
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
        if (Mode == ClockTool.Stopwatch || Remaining > TimeSpan.Zero) return;
        _accumulated = Duration;
        _watch.Reset();
        _tick.Stop();
        SystemSounds.Asterisk.Play();
        if (Mode == ClockTool.Pomodoro)
        {
            // La fase siguiente queda lista, pero en pausa: empieza cuando el usuario quiera.
            CompletedPhase = Phase;
            AdvancePhase(completed: true);
            _accumulated = TimeSpan.Zero;
        }
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
