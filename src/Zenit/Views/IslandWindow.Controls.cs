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

/// <summary>Controles de reproducción y barra de progreso arrastrable.</summary>
public partial class IslandWindow
{
    // ───────────────────────── Controles ─────────────────────────

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is { } snapshot)
        {
            // Respuesta inmediata; el evento real de la app confirma después.
            _snapshot = snapshot with
            {
                IsPlaying = !snapshot.IsPlaying,
                Position = snapshot.CurrentPosition,
                PositionStamp = DateTimeOffset.Now,
            };
            UpdatePlayGlyph(_snapshot.IsPlaying);
            EnsureLoop();
        }
        _ = _media.TogglePlayPauseAsync();
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e) => _ = _media.PreviousAsync();

    private void NextButton_Click(object sender, RoutedEventArgs e) => _ = _media.NextAsync();

    private void Art_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_snapshot is not null && !_snapshot.IsSpotify) return;
        try
        {
            Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true });
        }
        catch
        {
            // Spotify no está instalado.
        }
    }

    private void SeekTrack_MouseEnter(object sender, MouseEventArgs e) => SetTrackThickness(8);

    private void SeekTrack_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_scrubbing) SetTrackThickness(5);
    }

    private void SeekTrack_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_snapshot is not { CanSeek: true } || _snapshot.Duration <= TimeSpan.Zero) return;
        _scrubbing = true;
        SeekTrack.CaptureMouse();
        _scrubRatio = RatioAt(e);
        SetTrackThickness(8);
        UpdateProgress();
    }

    private void SeekTrack_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_scrubbing) return;
        _scrubRatio = RatioAt(e);
        UpdateProgress();
    }

    private void SeekTrack_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_scrubbing) return;
        e.Handled = true;
        _scrubbing = false;
        SeekTrack.ReleaseMouseCapture();
        if (!SeekTrack.IsMouseOver) SetTrackThickness(5);

        if (_snapshot is { } snapshot)
        {
            var target = TimeSpan.FromTicks((long)(snapshot.Duration.Ticks * _scrubRatio));
            _snapshot = snapshot with { Position = target, PositionStamp = DateTimeOffset.Now };
            _ = _media.SeekAsync(target);
        }
        UpdateProgress();
        if (!_hovering) _collapseTimer.Start();
    }

    private double RatioAt(MouseEventArgs e)
        => SeekTrack.ActualWidth <= 0 ? 0 : Math.Clamp(e.GetPosition(SeekTrack).X / SeekTrack.ActualWidth, 0, 1);

    private void SetTrackThickness(double thickness)
    {
        var animation = new DoubleAnimation(thickness, TimeSpan.FromMilliseconds(180)) { EasingFunction = EaseOut };
        TrackBackground.BeginAnimation(HeightProperty, animation);
        TrackFill.BeginAnimation(HeightProperty, animation);
        TrackBackground.CornerRadius = TrackFill.CornerRadius = new CornerRadius(thickness / 2);
    }
}
