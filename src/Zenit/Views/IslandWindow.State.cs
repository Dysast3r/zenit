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

/// <summary>Máquina de estados de la isla (reposo, compacta, aviso, expandida) y su contenido.</summary>
public partial class IslandWindow
{
    /// <summary>Clic en el icono de la bandeja: abre o cierra la isla.</summary>
    public void Toggle() => RequestExpand(_mode != IslandMode.Expanded);

    // ───────────────────────── Estado ─────────────────────────

    private void OnMediaChangedFromService(MediaSnapshot? snapshot)
        => Dispatcher.BeginInvoke(() => OnMediaChanged(snapshot));

    /// <summary>Segundos que la isla sigue en modo música después de pausar, antes de volver al reposo.</summary>
    private const int MusicPauseGraceSeconds = 5;

    /// <summary>
    /// Hay música que mostrar: está sonando o se pausó hace poco. Una app abierta con una canción cargada
    /// pero sin reproducir (Spotify al iniciarse) no cuenta.
    /// </summary>
    private bool MusicLive => _snapshot is not null && _musicActive;

    /// <summary>Hay un juego, video o presentación a pantalla completa en esta pantalla y el usuario pidió ocultarla.</summary>
    private bool HiddenByFullscreen => _fullscreen && _settings.HideInFullscreen;

    /// <summary>
    /// Segundos que se espera antes de dar por cerrada la app de música. Al pausar, algunas apps cierran su
    /// sesión un momento y la vuelven a abrir (o Windows la publica sin título): ese hueco no debe ocultar la isla.
    /// </summary>
    private const int SessionLostGraceSeconds = 3;

    private void OnMediaChanged(MediaSnapshot? snapshot)
    {
        if (snapshot is null && _snapshot is not null)
        {
            if (!_sessionLostTimer.IsEnabled) _sessionLostTimer.Start();
            return;
        }
        _sessionLostTimer.Stop();
        ApplyMedia(snapshot);
    }

    private void ApplyMedia(MediaSnapshot? snapshot)
    {
        var previousKey = _trackKey;
        _snapshot = snapshot;
        _trackKey = snapshot?.TrackKey;
        var trackChanged = _trackKey != previousKey;

        var wasActive = _musicActive;
        if (snapshot?.IsPlaying == true)
        {
            _musicActive = true;
            _musicPauseTimer.Stop();
        }
        else if (snapshot is null)
        {
            _musicActive = false;
            _musicPauseTimer.Stop();
        }
        else if (_musicActive && !_musicPauseTimer.IsEnabled)
        {
            _musicPauseTimer.Start();
        }

        if (trackChanged || !ReferenceEquals(snapshot?.Art, _appliedArt)) ApplyArt(snapshot);
        if (trackChanged) ApplyTexts(snapshot);
        UpdatePlayGlyph(snapshot?.IsPlaying ?? false);
        UpdateCompact();

        PreviousButton.IsEnabled = snapshot?.CanPrevious ?? false;
        NextButton.IsEnabled = snapshot?.CanNext ?? false;
        PlayButton.IsEnabled = snapshot is not null;
        ApplyAccent();

        if (trackChanged && snapshot is not null)
        {
            LoadLyricsIfNeeded();
            if (_mode == IslandMode.Expanded) PopArt();
        }

        // El aviso de canción sale cuando empieza a sonar algo (o cambia de tema mientras suena),
        // no cuando una app abre con una canción cargada en pausa.
        if (snapshot is { IsPlaying: true } && (trackChanged || !wasActive) && _mode != IslandMode.Expanded && _settings.ShowTrackPeek)
            ShowAlert(new IslandAlert("music", snapshot.Title, snapshot.Artist, _settings.PeekSeconds, Image: _artBrush, ShowBars: true));

        Evaluate();
        UpdateProgress();
        EnsureLoop();
    }

    private void RequestExpand(bool expand)
    {
        _hoverTimer.Stop();
        _collapseTimer.Stop();
        _expandRequested = expand;
        if (expand)
        {
            _alert = null;
            _peekUntil = DateTime.MinValue;
            RefreshPages();
        }
        Evaluate();
    }

    private void Evaluate()
    {
        IslandMode next;
        if (HiddenByFullscreen) next = IslandMode.Hidden;
        else if (_expandRequested) next = IslandMode.Expanded;
        else if (_alert is not null && DateTime.UtcNow < _peekUntil) next = IslandMode.Peek;
        else if (MusicLive || HasLiveActivity) next = IslandMode.Compact;
        // "Ocultar cuando no hay música" solo oculta sin ninguna app de música abierta: con una canción en pausa
        // la isla queda en reposo, a mano para volver a reproducirla.
        else next = _settings.HideWhenIdle && _snapshot is null ? IslandMode.Hidden : IslandMode.Idle;
        SetMode(next);
    }

