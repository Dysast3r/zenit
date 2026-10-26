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

/// <summary>Actividades en vivo (temporizador) y avisos breves: volumen, batería, notificaciones.</summary>
public partial class IslandWindow
{
    // ───────────────────────── Actividades en vivo y avisos ─────────────────────────

    private bool TimerLive => _settings.TimerLiveActivity && _timer.IsActive;

    /// <summary>Reunión que empieza dentro del margen de aviso (o acaba de empezar): se muestra como actividad en vivo.</summary>
    private CalendarEvent? MeetingLive => _settings.CalendarLiveActivity
        ? _services.Calendar.Upcoming(DateTime.Now, TimeSpan.FromMinutes(_settings.CalendarLeadMinutes))
        : null;

    private bool HasLiveActivity => TimerLive || MeetingLive is not null;

    /// <summary>"No molestar" de Windows activo y el usuario quiere respetarlo.</summary>
    private bool Quiet => _settings.RespectDoNotDisturb && NativeMethods.IsDoNotDisturbOn();

    /// <summary>
    /// Isla compacta: carátula (o ícono de la actividad) a la izquierda, el contenido elegido al centro,
    /// y ecualizador o cuenta regresiva a la derecha. El temporizador tiene prioridad sobre la reunión.
    /// </summary>
    private void UpdateCompact()
    {
        var timer = TimerLive;
        var meeting = timer ? null : MeetingLive;
        var activity = timer || meeting is not null;
        var music = _snapshot is not null;
        CompactArt.Visibility = music ? Visibility.Visible : Visibility.Hidden;
        CompactTimerIcon.Visibility = !music && activity ? Visibility.Visible : Visibility.Hidden;
        CompactBars.Visibility = music && !activity ? Visibility.Visible : Visibility.Hidden;
        CompactTimer.Visibility = activity ? Visibility.Visible : Visibility.Collapsed;
        SetCompactLyrics(music && !activity && _services.Lyrics.Lines.Count > 0
                         && _page < _pages.Count && _pages[_page] is LyricsPage);
        UpdateCompactCenter(meeting, narrow: activity);
        if (!activity) return;

        if (meeting is not null)
        {
            var minutes = (int)Math.Ceiling((meeting.Start - DateTime.Now).TotalMinutes);
            CompactTimerText.Text = minutes <= 0 ? Strings.T("Ahora", "Now") : Strings.F("{0} min", "{0} min", minutes);
            CompactTimerGlyph.Text = CompactTimerIconGlyph.Text = CalendarGlyph;
            CompactTimerText.Foreground = CompactTimerGlyph.Foreground = CompactTimerIconCircle.Fill = Blue;
            return;
        }

        var stopwatch = _timer.Mode == ClockTool.Stopwatch;
        CompactTimerText.Text = TimerService.Format(stopwatch ? _timer.Elapsed : _timer.Remaining);
        CompactTimerGlyph.Text = CompactTimerIconGlyph.Text = TimerGlyph;
        CompactTimerText.Foreground = CompactTimerGlyph.Foreground = stopwatch ? Green : Orange;
        CompactTimerIconCircle.Fill = stopwatch ? Green : Orange;
    }

    private const string TimerGlyph = "", CalendarGlyph = "";
    private const double MarqueeGap = 36, MarqueeSpeed = 28;
    private string _centerKey = "";
    private bool _marqueeNeeded, _marqueeRunning;

