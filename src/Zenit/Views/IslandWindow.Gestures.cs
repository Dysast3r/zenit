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

/// <summary>Gestos del mouse y del touchpad: clic, arrastre, deslizamientos y rueda.</summary>
public partial class IslandWindow
{
    // ───────────────────────── Gestos ─────────────────────────

    private void Island_MouseEnter(object sender, MouseEventArgs e)
    {
        _hovering = true;
        _collapseTimer.Stop();
        if (_settings.ExpandOnHover && (MusicLive || HasLiveActivity) && _mode != IslandMode.Expanded) _hoverTimer.Start();
        _scale.Target = RestingScale();
        EnsureLoop();
    }

    private void Island_MouseLeave(object sender, MouseEventArgs e)
    {
        _hovering = false;
        _hoverTimer.Stop();
        if (!_pointerDown && !_scrubbing && _mode == IslandMode.Expanded) _collapseTimer.Start();
        _scale.Target = RestingScale();
        EnsureLoop();
    }

    private void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_rightDown) return;
        _pointerDown = true;
        _dragging = false;
        _downPoint = e.GetPosition(this);
        _dragStartPager = _pager.Target;
        _velocity.Reset();
        _velocity.Add(_downPoint.X / _settings.Scale);
        _hoverTimer.Stop();
        Island.CaptureMouse();
        _scale.Target = RestingScale();
        EnsureLoop();
    }

    private void Island_MouseMove(object sender, MouseEventArgs e)
    {
        if (_rightDown)
        {
            MoveIsland(e);
            return;
        }
        if (!_pointerDown) return;
        var x = e.GetPosition(this).X;
        var dx = (x - _downPoint.X) / _settings.Scale;
        _velocity.Add(x / _settings.Scale);

        if (!_dragging)
        {
            if (Math.Abs(dx) < 6) return;
            _dragging = true;
            _scale.Target = 1;
        }

        if (_mode == IslandMode.Expanded)
        {
            // El carrusel sigue al cursor con resistencia elástica en los bordes.
            var raw = _dragStartPager + dx;
            var min = -PageWidth * (_pages.Count - 1);
            const double max = 0;
            var value = raw > max ? max + Rubber(raw - max, 70)
                      : raw < min ? min - Rubber(min - raw, 70)
                      : raw;
            _pager.Snap(value);
        }
        else if (_mode is IslandMode.Compact or IslandMode.Peek)
        {
            _shift.Snap(Math.Sign(dx) * Rubber(Math.Abs(dx), 36));
        }
        EnsureLoop();
    }

    private void Island_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pointerDown) return;
        _pointerDown = false;
        Island.ReleaseMouseCapture();

        var dx = (e.GetPosition(this).X - _downPoint.X) / _settings.Scale;
        var velocity = _velocity.Velocity;

        if (!_dragging)
        {
            // Un clic sobre el aviso del temporizador abre directamente su página.
            if (_alert?.Kind == "timer" && _mode == IslandMode.Peek && _pages.IndexOf(_allPages[PageKind.Timer]) is var timerPage and >= 0)
                GoToPage(timerPage, 0);
            RequestExpand(_mode != IslandMode.Expanded);
        }
        else if (_mode == IslandMode.Expanded)
        {
            // Proyecta dónde terminaría el gesto con su inercia y elige la página más cercana.
            var projected = _pager.Value + velocity * 0.2;
            GoToPage((int)Math.Round(-projected / PageWidth), velocity);
        }
        else if (_mode is IslandMode.Compact or IslandMode.Peek)
        {
            _shift.Target = 0;
            _shift.Velocity = velocity * 0.15;
            var intent = dx + velocity * 0.08;
            if (intent < -40) _ = _media.NextAsync();
            else if (intent > 40) _ = _media.PreviousAsync();
        }

        _dragging = false;
        _scale.Target = RestingScale();
        if (!_hovering && _mode == IslandMode.Expanded) _collapseTimer.Start();
        EnsureLoop();
    }

    private void Island_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_rightDown)
        {
            // Se perdió el mouse a mitad del arrastre (p. ej. Alt+Tab): vuelve a donde estaba.
            EndMove();
            PositionWindow();
            return;
        }
        if (!_pointerDown) return;
        _pointerDown = _dragging = false;
        _shift.Target = 0;
        GoToPage(_page, 0);
        _scale.Target = RestingScale();
        EnsureLoop();
    }

    // Clic derecho: si se suelta sin moverse abre el menú; si se arrastra, desliza la isla por el borde superior.

    private void Island_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_pointerDown) return;
        _rightDown = true;
        _moving = false;
        _moveStartCursor = PointToScreen(e.GetPosition(this)).X;
        _moveStartCenter = PointToScreen(new Point(ActualWidth / 2, 0)).X;
        _hoverTimer.Stop();
        Island.CaptureMouse();
    }

    private void MoveIsland(MouseEventArgs e)
    {
        if (CurrentScreen is not { } screen) return;
        var x = PointToScreen(e.GetPosition(this)).X;
        if (!_moving)
        {
            if (Math.Abs(x - _moveStartCursor) < 8) return;
            _moving = true;
            _collapseTimer.Stop();
            Mouse.OverrideCursor = Cursors.SizeWE;
        }

        var work = screen.WorkingArea;
        var position = (_moveStartCenter + x - _moveStartCursor - work.Left) / work.Width;
        // Se imanta al centro para poder volver a dejarla exactamente como venía.
        var snap = 24 * NativeMethods.DpiScaleAt(work.Left + work.Width / 2, work.Top) / work.Width;
        if (Math.Abs(position - 0.5) < snap) position = 0.5;
        _movePosition = Math.Clamp(position, 0, 1);
        PositionWindow();
    }

    private void Island_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (!_rightDown)
        {
            MenuRequested?.Invoke();
            return;
        }

        var moved = _moving;
        var position = _movePosition;
        EndMove();
        Island.ReleaseMouseCapture();

        if (!moved)
        {
            MenuRequested?.Invoke();
        }
        else if (position is { } value)
        {
            // Guardar avisa a todas las islas (modo "todas las pantallas") para que tomen la nueva posición.
            _settings.HorizontalPosition = value;
            _settings.Save();
        }
    }

    private void EndMove()
    {
        _rightDown = _moving = false;
        _movePosition = null;
        Mouse.OverrideCursor = null;
        if (!_hovering && _mode == IslandMode.Expanded) _collapseTimer.Start();
    }

    private void OnHorizontalWheel(int delta)
    {
        if (_mode == IslandMode.Hidden) return;
        if ((DateTime.UtcNow - _lastWheelAction).TotalMilliseconds < 450)
        {
            _wheelAccumulator = 0;
            return;
        }

        _wheelAccumulator += delta;
        if (Math.Abs(_wheelAccumulator) < 90) return;

        var direction = Math.Sign(_wheelAccumulator);
        _wheelAccumulator = 0;
        _lastWheelAction = DateTime.UtcNow;

        if (_mode == IslandMode.Expanded)
        {
            GoToPage(_page + direction, 0);
        }
        else if (_snapshot is not null)
        {
            _shift.Velocity = -direction * 700.0;
            _ = direction > 0 ? _media.NextAsync() : _media.PreviousAsync();
        }
        EnsureLoop();
    }

    private void GoToPage(int page, double velocity)
    {
        _page = Math.Clamp(page, 0, Math.Max(0, _pages.Count - 1));
        _pager.Target = -_page * PageWidth;
        _pager.Velocity = velocity;
        if (_pages.Count > 0 && _pages[_page] is IIslandPage islandPage) islandPage.Refresh();
        EnsureLoop();
    }

    private static double Rubber(double distance, double limit)
        => limit * (1 - 1 / (distance * 0.55 / limit + 1));
}