    private (double w, double h, double r) GeometryOf(IslandMode mode) => mode switch
    {
        IslandMode.Hidden => (64, 14, 7),
        IslandMode.Idle => (IdleWidth(), 32, 16),
        IslandMode.Compact => (CompactWidth(), 36, 18),
        IslandMode.Peek => (372, 72, 32),
        _ => (430, 204, 42),
    };

    private double IdleWidth() => _settings.Idle switch
    {
        IdleContent.Nothing => 150,
        IdleContent.Time => 132,
        _ => 176,
    };

    private void SetMode(IslandMode mode)
    {
        if (mode == _mode) return;

        var (w, h, r) = GeometryOf(mode);
        var growing = Enum.IsDefined(_mode) && w * h > GeometryOf(_mode).w * GeometryOf(_mode).h;
        _mode = mode;

        // Crecer con un rebote sutil; encogerse más firme, como en iOS.
        foreach (var spring in new[] { _width, _height, _radius })
            spring.Configure(growing ? 240 : 330, growing ? 20 : 28);
        _width.Target = w;
        _height.Target = h;
        _radius.Target = r;
        _scale.Target = RestingScale();
        _lift.Target = _settings.Style == IslandStyle.Taskbar && mode is IslandMode.Expanded or IslandMode.Peek ? _barLift : 0;

        IdlePanel.Width = IdleWidth();
        ShowPanel(IdlePanel, mode == IslandMode.Idle);
        ShowPanel(CompactPanel, mode == IslandMode.Compact);
        ShowPanel(PeekPanel, mode == IslandMode.Peek);
        ShowPanel(ExpandedPanel, mode == IslandMode.Expanded);

        var visible = mode != IslandMode.Hidden;
        Island.IsHitTestVisible = visible;
        var fade = new DoubleAnimation(visible ? 1 : 0, TimeSpan.FromMilliseconds(visible ? 220 : 260)) { EasingFunction = EaseOut };
        Island.BeginAnimation(OpacityProperty, fade);
        GlowShape.BeginAnimation(OpacityProperty, fade);

        var glow = mode switch
        {
            IslandMode.Expanded => 0.55,
            IslandMode.Peek => 0.45,
            IslandMode.Compact => 0.22,
            _ => 0.0,
        };
        GlowShape.BeginAnimation(IslandGlow.IntensityProperty,
            new DoubleAnimation(glow, TimeSpan.FromMilliseconds(500)) { EasingFunction = EaseOut });

        if (mode == IslandMode.Expanded) UpdateProgress();
        ApplyMarquee();
        EnsureLoop();
    }

    private double RestingScale()
        => _pointerDown && !_dragging ? 0.97
         : _hovering && _mode is IslandMode.Idle or IslandMode.Compact or IslandMode.Peek ? 1.035
         : 1.0;

    // ───────────────────────── Contenido ─────────────────────────

