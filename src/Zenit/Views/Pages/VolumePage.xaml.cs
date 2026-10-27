using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Zenit.Controls;
using Zenit.Services;

namespace Zenit.Views.Pages;

/// <summary>Mezclador de volumen dentro de la isla: el sistema y cada aplicación con audio.</summary>
public partial class VolumePage : UserControl, IIslandPage
{
    private const int MaxApps = 3;

    private static readonly Brush Muted = IslandTheme.InkAlpha(0x8C);
    private static readonly Brush IconBackground = IslandTheme.InkAlpha(0x26);
    private static readonly Brush OutputSelected = IslandTheme.InkAlpha(0x33);
    private static readonly Brush OutputIdle = IslandTheme.InkAlpha(0x14);
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
        Unloaded += (_, _) =>
        {
            _volume.Changed -= OnVolumeChanged;
            ShowOutputs(false);
        };
    }

    private bool _choosingOutput;

    private static string GlyphOf(AudioOutputKind kind) => kind switch
    {
        AudioOutputKind.Speakers => "",
        AudioOutputKind.Headphones => "",
        AudioOutputKind.Bluetooth => "",
        AudioOutputKind.Display => "",
        _ => "",
    };

    /// <summary>"Altavoces (Realtek(R) Audio)" → "Altavoces": el adaptador entre paréntesis sobra en el chip.</summary>
    private static string ShortName(string name)
    {
        var cut = name.IndexOf(" (", StringComparison.Ordinal);
        return cut > 0 ? name[..cut] : name;
    }

    /// <summary>Chip de la salida actual; solo se puede tocar si hay más de una.</summary>
    private void RefreshOutput(IReadOnlyList<AudioOutput> outputs)
    {
        var current = outputs.FirstOrDefault(o => o.IsDefault);
        OutputButton.Visibility = current is null ? Visibility.Collapsed : Visibility.Visible;
        if (current is null) return;
        OutputGlyph.Text = GlyphOf(current.Kind);
        OutputName.Text = ShortName(current.Name);
        OutputButton.ToolTip = current.Name;
        var choice = outputs.Count > 1;
        OutputChevron.Visibility = choice ? Visibility.Visible : Visibility.Collapsed;
        OutputButton.IsHitTestVisible = choice;
    }

    private void OutputButton_Click(object sender, RoutedEventArgs e) => ShowOutputs(!_choosingOutput);

    private void ShowOutputs(bool show)
    {
        _choosingOutput = show;
        Rows.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        OutputsHost.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        OutputChevron.Text = show ? "" : "";
        if (show) BuildOutputs(_volume.Outputs());
    }

    private void BuildOutputs(IReadOnlyList<AudioOutput> outputs)
    {
        OutputsList.Children.Clear();
        foreach (var output in outputs)
        {
            var row = new Grid { Width = 366 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = GlyphOf(output.Kind), FontFamily = GlyphFont, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
            var name = new TextBlock { Text = output.Name, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(name, 1);
            row.Children.Add(name);
            if (output.IsDefault)
            {
                var check = new TextBlock { Text = "", FontFamily = GlyphFont, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                Grid.SetColumn(check, 2);
                row.Children.Add(check);
            }

            var button = new Button
            {
                Style = (Style)FindResource("ChipButton"), Content = row, ToolTip = output.Name,
                Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 0, 4),
                Background = output.IsDefault ? OutputSelected : OutputIdle,
            };
            button.Click += (_, _) =>
            {
                if (!output.IsDefault) _volume.SelectOutput(output.Id);
                ShowOutputs(false);
                RefreshOutput(_volume.Outputs());
                Sync();
            };
            OutputsList.Children.Add(button);
        }
    }

    private void OnVolumeChanged(float level, bool muted) => Sync();

    public PageKind Kind => PageKind.Volume;

    /// <summary>Cada segundo con la isla expandida: relee los niveles y, cada pocos segundos, qué apps tienen audio.</summary>
    public void Refresh()
    {
        if (DateTime.UtcNow - _lastScan > TimeSpan.FromSeconds(3) && !_rows.Any(r => r.Slider.IsMouseCaptureWithin))
        {
            _lastScan = DateTime.UtcNow;
            RefreshOutput(_volume.Outputs());
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
                ? new TextBlock { Text = fallbackGlyph, FontFamily = GlyphFont, FontSize = 13, Foreground = IslandTheme.Ink, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                : new Image { Source = icon, Width = 22, Height = 22 },
        };

        var label = new TextBlock
        {
            Text = name, Width = 104, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = IslandTheme.Ink,
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
                row.Mute.Foreground = muted ? Muted : IslandTheme.Ink;
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
