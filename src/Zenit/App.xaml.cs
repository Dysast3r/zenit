using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Zenit.Services;
using Microsoft.Win32;
using Zenit.Views;
using Forms = System.Windows.Forms;

namespace Zenit;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    private ZenitServices? _services;
    private TrayIcon? _tray;
    private AppSettings? _settings;
    private SettingsWindow? _settingsWindow;

    /// <summary>Islas de los otros monitores cuando la isla va en todas las pantallas, por nombre de dispositivo.</summary>
    private readonly Dictionary<string, IslandWindow> _extraIslands = [];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        _mutex = new Mutex(true, @"Local\Zenit.SingleInstance", out _ownsMutex);
        if (!_ownsMutex && e.Args.Contains(RestartFlag))
        {
            // Reinicio por cambio de idioma: la instancia anterior está cerrándose.
            try
            {
                _ownsMutex = _mutex.WaitOne(TimeSpan.FromSeconds(5));
            }
            catch (AbandonedMutexException)
            {
                _ownsMutex = true;
            }
        }
        if (!_ownsMutex)
        {
            Shutdown();
            return;
        }

        try
        {
            _settings = AppSettings.Load();
            Strings.Initialize(_settings.Language);
            StartupService.RefreshPathIfEnabled();
            if (UpdateService.ConsumeFailedInstall())
                MessageBox.Show(Strings.T("No se pudo instalar la actualización: se mantiene la versión anterior.",
                    "The update could not be installed: the previous version is still in use."),
                    Strings.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);

            _services = new ZenitServices(_settings, Dispatcher);
            var window = new IslandWindow(_services);
            _tray = new TrayIcon(window, OpenSettings, Shutdown);
            window.MenuRequested += _tray.ShowMenuAtCursor;
            window.Show();
            _settings.Changed += SyncIslands;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SyncIslands();
#if DEBUG
            var demo = Array.IndexOf(e.Args, "--demo");
            if (demo >= 0 && demo + 1 < e.Args.Length)
            {
                window.ShowDemo(e.Args[demo + 1]);
                return;
            }
#endif
            if (e.Args.Contains("--settings")) OpenSettings();
        }
        catch (Exception ex)
        {
            // Sin isla ni bandeja, el proceso quedaría invisible reteniendo el mutex: se cierra.
            Log(ex);
            Shutdown();
            return;
        }

        try
        {
            await _services.Media.StartAsync();
        }
        catch (Exception ex)
        {
            Log(ex);
        }

        await CheckForUpdatesAsync(automatic: true);
    }

    /// <summary>Versión nueva encontrada en GitHub, si la hay.</summary>
    public UpdateInfo? AvailableUpdate { get; private set; }

    public event Action? UpdateFound;

    /// <summary>Busca actualizaciones; en modo automático, como mucho una vez cada 12 horas.</summary>
    public async Task CheckForUpdatesAsync(bool automatic)
    {
        if (_settings is null) return;
        if (automatic && (!_settings.AutoCheckUpdates || DateTime.Now - _settings.LastUpdateCheck < TimeSpan.FromHours(12))) return;
        try
        {
            if (automatic) await Task.Delay(TimeSpan.FromSeconds(20));
            AvailableUpdate = await UpdateService.CheckAsync();
            _settings.LastUpdateCheck = DateTime.Now;
            _settings.Save();
            if (AvailableUpdate is not null)
            {
                _tray?.ShowUpdate(AvailableUpdate, OpenSettings);
                UpdateFound?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Log(ex);
        }
    }

    private const string RestartFlag = "--restarted";

    /// <summary>Reinicia la app (p. ej. tras cambiar el idioma) y vuelve a abrir la configuración.</summary>
    public void Restart()
    {
        _settings?.Save();
        _tray?.Dispose();
        _tray = null;
        if (_ownsMutex)
        {
            _mutex?.ReleaseMutex();
            _ownsMutex = false;
        }
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { ArgumentList = { "--settings", RestartFlag }, UseShellExecute = false });
        Shutdown();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(SyncIslands);

    /// <summary>Con "Todas las pantallas", una isla más por cada monitor secundario; si no, solo la principal.</summary>
    private void SyncIslands()
    {
        if (_services is null || _settings is null || _tray is null) return;
        var wanted = _settings.Monitor == "*"
            ? Forms.Screen.AllScreens.Where(s => !s.Primary).Select(s => s.DeviceName).ToHashSet()
            : [];

        foreach (var (name, island) in _extraIslands.Where(p => !wanted.Contains(p.Key)).ToList())
        {
            _extraIslands.Remove(name);
            island.Close();
        }
        foreach (var name in wanted.Where(n => !_extraIslands.ContainsKey(n)))
        {
            var island = new IslandWindow(_services, primary: false, screen: name);
            island.MenuRequested += _tray.ShowMenuAtCursor;
            _extraIslands[name] = island;
            island.Show();
        }
    }

    private void OpenSettings()
    {
        if (_services is null) return;
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(_services!);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _tray?.Dispose();
        _services?.Dispose();
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // La isla debe seguir viva aunque falle algo puntual (p. ej. una sesión multimedia que desaparece).
        Log(e.Exception);
        e.Handled = true;
        // Si falla antes de terminar el arranque no hay bandeja para salir: se cierra en vez de quedar zombi.
        if (_tray is null) Shutdown();
    }

    internal static void Log(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(AppSettings.Folder);
            File.AppendAllText(Path.Combine(AppSettings.Folder, "error.log"), $"[{DateTime.Now:O}] {ex}\n\n");
        }
        catch
        {
            // Sin registro posible; no hay nada más que hacer.
        }
    }
}