    private void ShowPanel(FrameworkElement panel, bool show, bool instant = false)
    {
        if (_panelShown[panel] == show && !instant) return;
        _panelShown[panel] = show;
        panel.IsHitTestVisible = show;

        var scale = (ScaleTransform)panel.RenderTransform;
        if (instant)
        {
            panel.Visibility = show ? Visibility.Visible : Visibility.Hidden;
            panel.Opacity = show ? 1 : 0;
            return;
        }

        var blur = panel.Effect as BlurEffect ?? new BlurEffect { Radius = show ? 10 : 0, RenderingBias = RenderingBias.Performance };
        panel.Effect = blur;

        if (show)
        {
            panel.Visibility = Visibility.Visible;
            if (panel.Opacity < 0.05)
            {
                scale.ScaleX = scale.ScaleY = 0.92;
                blur.Radius = 10;
            }
        }

        var duration = show ? ContentIn : ContentOut;
        var begin = TimeSpan.FromMilliseconds(show ? 70 : 0);
        var targetScale = show ? 1.0 : 1.05;

        var opacity = new DoubleAnimation(show ? 1 : 0, duration) { BeginTime = begin, EasingFunction = EaseOut };
        opacity.Completed += (_, _) =>
        {
            if (_panelShown[panel] != show) return;
            panel.Effect = null;
            if (!show) panel.Visibility = Visibility.Hidden;
        };
        panel.BeginAnimation(OpacityProperty, opacity);

        var scaleAnimation = new DoubleAnimation(targetScale, duration) { BeginTime = begin, EasingFunction = EaseOut };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        blur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(show ? 0 : 8, duration) { BeginTime = begin, EasingFunction = EaseOut });
    }

    private void ApplyArt(MediaSnapshot? snapshot)
    {
        var art = snapshot?.Art;
        var previous = _artBrush;
        _appliedArt = art;
        _artBrush = art is null ? _fallbackArt : Freeze(new ImageBrush(art) { Stretch = Stretch.UniformToFill });
        if (IsLoaded && !ReferenceEquals(previous, _artBrush)) CrossfadeArt(previous);
        CompactArt.Background = _artBrush;
        ExpandedArt.Background = _artBrush;
        if (_alert?.Kind == "music") PeekArt.Background = _artBrush;
        ArtPlaceholder.Visibility = art is null ? Visibility.Visible : Visibility.Collapsed;

        var backdrop = art is null ? null : BlurredBackdrop(art);
        MusicBackdrop.BeginAnimation(OpacityProperty, null);
        MusicBackdrop.Source = backdrop;
        if (backdrop is not null)
            MusicBackdrop.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 0.6, TimeSpan.FromMilliseconds(600)) { EasingFunction = EaseOut });
    }

    /// <summary>
    /// Carátula desenfocada para el fondo de la página de música. Se calcula una sola vez por canción
    /// en un bitmap estático, para no pagar el desenfoque en cada frame.
    /// </summary>
    private static BitmapSource? BlurredBackdrop(BitmapSource art)
    {
        try
        {
            const int w = 430, h = 204, bleed = 70;
            var image = new Image
            {
                Source = art, Stretch = Stretch.UniformToFill, Width = w + 2 * bleed, Height = h + 2 * bleed,
                Effect = new BlurEffect { Radius = 48, RenderingBias = RenderingBias.Quality },
            };
            image.Measure(new Size(image.Width, image.Height));
            image.Arrange(new Rect(0, 0, image.Width, image.Height));
            var full = new RenderTargetBitmap(w + 2 * bleed, h + 2 * bleed, 96, 96, PixelFormats.Pbgra32);
            full.Render(image);
            var cropped = new CroppedBitmap(full, new Int32Rect(bleed, bleed, w, h));
            cropped.Freeze();
            return cropped;
        }
        catch
        {
            return null;
        }
    }

    private void ApplyTexts(MediaSnapshot? snapshot)
    {
        TitleText.Text = snapshot?.Title ?? Strings.NothingPlaying;
        ArtistText.Text = snapshot?.Artist ?? Strings.OpenSpotifyHint;
    }

    /// <summary>Al cambiar de canción, la carátula anterior se desvanece encogiéndose y la nueva crece desde atrás.</summary>
    private void CrossfadeArt(Brush previous)
    {
        var fade = TimeSpan.FromMilliseconds(420);
        foreach (var overlay in new[] { ExpandedArtPrevious, CompactArtPrevious })
        {
            overlay.Background = previous;
            overlay.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, fade) { EasingFunction = EaseOut });
            var shrink = new DoubleAnimation(1, 0.82, fade) { EasingFunction = EaseOut };
            var scale = (ScaleTransform)overlay.RenderTransform;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        }

        var grow = new DoubleAnimation(0.84, 1, TimeSpan.FromMilliseconds(560))
        {
            EasingFunction = new BackEase { Amplitude = 0.45, EasingMode = EasingMode.EaseOut },
        };
        foreach (var scale in new[] { ArtScale, CompactArtScale })
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }
    }

    private void PopArt()
    {
        var pop = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(520) };
        pop.KeyFrames.Add(new EasingDoubleKeyFrame(0.86, KeyTime.FromPercent(0.25), EaseOut));
        pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0), new ElasticEase { Oscillations = 1, Springiness = 4 }));
        ArtScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        ArtScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    private void UpdatePlayGlyph(bool playing)
    {
        AnimateGlyph(PlayGlyph, !playing);
        AnimateGlyph(PauseGlyph, playing);
    }

    private static void AnimateGlyph(Path glyph, bool show)
    {
        var duration = TimeSpan.FromMilliseconds(show ? 260 : 160);
        glyph.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 1 : 0, duration) { EasingFunction = EaseOut });
        var scale = (ScaleTransform)glyph.RenderTransform;
        var animation = new DoubleAnimation(show ? 1 : 0.6, duration)
        {
            EasingFunction = show ? new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut } : EaseOut,
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
}
