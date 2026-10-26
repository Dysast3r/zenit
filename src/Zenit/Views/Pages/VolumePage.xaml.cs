using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Zenit.Services;

namespace Zenit.Views.Pages;

/// <summary>Mezclador de volumen dentro de la isla: el sistema y cada aplicación con audio.</summary>
public partial class VolumePage : UserControl, IIslandPage
{
    private const int MaxApps = 3;

    private static readonly Brush Muted = Frozen(Color.FromArgb(0x8C, 255, 255, 255));
    private static readonly Brush IconBackground = Frozen(Color.FromArgb(0x26, 255, 255, 255));
    private static readonly FontFamily GlyphFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    /// <summary>Una fila: sus controles son fijos; lo que cambia es a qué salida (el sistema o una app) apuntan.</summary>
    private sealed class Row
    {
        public Slider Slider { get; } = new();
        public Button Mute { get; } = new();
        public Func<float> Read { get; set; } = () => 0;
        public Action<float> Write { get; set; } = _ => { };
        public Func<bool> ReadMuted { get; set; } = () => false;
        public Action ToggleMute { get; set; } = () => { };

        public void Bind(AudioApp app)
        {
            Read = () => app.Volume;
            Write = level => app.Volume = level;
            ReadMuted = () => app.Muted;
            ToggleMute = () => app.Muted = !app.Muted;
        }
    }

    private readonly VolumeService _volume;

    /// <summary>Propio de esta página: con la isla en varias pantallas, cada una libera solo sus sesiones.</summary>
    private readonly AudioSessions _sessions = new();
    private readonly List<Row> _rows = [];
    private string _layout = "";
    private DateTime _lastScan;
    private bool _syncing;

    public VolumePage(VolumeService volume)
    {
        _volume = volume;
        InitializeComponent();
        // Solo mientras está en una isla abierta: así una isla cerrada no queda enganchada al servicio.
        Loaded += (_, _) => _volume.Changed += OnVolumeChanged;
        Unloaded += (_, _) => _volume.Changed -= OnVolumeChanged;
    }

    private void OnVolumeChanged(float level, bool muted) => Sync();

    public PageKind Kind => PageKind.Volume;

    /// <summary>Cada segundo con la isla expandida: relee los niveles y, cada pocos segundos, qué apps tienen audio.</summary>
    public void Refresh()
    {
        if (DateTime.UtcNow - _lastScan > TimeSpan.FromSeconds(3) && !_rows.Any(r => r.Slider.IsMouseCaptureWithin))
        {
            _lastScan = DateTime.UtcNow;
            var apps = _sessions.Refresh().Take(MaxApps).ToList();
            var layout = string.Join('|', apps.Select(a => $"{a.ProcessId}:{a.Name}"));
            if (layout != _layout || _rows.Count == 0)
            {
                _layout = layout;
                Build(apps);
            }
            else
            {
                // Mismas apps: se conservan los controles y solo se apunta a las sesiones recién leídas.
                for (var i = 0; i < apps.Count; i++) _rows[i + 1].Bind(apps[i]);
            }
        }
        Sync();
    }

    private void Build(List<AudioApp> apps)
    {
        Rows.Children.Clear();
        _rows.Clear();

        var system = new Row
        {
            Read = () => _volume.Current.level,
            Write = _volume.Set,
            ReadMuted = () => _volume.Current.muted,
            ToggleMute = _volume.ToggleMute,
        };
        AddRow(system, Strings.T("Sistema", "System"), null, "");

        foreach (var app in apps)
        {
            var row = new Row();
            row.Bind(app);
            AddRow(row, app.Name, app.Icon, "");
        }
    }

    private void AddRow(Row row, string name, ImageSource? icon, string fallbackGlyph)
    {
        var badge = new Border
        {
            Width = 26, Height = 26, CornerRadius = new CornerRadius(7),
            Background = icon is null ? IconBackground : Brushes.Transparent,
            Child = icon is null
                ? new TextBlock { Text = fallbackGlyph, FontFamily = GlyphFont, FontSize = 13, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                : new Image { Source = icon, Width = 22, Height = 22 },
        };

        var label = new TextBlock
        {
            Text = name, Width = 104, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0),
        };

        row.Slider.Style = (Style)FindResource("IslandSlider");
        row.Slider.ValueChanged += (_, e) =>
        {
            if (!_syncing) row.Write((float)e.NewValue);
        };

        row.Mute.Style = (Style)FindResource("RoundButton");
        row.Mute.Width = row.Mute.Height = 28;
        row.Mute.FontSize = 11;
        row.Mute.Margin = new Thickness(10, 0, 0, 0);
        row.Mute.Click += (_, _) =>
        {
            row.ToggleMute();
            Sync();
        };

        var grid = new Grid { Height = 34 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(label, 1);
        Grid.SetColumn(row.Slider, 2);
        Grid.SetColumn(row.Mute, 3);
        grid.Children.Add(badge);
        grid.Children.Add(label);
        grid.Children.Add(row.Slider);
        grid.Children.Add(row.Mute);

        Rows.Children.Add(grid);
        _rows.Add(row);
    }

    /// <summary>Lleva los niveles reales a los controles, sin pisar el deslizador que el usuario está arrastrando.</summary>
    private void Sync()
    {
        _syncing = true;
        try
        {
            foreach (var row in _rows)
            {
                if (!row.Slider.IsMouseCaptureWithin) row.Slider.Value = row.Read();
                var muted = row.ReadMuted();
                row.Mute.Content = muted ? "" : "";
                row.Mute.Foreground = muted ? Muted : Brushes.White;
                row.Slider.Opacity = muted ? 0.4 : 1;
            }
        }
        catch
        {
            // Una app cerró su audio entre lecturas: la próxima lectura la quita.
        }
        finally
        {
            _syncing = false;
        }
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
