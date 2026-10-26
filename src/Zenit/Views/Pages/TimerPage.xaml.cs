using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Zenit.Services;

namespace Zenit.Views.Pages;

public partial class TimerPage : UserControl, IIslandPage
{
    private static readonly int[] PresetMinutes = [1, 5, 15, 25];
    private static readonly Brush Orange = Frozen(Color.FromRgb(0xFF, 0x9F, 0x0A));
    private static readonly Brush Green = Frozen(Color.FromRgb(0x30, 0xD1, 0x58));
    private static readonly Brush TabActive = Frozen(Colors.White);
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
        SetTab(TimerTab, timerMode);
        SetTab(StopwatchTab, !timerMode);
        Presets.Visibility = timerMode ? Visibility.Visible : Visibility.Collapsed;
        RingGlyph.Text = timerMode ? "" : "";
        Ring.Stroke = timerMode ? Orange : Green;
        StartButton.Background = timerMode ? Orange : Green;
        StartButton.Content = _timer.IsRunning ? "" : "";
        ResetButton.IsEnabled = _timer.IsActive;
        Animate(0);
    }

    /// <summary>El tiempo se redibuja cada frame mientras la página está visible.</summary>
    public void Animate(double time)
    {
        if (_timer.Mode == ClockTool.Timer)
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
        tab.Foreground = active ? Brushes.Black : Brushes.White;
    }

    private void TimerTab_Click(object sender, RoutedEventArgs e) => _timer.SetMode(ClockTool.Timer);
    private void StopwatchTab_Click(object sender, RoutedEventArgs e) => _timer.SetMode(ClockTool.Stopwatch);
    private void Start_Click(object sender, RoutedEventArgs e) => _timer.Toggle();
    private void Reset_Click(object sender, RoutedEventArgs e) => _timer.Reset();

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
