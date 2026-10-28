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
using Forms = System.Windows.Forms;

namespace Zenit.Views;

public enum IslandMode { Hidden, Idle, Compact, Peek, Expanded }

public partial class IslandWindow : Window
{
    private const double PageWidth = 430;
    private const double BaseWindowWidth = 560, BaseWindowHeight = 300;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_SETTINGCHANGE = 0x001A, WM_DPICHANGED = 0x02E0, SPI_SETWORKAREA = 0x002F;

    private static readonly Duration ContentIn = TimeSpan.FromMilliseconds(320);
    private static readonly Duration ContentOut = TimeSpan.FromMilliseconds(140);
    private static readonly IEasingFunction EaseOut = Freeze(new CubicEase { EasingMode = EasingMode.EaseOut });

    private static readonly Brush Orange = Frozen(Color.FromRgb(0xFF, 0x9F, 0x0A));
    private static readonly Brush Green = Frozen(Color.FromRgb(0x30, 0xD1, 0x58));
    private static readonly Brush Red = Frozen(Color.FromRgb(0xFF, 0x45, 0x3A));
    private static readonly Brush Blue = Frozen(Color.FromRgb(0x0A, 0x84, 0xFF));
    private static readonly Brush Subtle = IslandTheme.InkAlpha(0x33);

    /// <summary>Aviso breve que la isla muestra creciendo un momento (canción, volumen, batería, temporizador, notificación).</summary>
    private sealed record IslandAlert(
        string Kind, string Title, string Subtitle, double Seconds,
        string? Glyph = null, Brush? GlyphBackground = null, Brush? Image = null,
        double? Progress = null, bool ShowBars = false);

    private readonly ZenitServices _services;
    private readonly MediaService _media;
    private readonly WeatherService _weather;
    private readonly CurrencyService _currency;
    private readonly VolumeService _volume;
    private readonly TimerService _timer;
    private readonly AppSettings _settings;
    private readonly AudioMeter _meter = new();

    private readonly Spring _width = new(170), _height = new(32), _radius = new(16);
    private readonly Spring _scale = new(1), _shift = new(0), _pager = new(0);

    /// <summary>En la barra de tareas, cuánto sube la isla expandida para quedar sobre la barra (en DIP de la ventana).</summary>
    private readonly Spring _lift = new(0);
    private double _barLift;
    private readonly Spring[] _springs;

    private readonly FrameworkElement[] _panels;
    private readonly Dictionary<FrameworkElement, bool> _panelShown = [];
    private readonly EqualizerBars[] _bars;
    private readonly SolidColorBrush _accent = new(IslandTheme.InkColor);
    private readonly Brush _fallbackArt;
    private Brush _artBrush;

    private readonly Dictionary<PageKind, FrameworkElement> _allPages = [];
    private readonly List<FrameworkElement> _pages = [];
    private readonly List<Ellipse> _dots = [];

    private IslandMode _mode = IslandMode.Idle;
    private MediaSnapshot? _snapshot;
    private string? _trackKey;
    private ImageSource? _appliedArt;
    private IslandAlert? _alert;
    private bool _expandRequested, _hovering, _fullscreen, _musicActive;
#if DEBUG
    private bool _demo;
#else
    private const bool _demo = false;
#endif
    private DateTime _peekUntil;
    private int _page;
    private double _ear;
    private string _weatherKey = "", _currencyKey = "", _calendarKey = "";

    /// <summary>
    /// La isla principal es la única que actualiza datos de red, widgets y notificaciones; las de otras
    /// pantallas (modo "todas") solo muestran lo mismo.
    /// </summary>
    private readonly bool _primary;

    /// <summary>Pantalla fija de una isla secundaria; la principal la toma de los ajustes.</summary>
    private readonly string? _fixedScreen;

    private bool _pointerDown, _dragging;
    private Point _downPoint;
    private double _dragStartPager;
    private readonly VelocityTracker _velocity = new();
    private bool _scrubbing;
    private double _scrubRatio;

    // Mover la isla manteniendo el clic derecho (en píxeles físicos de pantalla).
    private bool _rightDown, _moving;
    private double _moveStartCursor, _moveStartCenter;
    private double? _movePosition;
    private double _wheelAccumulator;
    private DateTime _lastWheelAction;