    /// <summary>
    /// Centro de la isla compacta. Con una reunión en curso muestra su título; si no, lo que el usuario eligió.
    /// El texto que no cabe avanza en marquesina, pero solo mientras la isla compacta está a la vista.
    /// </summary>
    private void UpdateCompactCenter(CalendarEvent? meeting, bool narrow)
    {
        var now = DateTime.Now;
        var weather = _weather.Reports.FirstOrDefault();
        var text = _compactLyrics ? "" : meeting?.Title ?? _settings.CompactCenter switch
        {
            CompactCenter.Time => TimeFormats.Time(now, _settings, seconds: false),
            CompactCenter.Date => TimeFormats.Date(now, DateStyle.Medium, ""),
            CompactCenter.Temperature => weather is null ? "" : TimeFormats.Temperature(weather.Temperature),
            CompactCenter.Track => _snapshot?.Title ?? "",
            _ => "",
        };
        var showGlyph = meeting is null && _settings.CompactCenter == CompactCenter.Temperature && weather is not null;
        // Con una actividad a la derecha (temporizador o reunión) queda menos espacio libre.
        var width = narrow ? 104.0 : 176.0;

        var key = $"{text}|{width}|{showGlyph}|{weather?.Kind}|{weather?.IsDay}";
        if (key == _centerKey)
        {
            ApplyMarquee();
            return;
        }
        _centerKey = key;

        CompactCenterHost.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        CompactCenterHost.Width = width;
        CompactCenterText.Text = CompactCenterEcho.Text = text;
        CompactCenterGlyph.Visibility = showGlyph ? Visibility.Visible : Visibility.Collapsed;
        if (showGlyph)
        {
            CompactCenterGlyph.Kind = weather!.Kind;
            CompactCenterGlyph.IsDay = weather.IsDay;
        }

        CompactCenterText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var contentWidth = CompactCenterText.DesiredSize.Width + (showGlyph ? 21 : 0);
        _marqueeNeeded = contentWidth > width;
        CompactCenterEcho.Visibility = _marqueeNeeded ? Visibility.Visible : Visibility.Collapsed;

        // Centrado si cabe; si no, alineado a la izquierda para que la marquesina parta desde el borde.
        CompactCenterContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(CompactCenterContent, _marqueeNeeded ? 0 : Math.Round((width - contentWidth) / 2));
        Canvas.SetTop(CompactCenterContent, Math.Round((CompactCenterHost.Height - CompactCenterContent.DesiredSize.Height) / 2));
        // Los bordes se desvanecen para que el texto entre y salga suave.
        CompactCenterHost.OpacityMask = _marqueeNeeded ? MarqueeMask : null;
        _marqueeRunning = false;
        CompactCenterShift.BeginAnimation(TranslateTransform.XProperty, null);
        CompactCenterShift.X = 0;
        ApplyMarquee();
    }

    private bool _compactLyrics;
    private int _compactLyricIndex = int.MinValue;

    /// <summary>Ancho de la isla compacta: más ancha cuando muestra la letra en vez del contenido elegido.</summary>
    private double CompactWidth() => _compactLyrics ? 390 : 284;

    /// <summary>Si la última página abierta fue Letras, la isla compacta sigue mostrando la línea que se canta.</summary>
    private void SetCompactLyrics(bool show)
    {
        if (show == _compactLyrics) return;
        _compactLyrics = show;
        _compactLyricIndex = int.MinValue;
        CompactLyric.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        CompactPanel.Width = CompactWidth();
        if (_mode == IslandMode.Compact)
        {
            _width.Target = CompactWidth();
            EnsureLoop();
        }
        UpdateCompactLyric();
    }

