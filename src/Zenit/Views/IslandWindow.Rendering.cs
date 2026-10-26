using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Zenit.Animation;
using Zenit.Controls;
using Zenit.Interop;
using Zenit.Services;
using Zenit.Views.Pages;
using Microsoft.Win32;

namespace Zenit.Views;

/// <summary>Bucle de animación: resortes, forma, ecualizador y barra de progreso.</summary>
public partial class IslandWindow
{
    // ───────────────────────── Bucle de animación ─────────────────────────

    private void EnsureLoop()
    {
        if (_loopAttached) return;
        _loopAttached = true;
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (now == _lastFrame) return;

        // Los cambios de forma van a la tasa completa del monitor; lo demás (ecualizador, progreso,
        // íconos) se limita para no redibujar la ventana transparente 144 veces por segundo.
        var shaping = _pointerDown || !_springs.All(s => s.IsSettled);
        var minInterval = shaping ? 0 : _mode == IslandMode.Expanded ? 1 / 60.0 : 1 / 30.0;
        if (_lastFrame != TimeSpan.Zero && (now - _lastFrame).TotalSeconds < minInterval - 0.002) return;

        var dt = _lastFrame == TimeSpan.Zero ? 1 / 60.0 : Math.Clamp((now - _lastFrame).TotalSeconds, 0, 1 / 20.0);
        _lastFrame = now;
        _time += dt;

        foreach (var spring in _springs) spring.Step(dt);
        ApplyGeometry();

        var barsActive = UpdateBars(dt);
        if (_mode == IslandMode.Compact) UpdateCompactLyric();
        if (_mode == IslandMode.Expanded)
        {
            UpdateProgress();
            // Solo se animan las páginas que están a la vista (o entrando con el deslizamiento).
            var position = -_pager.Value / PageWidth;
            for (var i = 0; i < _pages.Count; i++)
                if (Math.Abs(i - position) < 1 && _pages[i] is IIslandPage page) page.Animate(_time);
        }

        var settled = _springs.All(s => s.IsSettled) && !_pointerDown;
        if (settled && !barsActive && _mode != IslandMode.Expanded)
        {
            CompositionTarget.Rendering -= OnRendering;
            _loopAttached = false;
        }
    }

    private void ApplyGeometry()
    {
        var w = Math.Max(1, _width.Value);
        var h = Math.Max(1, _height.Value);
        var r = Math.Max(0, _radius.Value);
        var box = w + 2 * _ear;

        var shape = IslandShape.Build(w, h, r, _ear);
        Island.Width = GlowShape.Width = box;
        Island.Height = GlowShape.Height = h;
        IslandFill.Data = GlowShape.Shape = shape;
        Island.Clip = shape;

        foreach (var panel in _panels)
        {
            if (panel.Visibility != Visibility.Visible) continue;
            Canvas.SetLeft(panel, _ear + (w - panel.Width) / 2);
            Canvas.SetTop(panel, (h - panel.Height) / 2);
        }
        PositionPrivacyDots(w, h);

        HostScale.ScaleX = HostScale.ScaleY = _scale.Value;
        HostShift.X = _shift.Value;
        PagerShift.X = _pager.Value;

        var position = -_pager.Value / PageWidth;
        for (var i = 0; i < _dots.Count; i++)
            _dots[i].Opacity = 0.3 + 0.7 * Math.Max(0, 1 - Math.Abs(position - i));
    }

    /// <summary>Puntos de cámara (verde) y micrófono (naranja) en el borde derecho, como en el iPhone.</summary>
    private void PositionPrivacyDots(double w, double h)
    {
        if (CameraDot.Visibility != Visibility.Visible && MicDot.Visibility != Visibility.Visible) return;
        var (x, y) = _mode switch
        {
            IslandMode.Compact => (_ear + w - 46, h / 2 - 3),
            IslandMode.Expanded => (_ear + w - 34, 14.0),
            IslandMode.Peek => (_ear + w - 20, 10.0),
            _ => (_ear + w - h / 2 - 3, h / 2 - 3),
        };
        Canvas.SetLeft(CameraDot, x);
        Canvas.SetTop(CameraDot, y);
        Canvas.SetLeft(MicDot, CameraDot.Visibility == Visibility.Visible ? x - 10 : x);
        Canvas.SetTop(MicDot, y);
    }

    private static readonly double[] BarFrequency = [5.1, 7.3, 6.2, 8.7];
    private static readonly double[] BarPhase = [0.0, 1.7, 3.1, 4.4];

    private bool UpdateBars(double dt)
    {
        var levels = EqualizerBars.Levels;
        var playing = _snapshot?.IsPlaying == true && _mode != IslandMode.Hidden;
        var active = playing;

        double energy = 0;
        if (playing)
        {
            var peak = _settings.ReactiveEqualizer && !_demo ? _meter.GetPeak() : -1;
            energy = peak < 0 ? 0.7 : Math.Clamp(Math.Pow(peak, 0.6) * 1.3, 0, 1);
        }

        for (var i = 0; i < levels.Length; i++)
        {
            double target = 0;
            if (playing)
            {
                var wobble = 0.5 + 0.25 * Math.Sin(_time * BarFrequency[i] + BarPhase[i])
                                 + 0.25 * Math.Sin(_time * BarFrequency[i] * 1.9 + BarPhase[i] * 2.3);
                // Piso con movimiento suave: aunque el volumen sea bajo, se nota que está sonando.
                target = Math.Max(0.2 + 0.25 * wobble, energy * (0.35 + 0.65 * wobble));
            }

            // Sube rápido, baja lento: así se ve como un vúmetro real.
            var rate = target > levels[i] ? 30 : 10;
            levels[i] += (target - levels[i]) * (1 - Math.Exp(-dt * rate));
            if (levels[i] > 0.005) active = true;
        }

        foreach (var bars in _bars)
            if (bars.IsVisible) bars.InvalidateVisual();
        return active;
    }

    private void UpdateProgress()
    {
        var snapshot = _snapshot;
        var duration = snapshot?.Duration ?? TimeSpan.Zero;
        var position = snapshot is null ? TimeSpan.Zero
            : _scrubbing ? TimeSpan.FromTicks((long)(duration.Ticks * _scrubRatio))
            : snapshot.CurrentPosition;

        var ratio = duration > TimeSpan.Zero ? Math.Clamp(position / duration, 0, 1) : 0;
        TrackFill.Width = ratio * SeekTrack.ActualWidth;

        var elapsed = Format(position);
        var remaining = "-" + Format(duration - position);
        if (elapsed != _elapsedCache) ElapsedText.Text = _elapsedCache = elapsed;
        if (remaining != _remainingCache) RemainingText.Text = _remainingCache = remaining;
    }

    private static string Format(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }
}
