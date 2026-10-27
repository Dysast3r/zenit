using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Zenit.Controls;
using Zenit.Services;

namespace Zenit.Views.Pages;

public partial class TimerPage : UserControl, IIslandPage
{
    private static readonly int[] PresetMinutes = [1, 5, 15, 25];
    private static readonly Brush Orange = Frozen(Color.FromRgb(0xFF, 0x9F, 0x0A));
    private static readonly Brush Green = Frozen(Color.FromRgb(0x30, 0xD1, 0x58));
    private static readonly Brush Red = Frozen(Color.FromRgb(0xFF, 0x45, 0x3A));
    private static readonly Brush BarActive = IslandTheme.InkAlpha(0x99);
    private static readonly Brush BarEmpty = IslandTheme.InkAlpha(0x26);
    private static readonly Brush TabActive = IslandTheme.Ink;
    private static readonly Brush TabIdle = Frozen(Colors.Transparent);

    private readonly TimerService _timer;

    public TimerPage(TimerService timer)
    {
        _timer = timer;
        InitializeComponent();

        foreach (var minutes in PresetMinutes)
        {
            var chip = new Button { Style = (Style)FindResource("ChipButton"), Content = $"{minutes}m", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 5, 0) };
            chip.Click += (_, _) => _timer.SetDuration(TimeSpan.FromMinutes(minutes));
            Presets.Children.Add(chip);
        }
        var plus = new Button { Style = (Style)FindResource("ChipButton"), Content = "+1m", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0) };
        plus.Click += (_, _) => _timer.AddMinute();
        Presets.Children.Add(plus);

        _timer.Changed += Refresh;
        Refresh();
    }

    public PageKind Kind => PageKind.Timer;

    public void Refresh()
    {
        var timerMode = _timer.Mode == ClockTool.Timer;
        var pomodoro = _timer.Mode == ClockTool.Pomodoro;
        SetTab(TimerTab, timerMode);
        SetTab(StopwatchTab, _timer.Mode == ClockTool.Stopwatch);
        SetTab(PomodoroTab, pomodoro);
        Presets.Visibility = timerMode ? Visibility.Visible : Visibility.Collapsed;
        PomodoroStats.Visibility = PhaseText.Visibility = RingText.Visibility = pomodoro ? Visibility.Visible : Visibility.Collapsed;
        RingGlyph.Visibility = pomodoro ? Visibility.Collapsed : Visibility.Visible;
        RingGlyph.Text = timerMode ? "" : "";

        var accent = pomodoro ? PhaseBrush(_timer.Phase) : timerMode ? Orange : Green;
        Ring.Stroke = StartButton.Background = accent;
        StartButton.Content = _timer.IsRunning ? "" : "";
        if (pomodoro)
        {
            // Sin empezar, el botón salta a la fase siguiente; con la fase en marcha, la reinicia.
            ResetButton.IsEnabled = true;
            ResetButton.Content = _timer.IsActive ? "" : "";
            ResetButton.ToolTip = _timer.IsActive ? Strings.T("Reiniciar", "Restart") : Strings.T("Saltar fase", "Skip phase");
            RefreshPomodoro();
        }
        else
        {
            ResetButton.IsEnabled = _timer.IsActive;
            ResetButton.Content = "";
            ResetButton.ToolTip = null;
        }
        Animate(0);
    }

    private void RefreshPomodoro()
    {
        var phase = _timer.Phase;
        PhaseText.Foreground = PhaseBrush(phase);
        PhaseText.Text = phase switch
        {
            PomodoroPhase.ShortBreak => Strings.T("Descanso corto", "Short break"),
            PomodoroPhase.LongBreak => Strings.T("Descanso largo", "Long break"),
            _ => Strings.T("Enfoque", "Focus"),
        };
        RingText.Text = phase == PomodoroPhase.Focus ? $"{_timer.CycleCount + 1}/{TimerService.PomodorosPerCycle}" : "☕";

        var stats = _timer.Stats;
        var today = stats.Today;
        TodayText.Text = Strings.F("Hoy: {0} · {1}", "Today: {0} · {1}",
            today.Sessions == 1 ? "1 pomodoro" : Strings.F("{0} pomodoros", "{0} pomodoros", today.Sessions),
            FormatMinutes(today.Minutes));
        var streak = stats.Streak;
        StreakText.Text = streak switch
        {
            0 => Strings.T("Aún sin racha", "No streak yet"),
            1 => Strings.T("Racha: 1 día", "Streak: 1 day"),
            _ => Strings.F("Racha: {0} días", "Streak: {0} days", streak),
        };

        // Barras de los últimos 7 días, relativas al mejor día; hoy va con el color del enfoque.
        WeekBars.Children.Clear();
        var days = stats.LastDays(7);
        var best = Math.Max(1, days.Max(d => d.Minutes));
        foreach (var day in days)
        {
            WeekBars.Children.Add(new Border
            {
                Width = 6, Margin = new Thickness(2, 0, 0, 0), CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = Math.Max(3, 26.0 * day.Minutes / best),
                Background = day.Date == today.Date ? Red : day.Minutes > 0 ? BarActive : BarEmpty,
                ToolTip = $"{day.Date.ToString("ddd d", CultureInfo.CurrentCulture)}: {FormatMinutes(day.Minutes)}",
            });
        }
    }

    private static string FormatMinutes(int minutes)
        => minutes >= 60 ? $"{minutes / 60} h {minutes % 60:00} min" : $"{minutes} min";

    private static Brush PhaseBrush(PomodoroPhase phase) => phase == PomodoroPhase.Focus ? Red : Green;

    /// <summary>El tiempo se redibuja cada frame mientras la página está visible.</summary>
    public void Animate(double time)
    {
        if (_timer.Mode != ClockTool.Stopwatch)
        {
            TimeText.Text = TimerService.Format(_timer.IsActive ? _timer.Remaining : _timer.Duration);
            Ring.Progress = _timer.IsActive ? 1 - _timer.Progress : 1;
        }
        else
        {
            var elapsed = _timer.Elapsed;
            TimeText.Text = TimerService.Format(elapsed, centiseconds: true);
            Ring.Progress = elapsed.TotalSeconds % 60 / 60;
        }
    }

    private static void SetTab(Button tab, bool active)
    {
        tab.Background = active ? TabActive : TabIdle;
        tab.Foreground = active ? IslandTheme.Background : IslandTheme.Ink;
    }

    private void TimerTab_Click(object sender, RoutedEventArgs e) => _timer.SetMode(ClockTool.Timer);
    private void StopwatchTab_Click(object sender, RoutedEventArgs e) => _timer.SetMode(ClockTool.Stopwatch);
    private void PomodoroTab_Click(object sender, RoutedEventArgs e) => _timer.SetMode(ClockTool.Pomodoro);
    private void Start_Click(object sender, RoutedEventArgs e) => _timer.Toggle();

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (_timer.Mode == ClockTool.Pomodoro && !_timer.IsActive) _timer.SkipPhase();
        else _timer.Reset();
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
