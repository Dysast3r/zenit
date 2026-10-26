using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Zenit.Controls;
using Zenit.Services;

namespace Zenit.Views;

public partial class SettingsWindow : Window
{
    private sealed record Section(string Glyph, string Title, string Subtitle, FrameworkElement Panel);

    private sealed record CurrencyItem(string Code, string Name)
    {
        public override string ToString() => $"{Code.ToUpperInvariant()} — {Name}";
    }

    private static readonly string[] SwatchColors = ["#0A84FF", "#22D3EE", "#8B5CF6", "#EC4899", "#10B981", "#F59E0B", "#EF4444", "#FFFFFF"];

    private static readonly (double scale, string name)[] SizeOptions =
    [
        (0.8, Strings.T("Pequeña", "Small")),
        (1.0, Strings.T("Normal", "Normal")),
        (1.2, Strings.T("Grande", "Large")),
        (1.4, Strings.T("Muy grande", "Extra large")),
    ];

    private static readonly string[] FallbackCurrencies =
        ["usd", "eur", "gbp", "jpy", "cny", "chf", "cad", "aud", "clp", "ars", "brl", "mxn", "cop", "pen", "uyu", "btc", "eth"];

    private readonly ZenitServices _services;
    private readonly AppSettings _settings;
    private readonly List<Section> _sections;
    private UpdateInfo? _update;
    private bool _updating;
    private readonly DispatcherTimer _saveTimer, _clockTimer, _searchTimer;
    private CancellationTokenSource? _search;
    private bool _loading;

