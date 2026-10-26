using System.Windows;
using Zenit.Interop;
using Zenit.Services;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Zenit.Views;

/// <summary>Icono de la bandeja del sistema con el menú de ajustes.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.Timer _clickTimer;
    private DateTime _lastDoubleClick;
    private Forms.ToolStripMenuItem? _updateItem;
    private string? _announcedTag;

    public TrayIcon(IslandWindow window, Action openSettings, Action exit)
    {
        _menu = new Forms.ContextMenuStrip
        {
            Renderer = new DarkRenderer(),
            ShowImageMargin = false,
            ShowCheckMargin = true,
            Font = new Drawing.Font("Segoe UI", 9.5f),
            Padding = new Forms.Padding(4),
        };

        _menu.Items.Add(new Forms.ToolStripMenuItem(Strings.AppName) { Enabled = false });
        var developer = new Forms.ToolStripMenuItem(Strings.F("Desarrollado por {0}", "Developed by {0}", AppInfo.DeveloperName))
        {
            ForeColor = Drawing.Color.FromArgb(125, 211, 252),
        };
        developer.Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppInfo.DeveloperUrl) { UseShellExecute = true });
        _menu.Items.Add(developer);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        var settings = new Forms.ToolStripMenuItem(Strings.Settings) { Font = new Drawing.Font("Segoe UI", 9.5f, Drawing.FontStyle.Bold) };
        settings.Click += (_, _) => openSettings();
        _menu.Items.Add(settings);
        AddToggle(Strings.T("Iniciar con Windows", "Start with Windows"), () => StartupService.IsEnabled, StartupService.SetEnabled);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        var quit = new Forms.ToolStripMenuItem(Strings.Exit);
        quit.Click += (_, _) => exit();
        _menu.Items.Add(quit);

        foreach (Forms.ToolStripItem item in _menu.Items) item.ForeColor = Drawing.Color.White;
        developer.ForeColor = Drawing.Color.FromArgb(125, 211, 252);

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = Strings.AppName,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        // Un clic abre o cierra la isla, pero se espera el tiempo de doble clic: así el doble clic
        // abre la configuración sin mover la isla de paso.
        _clickTimer = new Forms.Timer { Interval = Forms.SystemInformation.DoubleClickTime };
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            window.Toggle();
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button != Forms.MouseButtons.Left) return;
            if ((DateTime.UtcNow - _lastDoubleClick).TotalMilliseconds < _clickTimer.Interval) return;
            _clickTimer.Stop();
            _clickTimer.Start();
        };
        _icon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button != Forms.MouseButtons.Left) return;
            _clickTimer.Stop();
            _lastDoubleClick = DateTime.UtcNow;
            openSettings();
        };
    }

    /// <summary>Agrega "Actualizar a vX" al menú y avisa con una notificación de Windows (una vez por versión).</summary>
    public void ShowUpdate(UpdateInfo update, Action openSettings)
    {
        if (_updateItem is null)
        {
            _updateItem = new Forms.ToolStripMenuItem
            {
                ForeColor = Drawing.Color.FromArgb(52, 211, 153),
                Font = new Drawing.Font("Segoe UI", 9.5f, Drawing.FontStyle.Bold),
            };
            _updateItem.Click += (_, _) => openSettings();
            _menu.Items.Insert(2, _updateItem);
        }
        _updateItem.Text = Strings.F("Actualizar a {0}", "Update to {0}", update.Tag);

        if (update.Tag == _announcedTag) return;
        _announcedTag = update.Tag;
        _icon.ShowBalloonTip(5000, Strings.AppName,
            Strings.F("Hay una nueva versión: {0}", "A new version is available: {0}", update.Tag), Forms.ToolTipIcon.Info);
    }

    public void ShowMenuAtCursor()
    {
        _menu.Show(Forms.Cursor.Position);
        // La isla nunca se activa; sin esto el menú no se cerraría al hacer clic fuera.
        NativeMethods.SetForegroundWindow(_menu.Handle);
    }

    /// <summary>Opción con casilla; se relee al abrir el menú por si cambió desde la configuración.</summary>
    private void AddToggle(string text, Func<bool> read, Action<bool> onChanged)
    {
        var item = new Forms.ToolStripMenuItem(text) { CheckOnClick = true, Checked = read() };
        _menu.Opening += (_, _) => item.Checked = read();
        // Click (no CheckedChanged): CheckOnClick ya marcó la casilla y la relectura de arriba no dispara nada.
        item.Click += (_, _) =>
        {
            try
            {
                onChanged(item.Checked);
            }
            catch (Exception ex)
            {
                App.Log(ex);
            }
        };
        _menu.Items.Add(item);
    }

    private static Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/icon.ico"));
        using var stream = resource!.Stream;
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _clickTimer.Dispose();
        _menu.Dispose();
    }

    private sealed class DarkRenderer() : Forms.ToolStripProfessionalRenderer(new DarkColors())
    {
        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Drawing.Color.White : Drawing.Color.FromArgb(150, 150, 160);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = e.ImageRectangle;
            using var pen = new Drawing.Pen(Drawing.Color.FromArgb(236, 72, 153), 2f)
            {
                StartCap = Drawing.Drawing2D.LineCap.Round,
                EndCap = Drawing.Drawing2D.LineCap.Round,
                LineJoin = Drawing.Drawing2D.LineJoin.Round,
            };
            g.DrawLines(pen, [
                new Drawing.PointF(r.Left + r.Width * 0.2f, r.Top + r.Height * 0.52f),
                new Drawing.PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.74f),
                new Drawing.PointF(r.Left + r.Width * 0.82f, r.Top + r.Height * 0.3f),
            ]);
        }
    }

    private sealed class DarkColors : Forms.ProfessionalColorTable
    {
        private static readonly Drawing.Color Background = Drawing.Color.FromArgb(28, 28, 32);
        private static readonly Drawing.Color Hover = Drawing.Color.FromArgb(52, 52, 60);
        private static readonly Drawing.Color Line = Drawing.Color.FromArgb(60, 60, 68);

        public override Drawing.Color ToolStripDropDownBackground => Background;
        public override Drawing.Color MenuBorder => Line;
        public override Drawing.Color MenuItemBorder => Hover;
        public override Drawing.Color MenuItemSelected => Hover;
        public override Drawing.Color ImageMarginGradientBegin => Background;
        public override Drawing.Color ImageMarginGradientMiddle => Background;
        public override Drawing.Color ImageMarginGradientEnd => Background;
        public override Drawing.Color SeparatorDark => Line;
        public override Drawing.Color SeparatorLight => Background;
        public override Drawing.Color CheckBackground => Background;
        public override Drawing.Color CheckSelectedBackground => Hover;
        public override Drawing.Color CheckPressedBackground => Hover;
    }
}
