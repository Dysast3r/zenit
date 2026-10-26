using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Zenit.Services;

namespace Zenit.Views.Pages;

/// <summary>Letra sincronizada: la línea actual destacada, con la anterior y la siguiente atenuadas.</summary>
public partial class LyricsPage : UserControl, IIslandPage
{
    private readonly LyricsService _lyrics;
    private readonly Func<MediaSnapshot?> _snapshot;
    private int _index = int.MinValue;

    public LyricsPage(LyricsService lyrics, Func<MediaSnapshot?> snapshot)
    {
        _lyrics = lyrics;
        _snapshot = snapshot;
        InitializeComponent();
        _lyrics.Updated += () =>
        {
            _index = int.MinValue;
            Refresh();
        };
        Refresh();
    }

    public PageKind Kind => PageKind.Lyrics;

    public void Refresh()
    {
        var snapshot = _snapshot();
        HeaderText.Text = snapshot is null ? "" : $"♪  {snapshot.Title} — {snapshot.Artist}";

        string? status = null;
        if (snapshot is null) status = Strings.NothingPlaying;
        else if (_lyrics.IsLoading) status = Strings.T("Buscando la letra…", "Looking for lyrics…");
        else if (_lyrics.Lines.Count == 0)
            status = _lyrics.HasPlainOnly
                ? Strings.T("Esta canción no tiene letra sincronizada", "No synced lyrics for this song")
                : Strings.T("No se encontró la letra", "Lyrics not found");

        StatusText.Text = status ?? "";
        StatusText.Visibility = status is null ? Visibility.Collapsed : Visibility.Visible;
        LinesPanel.Visibility = status is null ? Visibility.Visible : Visibility.Hidden;
        Animate(0);
    }

    /// <summary>Se revisa en cada frame para que el cambio de línea caiga justo en el tiempo.</summary>
    public void Animate(double time)
    {
        var snapshot = _snapshot();
        var lines = _lyrics.Lines;
        if (snapshot is null || lines.Count == 0) return;

        var index = _lyrics.IndexAt(snapshot.CurrentPosition + LyricsService.Lead);
        if (index == _index) return;
        var forward = index > _index && _index != int.MinValue;
        _index = index;

        PreviousLine.Text = index > 0 ? lines[index - 1].Text : "";
        CurrentLine.Text = index >= 0 ? lines[index].Text : "♪";
        NextLine.Text = index + 1 < lines.Count ? lines[index + 1].Text : "";

        if (!forward) return;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        LinesShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(380)) { EasingFunction = ease });
        CurrentLine.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(380)) { EasingFunction = ease });
    }
}