    private readonly DispatcherTimer _clockTimer, _hoverTimer, _collapseTimer, _peekTimer, _musicPauseTimer, _sessionLostTimer, _expandedVolumeTimer, _weatherTimer, _currencyTimer, _calendarTimer;
    /// <summary>En la barra de tareas: al hacer clic en la barra, Windows la sube sobre la isla; esto la devuelve encima.</summary>
    private readonly DispatcherTimer _taskbarTimer;
    private bool _loopAttached;
    private TimeSpan _lastFrame;
    private double _time;
    private int _tick;
    private nint _hwnd;

    private string _elapsedCache = "", _remainingCache = "";

    public event Action? MenuRequested;

    public IslandWindow(ZenitServices services, bool primary = true, string? screen = null)
    {
        _services = services;
        _primary = primary;
        _fixedScreen = screen;
        _media = services.Media;
        _weather = services.Weather;
        _currency = services.Currency;
        _volume = services.Volume;
        _timer = services.Timer;
        _settings = services.Settings;
        InitializeComponent();

        _springs = [_width, _height, _radius, _scale, _shift, _pager, _lift];
        _lift.Configure(260, 24);
        _scale.Configure(420, 24);
        _shift.Configure(380, 24);
        _pager.Configure(300, 30);

        _panels = [IdlePanel, CompactPanel, PeekPanel, ExpandedPanel];
        foreach (var panel in _panels)
        {
            panel.RenderTransformOrigin = new Point(0.5, 0.5);
            panel.RenderTransform = new ScaleTransform(1, 1);
            panel.Opacity = 0;
            panel.Visibility = Visibility.Hidden;
            panel.IsHitTestVisible = false;
            _panelShown[panel] = false;
        }
        ShowPanel(IdlePanel, true, instant: true);

        _bars = [CompactBars, PeekBars, ExpandedBars];
        foreach (var bars in _bars) bars.BarBrush = _accent;

        _fallbackArt = Freeze(new LinearGradientBrush(Color.FromRgb(0x17, 0x46, 0xD8), Color.FromRgb(0x22, 0xD3, 0xEE), 45));
        _artBrush = _fallbackArt;
        ApplyArt(null);
        ApplyTexts(null);

        _allPages[PageKind.Music] = MusicPage;
        _allPages[PageKind.Lyrics] = new LyricsPage(services.Lyrics, () => _snapshot);
        _allPages[PageKind.Clock] = new ClockPage(_settings);
        _allPages[PageKind.Timer] = new TimerPage(_timer);
        _allPages[PageKind.Weather] = new WeatherPage(_weather, _settings);
        _allPages[PageKind.Currency] = new CurrencyPage(_currency);
        _allPages[PageKind.WorldClock] = new WorldClockPage(_settings);
        _allPages[PageKind.Widgets] = new WidgetsPage(services.Widgets);
        _allPages[PageKind.Volume] = new VolumePage(_volume);

        _clockTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => OnClockTick(), Dispatcher);
        _hoverTimer = OneShot(220, () =>
        {
            if (_hovering && !_pointerDown) RequestExpand(true);
        });
        _collapseTimer = OneShot(450, () =>
        {
            // Arrastrar un deslizador (volumen) puede sacar el cursor de la isla: no se cierra a mitad del gesto.
            var dragging = Mouse.Captured is DependencyObject captured && Island.IsAncestorOf(captured);
            if (_hovering || _pointerDown || _scrubbing || dragging) return;
            // En la barra de tareas la isla expandida sube sobre la barra y el cursor que la abrió queda debajo:
            // mientras siga en ese hueco no se cierra, y se vuelve a mirar hasta que salga o entre en la isla.
            if (PointerBelowLiftedIsland()) _collapseTimer!.Start();
            else RequestExpand(false);
        });
        _peekTimer = OneShot(3000, () =>
        {
            _alert = null;
            Evaluate();
        });
        _sessionLostTimer = OneShot(SessionLostGraceSeconds * 1000, () => ApplyMedia(null));
        _musicPauseTimer = OneShot(MusicPauseGraceSeconds * 1000, () =>
        {
            if (_snapshot?.IsPlaying == true) return;
            _musicActive = false;
            UpdateCompact();
            Evaluate();
        });
        _expandedVolumeTimer = OneShot(1600, HideExpandedVolume);
        _weatherTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
        _weatherTimer.Tick += (_, _) => _ = _weather.RefreshAsync();
        _currencyTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
        _currencyTimer.Tick += (_, _) => _ = _currency.RefreshAsync();
        _calendarTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMinutes(10) };
        _calendarTimer.Tick += (_, _) => _ = services.Calendar.RefreshAsync();
        _taskbarTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Background, (_, _) =>
        {
            if (_hwnd != 0 && !HiddenByFullscreen) NativeMethods.KeepTopmost(_hwnd);
        }, Dispatcher);
        // Este constructor arranca el timer: solo debe correr con la isla en la barra de tareas (ApplySettings).
        _taskbarTimer.Stop();

        _media.Changed += OnMediaChangedFromService;
        _weather.Updated += UpdateIdleContent;
        _currency.Updated += UpdateIdleContent;
        services.Calendar.Updated += OnCalendarUpdated;
        _settings.Changed += ApplySettings;
        _volume.Changed += OnVolumeChanged;
        _timer.Changed += OnTimerChanged;
        _timer.Completed += OnTimerCompleted;
        services.Indicators.PrivacyChanged += UpdatePrivacyDots;
        services.Indicators.ChargingStarted += OnChargingStarted;
        services.Indicators.BatteryLow += OnBatteryLow;
        services.Notifications.Received += OnNotification;

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            ApplySettings();
            if (_primary) services.Widgets.Start();
            _clockTimer.Start();
        };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) =>
        {
            // Las islas de otras pantallas se crean y se cierran al cambiar de monitor: no deben dejar
            // suscripciones vivas en los servicios compartidos.
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _media.Changed -= OnMediaChangedFromService;
            _weather.Updated -= UpdateIdleContent;
            _currency.Updated -= UpdateIdleContent;
            services.Calendar.Updated -= OnCalendarUpdated;
            _settings.Changed -= ApplySettings;
            _volume.Changed -= OnVolumeChanged;
            _timer.Changed -= OnTimerChanged;
            _timer.Completed -= OnTimerCompleted;
            services.Indicators.PrivacyChanged -= UpdatePrivacyDots;
            services.Indicators.ChargingStarted -= OnChargingStarted;
            services.Indicators.BatteryLow -= OnBatteryLow;
            services.Notifications.Received -= OnNotification;
            foreach (var timer in new[] { _clockTimer, _hoverTimer, _collapseTimer, _peekTimer, _musicPauseTimer, _sessionLostTimer, _weatherTimer, _currencyTimer, _calendarTimer, _taskbarTimer })
                timer.Stop();
            if (_loopAttached) CompositionTarget.Rendering -= OnRendering;
            _meter.Dispose();
        };
    }

    // ───────────────────────── Ventana y ajustes ─────────────────────────

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeOverlay(_hwnd);
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(PositionWindow);

    /// <summary>Pantalla de esta isla: la fija (islas secundarias) o la elegida en los ajustes; null = la principal.</summary>
    private string? ScreenName => _fixedScreen ?? (_settings.Monitor is "" or "*" ? null : _settings.Monitor);

    private Forms.Screen? CurrentScreen => Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == ScreenName) ?? Forms.Screen.PrimaryScreen;

    /// <summary>
    /// Pone la isla arriba del área de trabajo de su pantalla, en la posición horizontal elegida (centrada por defecto).
    /// Mientras se arrastra con el clic derecho se usa la posición del arrastre.
    /// </summary>
    private void PositionWindow()
    {
        var position = _movePosition ?? _settings.HorizontalPosition;
        var screen = CurrentScreen;
        if (_hwnd == 0 || screen is null)
        {
            var area = SystemParameters.WorkArea;
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top;
            return;
        }

        // En píxeles físicos: cada monitor puede tener su propia escala. Si la escala cambia al mover la
        // ventana, WPF la redimensiona y WM_DPICHANGED vuelve a llamar aquí para reubicarla.
        var work = screen.WorkingArea;
        var scale = NativeMethods.DpiScaleAt(work.Left + work.Width / 2, work.Top + work.Height / 2);
        var width = (int)Math.Round(Width * scale);

        // La isla expandida siempre cabe entera: cerca de los bordes, el centro se detiene a media isla.
        var half = (PageWidth / 2 + _ear + 6) * _settings.Scale * scale;
        var center = work.Left + position * work.Width;
        center = Math.Max(work.Left + half, Math.Min(work.Right - half, center));
        if (work.Width < 2 * half) center = work.Left + work.Width / 2.0;
        var x = (int)Math.Round(center - width / 2.0);

        if (_settings.Style != IslandStyle.Taskbar)
        {
            NativeMethods.MoveWindow(_hwnd, x, work.Top);
            return;
        }

        // Barra de tareas: la ventana se apoya en el borde inferior y la píldora en reposo queda centrada
        // en la barra. Su alto es lo que el área de trabajo deja libre abajo; si la barra se oculta sola o
        // está en otro borde, se usa el alto estándar de Windows 11 (48 px).
        var bounds = screen.Bounds;
        var bar = bounds.Bottom - work.Bottom;
        if (bar <= 0) bar = (int)Math.Round(48 * scale);
        var inset = (bar / scale / _settings.Scale - GeometryOf(IslandMode.Idle).h) / 2;
        Island.Margin = GlowShape.Margin = new Thickness(0, 0, 0, Math.Max(2, inset));
        // Expandida o con un aviso, la isla se despega y flota 10 px sobre la barra, como los paneles de Windows 11.
        _barLift = bar / scale - Math.Max(2, inset) * _settings.Scale + 10;
        var height = (int)Math.Round(Height * scale);
        NativeMethods.MoveWindow(_hwnd, x, bounds.Bottom - height);
    }

    /// <summary>Aplica todos los ajustes en vivo; se llama al cargar y cada vez que cambian.</summary>
    public void ApplySettings()
    {
        var s = _settings;

        // Forma: notch pegado arriba (iPhone 13), isla flotante (iPhone 14 Pro) o píldora en la barra de tareas,
        // que crece hacia arriba. El margen inferior de esta última lo calcula PositionWindow según la barra.
        var taskbar = s.Style == IslandStyle.Taskbar;
        _ear = s.Style == IslandStyle.Notch ? IslandShape.NotchEar : 0;
        Island.VerticalAlignment = GlowShape.VerticalAlignment = taskbar ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        Host.RenderTransformOrigin = new Point(0.5, taskbar ? 1 : 0);
        if (!taskbar)
        {
            var top = (s.Style == IslandStyle.Notch ? 0 : 8) + s.TopOffset;
            Island.Margin = GlowShape.Margin = new Thickness(0, top, 0, 0);
        }
        // Sobre la barra, un filo de luz finísimo la integra con el cristal de Windows 11 (la mitad queda recortada).
        IslandFill.Stroke = taskbar ? IslandTheme.InkAlpha(0x2A) : null;
        IslandFill.StrokeThickness = taskbar ? 2 : 0;
        _taskbarTimer.IsEnabled = taskbar;
        GlowShape.Visibility = s.ShowGlow ? Visibility.Visible : Visibility.Collapsed;
        // Sin borde no hay nada que animar: así no queda un reloj corriendo de fondo.
        GlowShape.Animation = s.ShowGlow ? s.GlowAnimation : GlowAnimation.None;
        GlowShape.Gradient = s.GlowTone == GlowTone.Gradient;
        if (TryParseColor(s.GlowColor2, out var second)) GlowShape.SecondColor = second;

        UserScale.ScaleX = UserScale.ScaleY = s.Scale;
        Width = BaseWindowWidth * s.Scale;
        // En la barra hace falta espacio extra arriba: la isla expandida sube por encima de la barra.
        Height = (BaseWindowHeight + (taskbar ? 64 : s.TopOffset)) * s.Scale;
        PositionWindow();

        if (_hwnd != 0) NativeMethods.ExcludeFromCapture(_hwnd, s.HideFromCapture && !_demo);

        if (_primary && _media.MusicAppsOnly != s.MusicAppsOnly)
        {
            _media.MusicAppsOnly = s.MusicAppsOnly;
            _media.Reselect();
        }

        if (_primary)
        {
            if (s.ShowNotifications) _ = _services.Notifications.StartAsync(Dispatcher);
            else _services.Notifications.Stop();
        }

        RebuildPages();
        UpdateIdleContent();
        UpdateCompact();
        UpdatePrivacyDots();
        ApplyAccent();
        RefreshDataIfNeeded();
        LoadLyricsIfNeeded();

        // Fuerza recalcular la geometría del modo actual (el reposo cambia de ancho según su contenido).
        var mode = _mode;
        _mode = (IslandMode)(-1);
        SetMode(mode);
        Evaluate();
        ApplyGeometry();
    }

    private void RefreshDataIfNeeded()
    {
        if (!_primary) return;
        var s = _settings;
        var needsWeather = s.Idle == IdleContent.TimeAndWeather || s.CompactCenter == CompactCenter.Temperature || IsPageEnabled(PageKind.Weather);
        var needsCurrency = s.Idle == IdleContent.Currency || IsPageEnabled(PageKind.Currency);

        var weatherKey = needsWeather
            ? $"{s.UseCurrentLocation}|{s.Unit}|{string.Join(';', s.Cities.Select(c => $"{c.Latitude},{c.Longitude}"))}"
            : "";
        if (weatherKey != _weatherKey)
        {
            _weatherKey = weatherKey;
            if (needsWeather) _ = _weather.RefreshAsync();
        }
        _weatherTimer.Interval = TimeSpan.FromMinutes(s.WeatherRefreshMinutes);
        _weatherTimer.IsEnabled = needsWeather;

        var currencyKey = needsCurrency ? $"{s.LocalCurrency}|{string.Join(';', s.Currencies)}" : "";
        if (currencyKey != _currencyKey)
        {
            _currencyKey = currencyKey;
            if (needsCurrency) _ = _currency.RefreshAsync();
        }
        _currencyTimer.Interval = TimeSpan.FromMinutes(s.CurrencyRefreshMinutes);
        _currencyTimer.IsEnabled = needsCurrency;

        var calendarKey = string.Join('\n', s.CalendarUrls);
        if (calendarKey != _calendarKey)
        {
            _calendarKey = calendarKey;
            _ = _services.Calendar.RefreshAsync();
        }
        _calendarTimer.IsEnabled = s.CalendarUrls.Count > 0;
    }

    /// <summary>Busca la letra de la canción actual si la página está activa (si ya está cargada, no hace nada).</summary>
    private void LoadLyricsIfNeeded()
    {
        if (_snapshot is { } snapshot && IsPageEnabled(PageKind.Lyrics))
            _ = _services.Lyrics.LoadAsync(snapshot.Title, snapshot.Artist, snapshot.Duration);
    }

    private bool IsPageEnabled(PageKind kind) => _settings.Pages.Any(p => p.Kind == kind && p.Enabled);

    private void RebuildPages()
    {
        var current = _page < _pages.Count ? _pages[_page] : null;

        _pages.Clear();
        PagesHost.Children.Clear();
        foreach (var entry in _settings.Pages.Where(p => p.Enabled))
        {
            var page = _allPages[entry.Kind];
            _pages.Add(page);
            PagesHost.Children.Add(page);
        }
        if (_pages.Count == 0)
        {
            _pages.Add(MusicPage);
            PagesHost.Children.Add(MusicPage);
        }

        DotsHost.Children.Clear();
        _dots.Clear();
        if (_pages.Count > 1)
        {
            foreach (var _ in _pages)
            {
                var dot = new Ellipse { Width = 6, Height = 6, Fill = IslandTheme.Ink, Margin = new Thickness(3, 0, 3, 0) };
                _dots.Add(dot);
                DotsHost.Children.Add(dot);
            }
        }

        var index = current is null ? 0 : _pages.IndexOf(current);
        _page = Math.Max(0, index);
        _pager.Snap(-_page * PageWidth);
        RefreshPages();
    }

    private void ApplyAccent()
    {
        var color = _settings.AccentMode == AccentMode.Custom && TryParseColor(_settings.CustomAccent, out var custom)
            ? custom
            : _snapshot?.Accent ?? IslandTheme.InkColor;
        var animation = new ColorAnimation(color, TimeSpan.FromMilliseconds(700)) { EasingFunction = EaseOut };
        _accent.BeginAnimation(SolidColorBrush.ColorProperty, animation);

        // El neón sigue al acento, salvo que el usuario le haya dado su propio tono.
        var glow = _settings.GlowTone != GlowTone.Accent && TryParseColor(_settings.GlowColor, out var own) ? own : color;
        GlowShape.BeginAnimation(IslandGlow.ColorProperty, new ColorAnimation(glow, TimeSpan.FromMilliseconds(700)) { EasingFunction = EaseOut });
    }

    private static bool TryParseColor(string text, out Color color)
    {
        try
        {
            color = (Color)ColorConverter.ConvertFromString(text);
            return true;
        }
        catch
        {
            color = Colors.White;
            return false;
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        var delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
        if (msg == NativeMethods.WM_MOUSEHWHEEL)
        {
            OnHorizontalWheel(delta);
            handled = true;
        }
        else if (msg == WM_MOUSEWHEEL && _settings.WheelControlsVolume && Island.IsMouseOver && _volume.IsAvailable)
        {
            // Rueda sobre la isla: volumen del sistema en pasos de 2 %. Si la isla aún no se expandió por
            // el cursor, se cancela: quien gira la rueda quiere ver la barra de volumen, no la isla abierta.
            _hoverTimer.Stop();
            _volume.Adjust(delta / 120f * 0.02f);
            handled = true;
        }
        else if ((msg == WM_SETTINGCHANGE && wParam == SPI_SETWORKAREA) || msg == WM_DPICHANGED)
        {
            // La barra de tareas cambió de tamaño o la ventana pasó a otra escala: recentrar arriba.
            Dispatcher.BeginInvoke(PositionWindow);
        }
        return 0;
    }

    private void OnClockTick()
    {
        UpdateIdleContent();
        UpdateCompact();
        CheckMeeting();
        if (_mode == IslandMode.Expanded) RefreshPages();
        if (_hwnd != 0 && !HiddenByFullscreen) NativeMethods.KeepTopmost(_hwnd);

        // Cada isla mira su propia pantalla: un juego en el otro monitor no la oculta.
        var fullscreen = _hwnd != 0 && NativeMethods.IsFullscreenAppRunning(_hwnd);
        if (fullscreen != _fullscreen)
        {
            _fullscreen = fullscreen;
            Evaluate();
        }

        if (++_tick % 2 == 0)
        {
            _media.PollTimeline();
            if (_primary) _media.Refresh();
        }
    }

    private void RefreshPages()
    {
        foreach (var page in _pages)
            if (page is IIslandPage islandPage) islandPage.Refresh();
    }

    private void UpdateIdleContent()
    {
        var now = DateTime.Now;
        var weather = _weather.Reports.FirstOrDefault();
        var quote = _currency.IsStale ? null : _currency.Report?.Quotes.FirstOrDefault();

        string main = "", extra = "";
        var showGlyph = false;
        switch (_settings.Idle)
        {
            case IdleContent.Time:
                main = TimeFormats.Time(now, _settings, seconds: false);
                break;
            case IdleContent.Date:
                main = TimeFormats.Date(now, DateStyle.Medium, "");
                break;
            case IdleContent.TimeAndWeather:
                main = TimeFormats.Time(now, _settings, seconds: false);
                if (weather is not null)
                {
                    showGlyph = true;
                    extra = TimeFormats.Temperature(weather.Temperature);
                    IdleGlyph.Kind = weather.Kind;
                    IdleGlyph.IsDay = weather.IsDay;
                }
                break;
            case IdleContent.Currency:
                if (quote is not null)
                {
                    main = quote.Code.ToUpperInvariant();
                    extra = CurrencyService.FormatValue(quote.Value);
                }
                else main = TimeFormats.Time(now, _settings, seconds: false);
                break;
            case IdleContent.Battery:
                main = TimeFormats.Time(now, _settings, seconds: false);
                // En un equipo de escritorio no hay batería: queda solo la hora.
                if (_services.Indicators.BatteryLevel is { } level) extra = $"{level * 100:0}%";
                break;
        }

        IdleText.Text = main;
        IdleExtra.Text = extra;
        IdleGlyph.Visibility = showGlyph ? Visibility.Visible : Visibility.Collapsed;
        IdleDot.Visibility = main.Length > 0 && extra.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        IdleExtra.Visibility = extra.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ───────────────────────── Utilidades ─────────────────────────

    private DispatcherTimer OneShot(int milliseconds, Action action)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        return timer;
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static Brush Frozen(Color color) => Freeze(new SolidColorBrush(color));
}