    public SettingsWindow(ZenitServices services)
    {
        _services = services;
        _settings = services.Settings;
        InitializeComponent();

        var version = AppInfo.Version;
        VersionText.Text = Strings.F("Versión {0} · {1}", "Version {0} · {1}", version, AppInfo.DeveloperName);
        AboutVersion.Text = Strings.F("Versión {0}", "Version {0}", version);

        _sections =
        [
            new("", Strings.T("General", "General"), Strings.T("Apariencia y comportamiento de la isla", "Island appearance and behavior"), GeneralSection),
            new("", Strings.T("Páginas", "Pages"), Strings.T("Elige y ordena lo que ves al expandir la isla", "Choose and order what you see when the island expands"), PagesSection),
            new("", Strings.T("Música", "Music"), Strings.T("Spotify, avisos y ecualizador", "Spotify, notifications and equalizer"), MusicSection),
            new("", Strings.T("Reloj y fecha", "Clock & date"), Strings.T("Formato de la hora y de la fecha", "Time and date format"), ClockSection),
            new("\uE787", Strings.T("Calendario", "Calendar"), Strings.T("Avisos antes de tus reuniones de Google Calendar u Outlook", "Alerts before your Google Calendar or Outlook meetings"), CalendarSection),
            new("", Strings.T("Relojes mundiales", "World clocks"), Strings.T("La hora en otras ciudades del mundo", "The time in other cities around the world"), WorldSection),
            new("", Strings.T("Clima", "Weather"), Strings.T("Temperatura actual y pronóstico por horas", "Current temperature and hourly forecast"), WeatherSection),
            new("", Strings.T("Monedas", "Currencies"), Strings.T("Tipos de cambio y criptomonedas", "Exchange rates and crypto"), CurrencySection),
            new("", Strings.T("Sistema", "System"), Strings.T("Notificaciones, volumen, batería y privacidad", "Notifications, volume, battery and privacy"), SystemSection),
            new("", Strings.T("Widgets", "Widgets"), Strings.T("Datos de cualquier API pública, creados por la comunidad", "Data from any public API, made by the community"), WidgetsSection),
            new("", Strings.T("Acerca de", "About"), Strings.T("Versión, privacidad y mantenimiento", "Version, privacy and maintenance"), AboutSection),
        ];
        foreach (var section in _sections)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = section.Glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) });
            row.Children.Add(new TextBlock { Text = section.Title, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
            Nav.Items.Add(new ListBoxItem { Content = row });
        }

        BuildStaticChoices();
        LoadValues();
        Nav.SelectedIndex = 0;

        _saveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(160) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _settings.Save();
        };
        _searchTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(350) };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await SearchCitiesAsync();
        };
        _clockTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdatePreviews(), Dispatcher);
        _clockTimer.Start();

        Loaded += async (_, _) => await LoadCurrenciesAsync();
        // La opción de inicio con Windows también se cambia desde la bandeja: se relee al volver a la ventana
        // para que el siguiente guardado no la revierta.
        Activated += (_, _) =>
        {
            _loading = true;
            StartupSwitch.IsChecked = StartupService.IsEnabled;
            _loading = false;
        };
        _services.Widgets.Updated += OnWidgetsUpdated;
        _services.Notifications.AccessChanged += OnNotificationAccessChanged;
        _services.Calendar.Updated += UpdateCalendarStatus;
        Closed += (_, _) =>
        {
            _services.Widgets.Updated -= OnWidgetsUpdated;
            _services.Notifications.AccessChanged -= OnNotificationAccessChanged;
            _services.Calendar.Updated -= UpdateCalendarStatus;
            _clockTimer.Stop();
            if (_saveTimer.IsEnabled)
            {
                _saveTimer.Stop();
                _settings.Save();
            }
        };
    }

    // ───────────────────────── Navegación ─────────────────────────

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedIndex < 0) return;
        var current = _sections[Nav.SelectedIndex];
        foreach (var section in _sections) section.Panel.Visibility = section == current ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = current.Title;
        PageSubtitle.Text = current.Subtitle;
        Scroller.ScrollToTop();

        // Entrada suave del contenido.
        current.Panel.RenderTransform = new TranslateTransform(0, 12);
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        current.Panel.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        current.Panel.RenderTransform.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
    }

    // ───────────────────────── Carga de valores ─────────────────────────

    private void BuildStaticChoices()
    {
        var windowsLanguage = Strings.Capitalize(CultureInfo.GetCultureInfo(Strings.WindowsCulture.TwoLetterISOLanguageName).NativeName);
        Fill(LanguageCombo,
            (AppLanguage.System, Strings.F("Automático · {0}", "Automatic · {0}", windowsLanguage)),
            (AppLanguage.Spanish, "Español"),
            (AppLanguage.English, "English"),
            (AppLanguage.Portuguese, "Português"));

        Fill(IdleCombo,
            (IdleContent.Nothing, Strings.T("Nada (notch clásico)", "Nothing (classic notch)")),
            (IdleContent.Time, Strings.T("Hora", "Time")),
            (IdleContent.Date, Strings.T("Fecha", "Date")),
            (IdleContent.TimeAndWeather, Strings.T("Hora y clima", "Time and weather")),
            (IdleContent.Currency, Strings.T("Moneda principal", "Main currency")),
            (IdleContent.Battery, Strings.T("Hora y batería", "Time and battery")));

        Fill(CompactCenterCombo,
            (CompactCenter.Nothing, Strings.T("Nada", "Nothing")),
            (CompactCenter.Time, Strings.T("Hora", "Time")),
            (CompactCenter.Date, Strings.T("Fecha", "Date")),
            (CompactCenter.Temperature, Strings.T("Temperatura", "Temperature")),
            (CompactCenter.Track, Strings.T("Canción (en movimiento)", "Song (scrolling)")));

        Fill(CalendarLeadCombo, new[] { 1, 2, 5, 10, 15 }.Select(m => ((object)m, Strings.F("{0} min de anticipación", "{0} min before", m))).ToArray());
        BuildMonitorChoices();

        Fill(AccentCombo,
            (AccentMode.Artwork, Strings.T("Automático", "Automatic")),
            (AccentMode.Custom, Strings.T("Personalizado", "Custom")));

        Fill(UnitCombo,
            (TemperatureUnit.Celsius, Strings.T("Celsius (°C)", "Celsius (°C)")),
            (TemperatureUnit.Fahrenheit, Strings.T("Fahrenheit (°F)", "Fahrenheit (°F)")));

        Fill(WeatherRefreshCombo, new[] { 5, 10, 15, 30, 60 }.Select(m => ((object)m, Minutes(m))).ToArray());
        Fill(CurrencyRefreshCombo, new[] { 15, 30, 60, 180, 720 }.Select(m => ((object)m, Minutes(m))).ToArray());

        var sample = DateTime.Now;
        Fill(DateCombo, Enum.GetValues<DateStyle>()
            .Select(style => ((object)style, style == DateStyle.Custom
                ? Strings.T("Personalizado…", "Custom…")
                : $"{DateStyleName(style)}  ·  {TimeFormats.Date(sample, style, "")}"))
            .ToArray());

        foreach (var color in SwatchColors)
        {
            var swatch = new Button
            {
                Width = 22, Height = 22, Margin = new Thickness(3, 0, 3, 0), Padding = new Thickness(0), Tag = color,
                Cursor = System.Windows.Input.Cursors.Hand, ToolTip = color,
                Template = SwatchTemplate((Color)ColorConverter.ConvertFromString(color)),
            };
            swatch.Click += (_, _) =>
            {
                _settings.AccentMode = AccentMode.Custom;
                _settings.CustomAccent = color;
                _loading = true;
                Select(AccentCombo, AccentMode.Custom);
                _loading = false;
                ScheduleSave();
            };
            Swatches.Children.Add(swatch);
        }

        foreach (var (scale, name) in SizeOptions)
        {
            // Cada atajo muestra una mini isla dibujada a su escala.
            var preview = new Border
            {
                Width = 46 * scale, Height = 12 * scale, CornerRadius = new CornerRadius(6 * scale),
                Background = Brushes.White, Margin = new Thickness(0, 0, 0, 6), HorizontalAlignment = HorizontalAlignment.Center,
            };
            var content = new StackPanel { Margin = new Thickness(0, 4, 0, 2) };
            content.Children.Add(new Grid { Height = 22, Children = { preview } });
            content.Children.Add(new TextBlock { Text = name, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 12.5 });
            content.Children.Add(new TextBlock { Text = $"{scale * 100:0}%", HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11, Foreground = (Brush)FindResource("Muted") });

            var button = new Button
            {
                Content = content, Tag = scale, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(6),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            button.Click += (_, _) => ScaleSlider.Value = scale;
            SizePresets.Children.Add(button);
        }

        ZoneCombo.ItemsSource = TimeZoneInfo.GetSystemTimeZones();
    }

    private void LoadValues()
    {
        _loading = true;
        var s = _settings;

        Select(LanguageCombo, s.Language);
        StyleNotch.IsChecked = s.Style == IslandStyle.Notch;
        StyleFloating.IsChecked = s.Style == IslandStyle.Floating;
        Select(IdleCombo, s.Idle);
        SelectMonitor(s.Monitor);
        ScaleSlider.Value = s.Scale;
        OffsetSlider.Value = s.TopOffset;
        Select(AccentCombo, s.AccentMode);
        HoverSwitch.IsChecked = s.ExpandOnHover;
        HideIdleSwitch.IsChecked = s.HideWhenIdle;
        FullscreenSwitch.IsChecked = s.HideInFullscreen;
        CaptureSwitch.IsChecked = s.HideFromCapture;
        StartupSwitch.IsChecked = StartupService.IsEnabled;

        SpotifyOnlySwitch.IsChecked = s.SpotifyOnly;
        PeekSwitch.IsChecked = s.ShowTrackPeek;
        PeekSlider.Value = s.PeekSeconds;
        ReactiveSwitch.IsChecked = s.ReactiveEqualizer;
        Select(CompactCenterCombo, s.CompactCenter);

        H24Switch.IsChecked = s.Use24Hour;
        SecondsSwitch.IsChecked = s.ShowSeconds;
        Select(DateCombo, s.DateStyle);
        CustomDateBox.Text = s.CustomDateFormat;
        WeekSwitch.IsChecked = s.ShowWeekNumber;

        LocationSwitch.IsChecked = s.UseCurrentLocation;
        Select(UnitCombo, s.Unit);
        Select(WeatherRefreshCombo, s.WeatherRefreshMinutes);
        Select(CurrencyRefreshCombo, s.CurrencyRefreshMinutes);

        NotificationsSwitch.IsChecked = s.ShowNotifications;
        VolumeHudSwitch.IsChecked = s.VolumeHud;
        WheelVolumeSwitch.IsChecked = s.WheelControlsVolume;
        BatterySwitch.IsChecked = s.BatteryAlerts;
        PrivacySwitch.IsChecked = s.PrivacyIndicators;
        TimerLiveSwitch.IsChecked = s.TimerLiveActivity;
        DndSwitch.IsChecked = s.RespectDoNotDisturb;
        AutoUpdateSwitch.IsChecked = s.AutoCheckUpdates;

        CalendarSwitch.IsChecked = s.CalendarLiveActivity;
        Select(CalendarLeadCombo, s.CalendarLeadMinutes);

        _loading = false;

        BuildWidgetsList();
        UpdateNotificationInfo();
        UpdateUpdateCard();
        BuildPagesList();
        BuildWorldList();
        BuildCalendarList();
        BuildCitiesList();
        BuildCurrencyChips();
        UpdateLabels();
        UpdatePreviews();
    }

    private async Task LoadCurrenciesAsync()
    {
        var names = await CurrencyService.GetNamesAsync();
        var items = names.Count > 0
            ? names.Select(p => new CurrencyItem(p.Key, p.Value)).OrderBy(i => i.Code).ToList()
            : FallbackCurrencies.Select(c => new CurrencyItem(c, c.ToUpperInvariant())).ToList();

        _loading = true;
        LocalCurrencyCombo.ItemsSource = items;
        LocalCurrencyCombo.SelectedItem = items.FirstOrDefault(i => i.Code == _settings.LocalCurrency);
        AddCurrencyCombo.ItemsSource = items;
        _loading = false;
        BuildCurrencyChips();
    }

    // ───────────────────────── Guardado ─────────────────────────

    private void Any_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded && sender is not CheckBox) return;
        Collect();
        UpdateLabels();
        UpdatePreviews();
        ScheduleSave();
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var language = Selected(LanguageCombo, _settings.Language);
        if (language == _settings.Language) return;
        _settings.Language = language;
        ApplyLanguage();
    }

    /// <summary>Si el idioma efectivo cambió, guarda y reinicia para que toda la interfaz cambie de una vez.</summary>
    private void ApplyLanguage()
    {
        SaveNow();
        if (Strings.WouldChange(_settings.Language) && Application.Current is App app) app.Restart();
    }

    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => Any_Changed(sender, e);

    private void Style_Checked(object sender, RoutedEventArgs e) => Any_Changed(sender, e);

    private void CustomDate_Changed(object sender, TextChangedEventArgs e) => Any_Changed(sender, e);

    private void Collect()
    {
        var s = _settings;
        s.Style = StyleFloating.IsChecked == true ? IslandStyle.Floating : IslandStyle.Notch;
        s.Idle = Selected(IdleCombo, s.Idle);
        s.Monitor = Selected(MonitorCombo, s.Monitor);
        s.Scale = Math.Round(ScaleSlider.Value, 2);
        s.TopOffset = OffsetSlider.Value;
        s.AccentMode = Selected(AccentCombo, s.AccentMode);
        s.ExpandOnHover = HoverSwitch.IsChecked == true;
        s.HideWhenIdle = HideIdleSwitch.IsChecked == true;
        s.HideInFullscreen = FullscreenSwitch.IsChecked == true;
        s.HideFromCapture = CaptureSwitch.IsChecked == true;

        if (StartupSwitch.IsChecked != StartupService.IsEnabled)
        {
            try
            {
                StartupService.SetEnabled(StartupSwitch.IsChecked == true);
            }
            catch (Exception ex)
            {
                App.Log(ex);
            }
        }

        s.SpotifyOnly = SpotifyOnlySwitch.IsChecked == true;
        s.ShowTrackPeek = PeekSwitch.IsChecked == true;
        s.PeekSeconds = PeekSlider.Value;
        s.ReactiveEqualizer = ReactiveSwitch.IsChecked == true;
        s.CompactCenter = Selected(CompactCenterCombo, s.CompactCenter);

        s.Use24Hour = H24Switch.IsChecked == true;
        s.ShowSeconds = SecondsSwitch.IsChecked == true;
        s.DateStyle = Selected(DateCombo, s.DateStyle);
        s.CustomDateFormat = CustomDateBox.Text;
        s.ShowWeekNumber = WeekSwitch.IsChecked == true;

        s.UseCurrentLocation = LocationSwitch.IsChecked == true;
        s.Unit = Selected(UnitCombo, s.Unit);
        s.WeatherRefreshMinutes = Selected(WeatherRefreshCombo, s.WeatherRefreshMinutes);
        s.CurrencyRefreshMinutes = Selected(CurrencyRefreshCombo, s.CurrencyRefreshMinutes);

        s.ShowNotifications = NotificationsSwitch.IsChecked == true;
        s.VolumeHud = VolumeHudSwitch.IsChecked == true;
        s.WheelControlsVolume = WheelVolumeSwitch.IsChecked == true;
        s.BatteryAlerts = BatterySwitch.IsChecked == true;
        s.PrivacyIndicators = PrivacySwitch.IsChecked == true;
        s.TimerLiveActivity = TimerLiveSwitch.IsChecked == true;
        s.RespectDoNotDisturb = DndSwitch.IsChecked == true;
        s.CalendarLiveActivity = CalendarSwitch.IsChecked == true;
        s.CalendarLeadMinutes = Selected(CalendarLeadCombo, s.CalendarLeadMinutes);
        s.AutoCheckUpdates = AutoUpdateSwitch.IsChecked == true;

        if (LocalCurrencyCombo.SelectedItem is CurrencyItem local && local.Code != s.LocalCurrency)
        {
            s.LocalCurrency = local.Code;
            s.Currencies.Remove(local.Code);
            BuildCurrencyChips();
        }
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        _saveTimer.Stop();
        _settings.Save();
    }

    private void UpdateLabels()
    {
        ScaleLabel.Text = $"{ScaleSlider.Value * 100:0}%";
        foreach (Button preset in SizePresets.Children)
        {
            var active = Math.Abs((double)preset.Tag - ScaleSlider.Value) < 0.001;
            preset.BorderBrush = active ? (Brush)FindResource("Brand") : (Brush)FindResource("CardBorder");
            preset.BorderThickness = new Thickness(active ? 2 : 1);
        }
        OffsetLabel.Text = $"{OffsetSlider.Value:0} px";
        PeekLabel.Text = $"{PeekSlider.Value:0.#} s";
        CustomDateCard.Visibility = _settings.DateStyle == DateStyle.Custom ? Visibility.Visible : Visibility.Collapsed;
        Swatches.Opacity = _settings.AccentMode == AccentMode.Custom ? 1 : 0.45;
        foreach (Button swatch in Swatches.Children)
            swatch.BorderThickness = new Thickness(string.Equals((string)swatch.Tag, _settings.CustomAccent, StringComparison.OrdinalIgnoreCase) && _settings.AccentMode == AccentMode.Custom ? 2 : 0);
    }

    private void UpdatePreviews()
    {
        var now = DateTime.Now;
        ClockPreviewTime.Text = TimeFormats.Time(now, _settings);
        ClockPreviewDate.Text = TimeFormats.Date(now, _settings);
        HeroTime.Text = TimeFormats.Time(now, _settings, seconds: false);

        // Vista previa de la forma en la portada.
        if (_settings.Style == IslandStyle.Notch)
        {
            HeroNotch.Data = IslandShape.Build(190, 34, 16, IslandShape.NotchEar);
            HeroNotch.Margin = new Thickness(0);
            HeroNotchContent.Margin = new Thickness(0, 8, 0, 0);
        }
        else
        {
            HeroNotch.Data = IslandShape.Build(190, 32, 16, 0);
            HeroNotch.Margin = new Thickness(0, 10, 0, 0);
            HeroNotchContent.Margin = new Thickness(0, 17, 0, 0);
        }
    }

    // ───────────────────────── Páginas ─────────────────────────

    private void BuildPagesList()
    {
        PagesList.Children.Clear();
        for (var i = 0; i < _settings.Pages.Count; i++)
        {
            var entry = _settings.Pages[i];
            var index = i;
            var (glyph, title, description) = PageInfo(entry.Kind);

            var toggle = new CheckBox { Style = (Style)FindResource("Switch"), IsChecked = entry.Enabled, Margin = new Thickness(12, 0, 0, 0) };
            toggle.Click += (_, _) =>
            {
                entry.Enabled = toggle.IsChecked == true;
                SaveNow();
            };

            var up = new Button { Content = "", Style = (Style)FindResource("IconButton"), IsEnabled = i > 0, ToolTip = Strings.T("Subir", "Move up") };
            up.Click += (_, _) => MovePage(index, -1);
            var down = new Button { Content = "", Style = (Style)FindResource("IconButton"), IsEnabled = i < _settings.Pages.Count - 1, ToolTip = Strings.T("Bajar", "Move down") };
            down.Click += (_, _) => MovePage(index, 1);

            var controls = new StackPanel { Orientation = Orientation.Horizontal };
            controls.Children.Add(up);
            controls.Children.Add(down);
            controls.Children.Add(toggle);

            PagesList.Children.Add(new SettingCard
            {
                Glyph = glyph,
                Header = $"{i + 1}.  {title}",
                Description = description,
                Content = controls,
            });
        }
    }

    private void MovePage(int index, int delta)
    {
        var target = index + delta;
        if (target < 0 || target >= _settings.Pages.Count) return;
        (_settings.Pages[index], _settings.Pages[target]) = (_settings.Pages[target], _settings.Pages[index]);
        BuildPagesList();
        SaveNow();
    }

    private static (string glyph, string title, string description) PageInfo(PageKind kind) => kind switch
    {
        PageKind.Music => ("", Strings.T("Música", "Music"), Strings.T("Carátula, progreso y controles", "Album art, progress and controls")),
        PageKind.Clock => ("", Strings.T("Reloj y fecha", "Clock & date"), Strings.T("Hora grande, fecha y avance del día", "Big time, date and day progress")),
        PageKind.Weather => ("", Strings.T("Clima", "Weather"), Strings.T("Temperatura actual y próximas horas", "Current temperature and next hours")),
        PageKind.Currency => ("", Strings.T("Monedas", "Currencies"), Strings.T("Tipo de cambio y variación diaria", "Exchange rate and daily change")),
        PageKind.Lyrics => ("", Strings.T("Letras", "Lyrics"), Strings.T("Letra sincronizada de la canción (LRCLIB)", "Synced song lyrics (LRCLIB)")),
        PageKind.Timer => ("", Strings.T("Temporizador y cronómetro", "Timer & stopwatch"), Strings.T("Cuenta regresiva en vivo, visible en la isla compacta", "Live countdown, visible on the compact island")),
        PageKind.Volume => ("\uE767", Strings.T("Volumen", "Volume"), Strings.T("El sistema y cada aplicación con audio, con su propio deslizador", "The system and each app playing audio, each with its own slider")),
        PageKind.Widgets => ("", Strings.T("Widgets", "Widgets"), Strings.T("Datos de la comunidad desde cualquier API pública", "Community data from any public API")),
        _ => ("", Strings.T("Relojes mundiales", "World clocks"), Strings.T("Esferas analógicas de otras ciudades", "Analog faces for other cities")),
    };

    // ───────────────────────── Relojes mundiales ─────────────────────────

    private void BuildWorldList()
    {
        WorldList.Children.Clear();
        foreach (var entry in _settings.WorldClocks.ToList())
        {
            TimeZoneInfo? zone = null;
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(entry.TimeZoneId);
            }
            catch
            {
                // Zona que ya no existe en este sistema.
            }

            var label = new TextBox { Text = entry.Label, Width = 180 };
            label.TextChanged += (_, _) =>
            {
                entry.Label = label.Text;
                ScheduleSave();
            };
            var remove = new Button { Content = "", Style = (Style)FindResource("IconButton"), ToolTip = Strings.T("Quitar", "Remove") };
            remove.Click += (_, _) =>
            {
                _settings.WorldClocks.Remove(entry);
                BuildWorldList();
                SaveNow();
            };

            var controls = new StackPanel { Orientation = Orientation.Horizontal };
            controls.Children.Add(label);
            controls.Children.Add(remove);

            var now = zone is null ? (DateTime?)null : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
            WorldList.Children.Add(new SettingCard
            {
                Glyph = "",
                Header = string.IsNullOrWhiteSpace(entry.Label) ? zone?.StandardName ?? entry.TimeZoneId : entry.Label,
                Description = zone is null ? entry.TimeZoneId : $"{zone.DisplayName}  ·  {TimeFormats.Time(now!.Value, _settings, seconds: false)}",
                Content = controls,
            });
        }
    }

    private void ZoneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ZoneCombo.SelectedItem is not TimeZoneInfo zone) return;
        // "(UTC-05:00) Eastern Time (US & Canada)" → "Eastern Time"
        var name = zone.DisplayName;
        var start = name.IndexOf(") ", StringComparison.Ordinal);
        if (start >= 0) name = name[(start + 2)..];
        var cut = name.IndexOfAny([',', '(']);
        ZoneLabelBox.Text = (cut > 0 ? name[..cut] : name).Trim();
    }

    private void AddZone_Click(object sender, RoutedEventArgs e)
    {
        if (ZoneCombo.SelectedItem is not TimeZoneInfo zone || _settings.WorldClocks.Count >= 4) return;
        _settings.WorldClocks.Add(new WorldClockEntry { TimeZoneId = zone.Id, Label = ZoneLabelBox.Text.Trim() });
        ZoneCombo.SelectedItem = null;
        ZoneLabelBox.Text = "";
        BuildWorldList();
        SaveNow();
    }

    // ───────────────────────── Pantallas ─────────────────────────

    /// <summary>"Principal", "Todas" y cada monitor conectado ("Pantalla 2 · 2560 × 1440").</summary>
    private void BuildMonitorChoices()
    {
        var items = new List<(object value, string text)>
        {
            ("", Strings.T("Principal", "Main display")),
            ("*", Strings.T("Todas las pantallas", "All displays")),
        };
        foreach (var screen in System.Windows.Forms.Screen.AllScreens.OrderBy(s => s.DeviceName))
        {
            var number = new string(screen.DeviceName.Where(char.IsDigit).ToArray());
            var text = Strings.F("Pantalla {0} · {1} × {2}", "Display {0} · {1} × {2}", number, screen.Bounds.Width, screen.Bounds.Height);
            if (screen.Primary) text += " " + Strings.T("(principal)", "(main)");
            items.Add((screen.DeviceName, text));
        }
        Fill(MonitorCombo, items.ToArray());
    }

    private void SelectMonitor(string monitor)
    {
        // Un monitor guardado que ya no está conectado se muestra igual, para no perder la elección.
        if (!MonitorCombo.Items.OfType<ComboBoxItem>().Any(i => Equals(i.Tag, monitor)))
            MonitorCombo.Items.Add(new ComboBoxItem { Content = Strings.T("Pantalla desconectada (usa la principal)", "Disconnected display (uses the main one)"), Tag = monitor });
        Select(MonitorCombo, monitor);
    }

    // ───────────────────────── Calendario ─────────────────────────

    private void BuildCalendarList()
    {
        CalendarList.Children.Clear();
        foreach (var url in _settings.CalendarUrls.ToList())
        {
            var remove = new Button { Content = "\uE711", Style = (Style)FindResource("IconButton"), ToolTip = Strings.T("Quitar", "Remove") };
            remove.Click += (_, _) =>
            {
                _settings.CalendarUrls.Remove(url);
                BuildCalendarList();
                SaveNow();
            };
            CalendarList.Children.Add(new SettingCard
            {
                Glyph = "\uE787",
                Header = CalendarName(url),
                Description = Strings.T("Enlace privado · no lo compartas", "Private link · do not share it"),
                Content = remove,
            });
        }
        UpdateCalendarStatus();
    }

    /// <summary>Nombre corto del calendario a partir del enlace, sin mostrar la parte secreta.</summary>
    private static string CalendarName(string url)
    {
        if (!Uri.TryCreate(CalendarService.NormalizeUrl(url), UriKind.Absolute, out var uri)) return url;
        return uri.Host switch
        {
            "calendar.google.com" => "Google Calendar",
            var host when host.Contains("outlook", StringComparison.OrdinalIgnoreCase) || host.Contains("office", StringComparison.OrdinalIgnoreCase) => "Outlook",
            var host => host,
        };
    }

    private void CalendarUrl_Changed(object sender, TextChangedEventArgs e)
        => CalendarUrlHint.Visibility = CalendarUrlBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void AddCalendar_Click(object sender, RoutedEventArgs e)
    {
        var url = CalendarUrlBox.Text.Trim();
        var normalized = CalendarService.NormalizeUrl(url);
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            CalendarStatus.Text = Strings.T("El enlace debe empezar con https:// o webcal://", "The link must start with https:// or webcal://");
            return;
        }
        if (!_settings.CalendarUrls.Contains(url)) _settings.CalendarUrls.Add(url);
        CalendarUrlBox.Text = "";
        CalendarStatus.Text = Strings.T("Leyendo el calendario…", "Reading the calendar…");
        BuildCalendarList();
        SaveNow();
    }

    private void UpdateCalendarStatus()
    {
        var calendar = _services.Calendar;
        if (_settings.CalendarUrls.Count == 0)
        {
            CalendarStatus.Text = Strings.T("Aún no hay calendarios.", "No calendars yet.");
            return;
        }
        if (calendar.LastUpdated == default) return;

        var today = calendar.Events.Count(ev => ev.Start > DateTime.Now && ev.Start.Date == DateTime.Today);
        var text = today == 0
            ? Strings.T("No quedan reuniones hoy.", "No more meetings today.")
            : Strings.F("{0} reuniones más hoy.", "{0} more meetings today.", today);
        if (calendar.Events.FirstOrDefault(ev => ev.Start > DateTime.Now) is { } next)
            text += " " + Strings.F("Próxima: {0} a las {1}.", "Next: {0} at {1}.", next.Title, TimeFormats.Time(next.Start, _settings, seconds: false));
        if (calendar.FailedSources > 0)
            text += " " + Strings.F("{0} calendario(s) no se pudieron leer: revisa el enlace.", "{0} calendar(s) could not be read: check the link.", calendar.FailedSources);
        CalendarStatus.Text = text;
    }

    // ───────────────────────── Clima ─────────────────────────

    private void BuildCitiesList()
    {
        CitiesList.Children.Clear();
        foreach (var city in _settings.Cities.ToList())
        {
            var remove = new Button { Content = "", Style = (Style)FindResource("IconButton"), ToolTip = Strings.T("Quitar", "Remove") };
            remove.Click += (_, _) =>
            {
                _settings.Cities.Remove(city);
                BuildCitiesList();
                SaveNow();
            };
            CitiesList.Children.Add(new SettingCard
            {
                Glyph = "",
                Header = city.Name,
                Description = $"{city.Country}  ·  {city.Latitude:0.00}, {city.Longitude:0.00}",
                Content = remove,
            });
        }
    }

    private void CitySearch_Changed(object sender, TextChangedEventArgs e)
    {
        CitySearchHint.Visibility = CitySearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private async Task SearchCitiesAsync()
    {
        _search?.Cancel();
        var cts = _search = new CancellationTokenSource();
        var query = CitySearchBox.Text;
        CityResults.Children.Clear();
        if (query.Trim().Length < 2) return;

        var results = await LocationService.SearchAsync(query, cts.Token);
        if (cts.IsCancellationRequested) return;

        CityResults.Children.Clear();
        if (results.Count == 0)
        {
            CityResults.Children.Add(new TextBlock { Text = Strings.T("Sin resultados", "No results"), Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(4, 6, 0, 0) });
            return;
        }

        foreach (var city in results)
        {
            var text = new StackPanel { Orientation = Orientation.Horizontal };
            text.Children.Add(new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            text.Children.Add(new TextBlock { Text = city.Name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            text.Children.Add(new TextBlock { Text = $"   {city.Country}", Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center });

            var button = new Button
            {
                Content = text, HorizontalContentAlignment = HorizontalAlignment.Left, HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 2, 0, 0), Padding = new Thickness(12, 8, 12, 8),
            };
            button.Click += (_, _) =>
            {
                if (_settings.Cities.Any(c => Math.Abs(c.Latitude - city.Latitude) < 0.01 && Math.Abs(c.Longitude - city.Longitude) < 0.01)) return;
                _settings.Cities.Add(city);
                CitySearchBox.Text = "";
                CityResults.Children.Clear();
                BuildCitiesList();
                SaveNow();
            };
            CityResults.Children.Add(button);
        }
    }

    // ───────────────────────── Monedas ─────────────────────────

    private void BuildCurrencyChips()
    {
        CurrencyChips.Children.Clear();
        var items = LocalCurrencyCombo.ItemsSource as IEnumerable<CurrencyItem>;
        foreach (var code in _settings.Currencies.ToList())
        {
            var name = items?.FirstOrDefault(i => i.Code == code)?.Name ?? "";
            var chip = new StackPanel { Orientation = Orientation.Horizontal };
            chip.Children.Add(new TextBlock { Text = CurrencyService.SymbolOf(code), FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Brand"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            chip.Children.Add(new TextBlock { Text = code.ToUpperInvariant(), FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            if (name.Length > 0)
                chip.Children.Add(new TextBlock { Text = $"  {name}", Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 170, TextTrimming = TextTrimming.CharacterEllipsis });
            var remove = new Button { Content = "", Style = (Style)FindResource("IconButton"), Width = 26, Height = 26, FontSize = 10, Margin = new Thickness(10, 0, 0, 0) };
            remove.Click += (_, _) =>
            {
                _settings.Currencies.Remove(code);
                BuildCurrencyChips();
                SaveNow();
            };
            chip.Children.Add(remove);

            CurrencyChips.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(18), Background = (Brush)FindResource("CardBrush"),
                BorderBrush = (Brush)FindResource("CardBorder"), BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 4, 4, 4), Margin = new Thickness(0, 0, 8, 8), Child = chip,
            });
        }
    }

    private void AddCurrency_Click(object sender, RoutedEventArgs e)
    {
        var item = AddCurrencyCombo.SelectedItem as CurrencyItem;
        var code = item?.Code ?? AddCurrencyCombo.Text.Split(' ')[0].Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(code) || code == _settings.LocalCurrency || _settings.Currencies.Contains(code) || _settings.Currencies.Count >= 4) return;
        _settings.Currencies.Add(code);
        AddCurrencyCombo.SelectedItem = null;
        AddCurrencyCombo.Text = "";
        BuildCurrencyChips();
        SaveNow();
    }

    // ───────────────────────── Acerca de ─────────────────────────

    // ───────────────────────── Sistema ─────────────────────────

    private void OnNotificationAccessChanged() => Dispatcher.BeginInvoke(UpdateNotificationInfo);

    private void UpdateNotificationInfo()
    {
        var message = _services.Notifications.Access switch
        {
            NotificationAccess.RequiresPackage => Strings.T(
                "Windows solo comparte las notificaciones con apps instaladas como paquete (Microsoft Store o MSIX). En la versión portable esta opción queda en espera.",
                "Windows only shares notifications with apps installed as a package (Microsoft Store or MSIX). In the portable version this option is on hold."),
            NotificationAccess.Denied => Strings.T(
                "Permiso denegado. Actívalo en Configuración de Windows → Privacidad → Notificaciones.",
                "Permission denied. Enable it in Windows Settings → Privacy → Notifications."),
            _ => null,
        };
        NotificationsInfoText.Text = message ?? "";
        NotificationsInfo.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
    }

    // ───────────────────────── Widgets ─────────────────────────

    private void OnWidgetsUpdated() => Dispatcher.BeginInvoke(BuildWidgetsList);

    private void BuildWidgetsList()
    {
        WidgetsList.Children.Clear();
        var widgets = _services.Widgets.Widgets;
        if (widgets.Count == 0)
        {
            WidgetsList.Children.Add(new TextBlock
            {
                Text = Strings.T("Aún no hay widgets. Pulsa \"Instalar ejemplos\" para empezar.", "No widgets yet. Click \"Install examples\" to start."),
                Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(2, 0, 0, 0),
            });
            return;
        }

        foreach (var widget in widgets)
        {
            var status = widget.Error ?? (widget.Values.Count > 0
                ? string.Join("  ·  ", widget.Values.Select(v => $"{v.Label}: {v.Text}"))
                : Strings.Loading);
            WidgetsList.Children.Add(new SettingCard
            {
                Glyph = "",
                Header = $"{widget.Definition.Icon}  {widget.Definition.Name}",
                Description = $"{widget.File}  ·  {status}",
            });
        }
    }

    private void OpenWidgets_Click(object sender, RoutedEventArgs e)
    {
        System.IO.Directory.CreateDirectory(WidgetService.Folder);
        Process.Start(new ProcessStartInfo(WidgetService.Folder) { UseShellExecute = true });
    }

    private void ReloadWidgets_Click(object sender, RoutedEventArgs e)
    {
        _services.Widgets.Reload();
        BuildWidgetsList();
    }

    private void ExampleWidgets_Click(object sender, RoutedEventArgs e)
    {
        _services.Widgets.InstallExamples();
        var widgetsPage = _settings.Pages.FirstOrDefault(p => p.Kind == PageKind.Widgets);
        if (widgetsPage is { Enabled: false })
        {
            widgetsPage.Enabled = true;
            BuildPagesList();
            SaveNow();
        }
        BuildWidgetsList();
    }

    private void WidgetGuide_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo($"{AppInfo.RepositoryUrl}/tree/main/widgets#readme") { UseShellExecute = true });

    // ───────────────────────── Actualizaciones ─────────────────────────

    private void UpdateUpdateCard()
    {
        _update ??= (Application.Current as App)?.AvailableUpdate;
        if (_update is not null)
        {
            UpdateTitle.Text = Strings.F("Hay una nueva versión: {0}", "A new version is available: {0}", _update.Tag);
            UpdateDetail.Text = Strings.T("Se descargará desde GitHub y se verificará su SHA-256 antes de instalarla.", "It will be downloaded from GitHub and its SHA-256 verified before installing.");
            UpdateButton.Content = Strings.T("Actualizar ahora", "Update now");
        }
        else
        {
            UpdateTitle.Text = Strings.F("Tienes la versión {0}", "You have version {0}", AppInfo.Version);
            UpdateDetail.Text = _settings.LastUpdateCheck == default
                ? Strings.T("Aún no se han buscado actualizaciones.", "No update check yet.")
                : Strings.F("Última revisión: {0:g}", "Last checked: {0:g}", _settings.LastUpdateCheck);
            UpdateButton.Content = Strings.T("Buscar ahora", "Check now");
        }
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_updating || Application.Current is not App app) return;
        _updating = true;
        UpdateButton.IsEnabled = false;
        try
        {
            if (_update is null)
            {
                UpdateDetail.Text = Strings.T("Buscando…", "Checking…");
                await app.CheckForUpdatesAsync(automatic: false);
                _update = app.AvailableUpdate;
                UpdateUpdateCard();
                return;
            }

            UpdateProgress.Visibility = Visibility.Visible;
            UpdateDetail.Text = Strings.T("Descargando y verificando…", "Downloading and verifying…");
            await UpdateService.InstallAsync(_update, new Progress<double>(p => UpdateProgress.Value = p));
        }
        catch (Exception ex)
        {
            App.Log(ex);
            UpdateDetail.Text = ex is System.IO.InvalidDataException ? ex.Message : Strings.T("No se pudo actualizar. Revisa tu conexión.", "Update failed. Check your connection.");
            UpdateProgress.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _updating = false;
            UpdateButton.IsEnabled = true;
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        System.IO.Directory.CreateDirectory(AppSettings.Folder);
        Process.Start(new ProcessStartInfo(AppSettings.Folder) { UseShellExecute = true });
    }

    private void Developer_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(AppInfo.DeveloperUrl) { UseShellExecute = true });
        e.Handled = true;
    }

    private void License_Click(object sender, RoutedEventArgs e)
        => MessageBox.Show(this, AppInfo.LicenseText(), Strings.T("Licencia MIT", "MIT License"), MessageBoxButton.OK, MessageBoxImage.Information);

    private void Source_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo(AppInfo.RepositoryUrl) { UseShellExecute = true });

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this,
            Strings.T("¿Restablecer todos los ajustes a los valores de fábrica?", "Reset all settings to factory defaults?"),
            Strings.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        _settings.CopyFrom(new AppSettings());
        LoadValues();
        ApplyLanguage();
        if (LocalCurrencyCombo.ItemsSource is IEnumerable<CurrencyItem> items)
            LocalCurrencyCombo.SelectedItem = items.FirstOrDefault(i => i.Code == _settings.LocalCurrency);
        SaveNow();
    }

    // ───────────────────────── Utilidades ─────────────────────────

    private static void Fill(ComboBox combo, params (object value, string text)[] items)
    {
        combo.Items.Clear();
        foreach (var (value, text) in items) combo.Items.Add(new ComboBoxItem { Content = text, Tag = value });
    }

    private static void Select(ComboBox combo, object value)
        => combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, value));

    private static T Selected<T>(ComboBox combo, T fallback)
        => combo.SelectedItem is ComboBoxItem { Tag: T value } ? value : fallback;

    private static string Minutes(int minutes) => minutes >= 60
        ? $"{minutes / 60} h"
        : Strings.F("{0} minutos", "{0} minutes", minutes);

    private static string DateStyleName(DateStyle style) => style switch
    {
        DateStyle.Long => Strings.T("Largo", "Long"),
        DateStyle.Full => Strings.T("Completo", "Full"),
        DateStyle.Medium => Strings.T("Medio", "Medium"),
        DateStyle.Short => Strings.T("Corto", "Short"),
        DateStyle.Iso => "ISO 8601",
        _ => Strings.T("Personalizado", "Custom"),
    };

    private static ControlTemplate SwatchTemplate(Color color)
    {
        var template = new ControlTemplate(typeof(Button));
        var ring = new FrameworkElementFactory(typeof(Border));
        ring.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
        ring.SetValue(Border.BackgroundProperty, new SolidColorBrush(color));
        ring.SetValue(Border.BorderBrushProperty, Brushes.White);
        ring.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(BorderThickness))
        {
            RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent,
        });
        template.VisualTree = ring;
        return template;
    }
}