    /// <summary>Se revisa en cada frame, como la página de letras, para que el cambio caiga justo en el tiempo.</summary>
    private void UpdateCompactLyric()
    {
        var lines = _services.Lyrics.Lines;
        if (!_compactLyrics || _snapshot is not { } snapshot || lines.Count == 0) return;

        var index = _services.Lyrics.IndexAt(snapshot.CurrentPosition + LyricsService.Lead);
        if (index == _compactLyricIndex) return;
        var forward = index > _compactLyricIndex && _compactLyricIndex != int.MinValue;
        _compactLyricIndex = index;
        CompactLyric.Text = index >= 0 ? lines[index].Text : "♪";

        if (!forward) return;
        CompactLyricShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = EaseOut });
        CompactLyric.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = EaseOut });
    }

    private void ApplyMarquee()
    {
        var run = _marqueeNeeded && _mode == IslandMode.Compact;
        if (run == _marqueeRunning) return;
        _marqueeRunning = run;
        if (!run)
        {
            CompactCenterShift.BeginAnimation(TranslateTransform.XProperty, null);
            CompactCenterShift.X = 0;
            return;
        }

        // Pausa al inicio, recorre hasta que la copia queda donde estaba el original y repite sin salto.
        var distance = CompactCenterText.DesiredSize.Width + MarqueeGap;
        var hold = TimeSpan.FromSeconds(1.6);
        var travel = TimeSpan.FromSeconds(distance / MarqueeSpeed);
        var marquee = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        marquee.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        marquee.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(hold)));
        marquee.KeyFrames.Add(new LinearDoubleKeyFrame(-distance, KeyTime.FromTimeSpan(hold + travel)));
        Timeline.SetDesiredFrameRate(marquee, 30);
        CompactCenterShift.BeginAnimation(TranslateTransform.XProperty, marquee);
    }

    private static readonly Brush MarqueeMask = Freeze(new LinearGradientBrush(
        [new GradientStop(Colors.Transparent, 0), new GradientStop(Colors.Black, 0.1), new GradientStop(Colors.Black, 0.9), new GradientStop(Colors.Transparent, 1)],
        new Point(0, 0.5), new Point(1, 0.5)));

    private void OnTimerChanged()
    {
        UpdateCompact();
        Evaluate();
    }

    private void OnTimerCompleted()
        => ShowAlert(new IslandAlert("timer", Strings.T("¡Tiempo!", "Time's up!"),
            Strings.F("Temporizador de {0}", "{0} timer", TimerService.Format(_timer.Duration)), 6,
            Glyph: "", GlyphBackground: Orange), force: true);

    private const string VolumeGlyph = "", MutedGlyph = "";

    private void OnVolumeChanged(float level, bool muted)
    {
        if (!_settings.VolumeHud) return;
        var percent = (int)Math.Round(level * 100);
        var glyph = muted || percent == 0 ? MutedGlyph : VolumeGlyph;
        // Con la isla expandida no se interrumpe la página: la barra reemplaza un momento a los puntos.
        if (_mode == IslandMode.Expanded)
        {
            ShowExpandedVolume(muted ? 0 : level, glyph, muted ? "" : $"{percent}%");
            return;
        }
        ShowAlert(new IslandAlert("volume",
            muted ? Strings.T("Silencio", "Muted") : Strings.T("Volumen", "Volume"),
            muted ? "" : $"{percent}%", 1.6,
            Glyph: glyph, GlyphBackground: Subtle, Progress: muted ? 0 : level));
    }

    private void ShowExpandedVolume(float level, string glyph, string percent)
    {
        var shown = ExpandedVolume.Opacity > 0.5;
        ExpandedVolumeGlyph.Text = glyph;
        ExpandedVolumeText.Text = percent;
        var width = ExpandedVolumeTrack.Width * Math.Clamp(level, 0, 1);
        if (shown) ExpandedVolumeFill.BeginAnimation(WidthProperty, new DoubleAnimation(width, TimeSpan.FromMilliseconds(160)) { EasingFunction = EaseOut });
        else
        {
            ExpandedVolumeFill.BeginAnimation(WidthProperty, null);
            ExpandedVolumeFill.Width = width;
        }

        var fade = TimeSpan.FromMilliseconds(180);
        ExpandedVolume.BeginAnimation(OpacityProperty, new DoubleAnimation(1, fade) { EasingFunction = EaseOut });
        DotsHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, fade) { EasingFunction = EaseOut });
        _expandedVolumeTimer.Stop();
        _expandedVolumeTimer.Start();
    }

    private void HideExpandedVolume()
    {
        var fade = TimeSpan.FromMilliseconds(260);
        ExpandedVolume.BeginAnimation(OpacityProperty, new DoubleAnimation(0, fade) { EasingFunction = EaseOut });
        DotsHost.BeginAnimation(OpacityProperty, new DoubleAnimation(1, fade) { EasingFunction = EaseOut });
    }

    private void OnChargingStarted(float percent)
    {
        if (_settings.BatteryAlerts && !Quiet)
            ShowAlert(new IslandAlert("battery", Strings.T("Cargando", "Charging"), $"{percent * 100:0}%", 3,
                Glyph: "", GlyphBackground: Green, Progress: percent));
    }

    private void OnBatteryLow(float percent)
    {
        if (_settings.BatteryAlerts)
            ShowAlert(new IslandAlert("battery", Strings.T("Batería baja", "Low battery"), $"{percent * 100:0}%", 4,
                Glyph: "", GlyphBackground: Red, Progress: percent));
    }

    private void OnNotification(IslandNotification notification)
    {
        if (!_settings.ShowNotifications) return;
        var subtitle = string.IsNullOrWhiteSpace(notification.Body) ? notification.App : $"{notification.App} · {notification.Body}";
        // Mensajes de servicios web (WhatsApp Web, Gmail…) llegan a nombre del navegador: se muestran con su marca.
        Brush? background = notification.Brand is { } brand ? Frozen(brand) : _accent;
        var glyph = notification.Glyph ?? (notification.Icon is null ? "" : null);
        ShowAlert(new IslandAlert("notification", notification.Title, subtitle, 5,
            Glyph: glyph, GlyphBackground: background,
            Image: glyph is not null || notification.Icon is null ? null : Freeze(new ImageBrush(notification.Icon) { Stretch = Stretch.UniformToFill })));
    }

    // ───────────────────────── Calendario ─────────────────────────

    private readonly HashSet<string> _announcedMeetings = [];
    private bool _meetingWasLive;

    private void OnCalendarUpdated()
    {
        UpdateCompact();
        CheckMeeting();
    }

    /// <summary>
    /// Anuncia la próxima reunión al entrar en el margen de aviso y otra vez al empezar (una vez cada una),
    /// y pasa la isla a compacta mientras dura la actividad.
    /// </summary>
    private void CheckMeeting()
    {
        var meeting = MeetingLive;
        if ((meeting is not null) != _meetingWasLive)
        {
            _meetingWasLive = meeting is not null;
            UpdateCompact();
            Evaluate();
        }
        if (meeting is null) return;

        var minutes = (int)Math.Ceiling((meeting.Start - DateTime.Now).TotalMinutes);
        var phase = minutes > 0 ? "soon" : "now";
        if (!_announcedMeetings.Add($"{meeting.Title}|{meeting.Start:O}|{phase}")) return;

        var subtitle = minutes > 0
            ? Strings.F("En {0} min · {1}", "In {0} min · {1}", minutes, TimeFormats.Time(meeting.Start, _settings, seconds: false))
            : Strings.T("Empieza ahora", "Starting now");
        ShowAlert(new IslandAlert("calendar", meeting.Title, subtitle, 5, Glyph: CalendarGlyph, GlyphBackground: Blue));
    }

    private void UpdatePrivacyDots()
    {
        var show = _settings.PrivacyIndicators;
        CameraDot.Visibility = show && _services.Indicators.CameraInUse ? Visibility.Visible : Visibility.Collapsed;
        MicDot.Visibility = show && _services.Indicators.MicrophoneInUse ? Visibility.Visible : Visibility.Collapsed;
        ApplyGeometry();
    }

    /// <summary>Muestra un aviso; si ya hay uno del mismo tipo a la vista, solo actualiza su contenido.</summary>
    private void ShowAlert(IslandAlert alert, bool force = false)
    {
        if (!force && _mode == IslandMode.Expanded) return;
        if (_fullscreen && _settings.HideInFullscreen) return;
        // En "No molestar" solo pasa lo que pidió el usuario (volumen) y las alarmas (temporizador, batería baja).
        if (!force && alert.Kind is "notification" or "music" or "calendar" && Quiet) return;

        var sameKind = _alert?.Kind == alert.Kind && _mode == IslandMode.Peek;
        _alert = alert;
        if (force) _expandRequested = false;

        PeekTitle.Text = alert.Title;
        PeekArtist.Text = alert.Subtitle;
        PeekArtist.Visibility = alert.Subtitle.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        PeekArt.Background = alert.Image ?? alert.GlyphBackground ?? _artBrush;
        PeekArt.CornerRadius = new CornerRadius(alert.Glyph is null ? 12 : 24);
        PeekGlyph.Text = alert.Glyph ?? "";
        PeekGlyph.Visibility = alert.Glyph is null ? Visibility.Collapsed : Visibility.Visible;
        PeekBars.Visibility = alert.ShowBars ? Visibility.Visible : Visibility.Collapsed;
        PeekProgress.Visibility = alert.Progress is null ? Visibility.Collapsed : Visibility.Visible;
        if (alert.Progress is { } progress)
        {
            var width = 84 * Math.Clamp(progress, 0, 1);
            if (sameKind) PeekProgressFill.BeginAnimation(WidthProperty, new DoubleAnimation(width, TimeSpan.FromMilliseconds(160)) { EasingFunction = EaseOut });
            else
            {
                PeekProgressFill.BeginAnimation(WidthProperty, null);
                PeekProgressFill.Width = width;
            }
        }

        _peekUntil = DateTime.UtcNow.AddSeconds(alert.Seconds);
        _peekTimer.Interval = TimeSpan.FromSeconds(alert.Seconds);
        _peekTimer.Stop();
        _peekTimer.Start();
        Evaluate();
        EnsureLoop();
    }
}
