using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Zenit.Services;

/// <summary>
/// Estado del sistema que el iPhone muestra en la isla: carga de batería y cámara o micrófono en uso
/// (los puntos verde y naranja). Se lee del registro de privacidad de Windows; no accede a los dispositivos.
/// </summary>
public sealed class SystemIndicators : IDisposable
{
    private const string ConsentStore = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    private readonly DispatcherTimer _poll;
    private bool? _pluggedIn;
    private bool _lowWarned;

    public bool CameraInUse { get; private set; }
    public bool MicrophoneInUse { get; private set; }

    /// <summary>Nivel de batería (0..1), o null en equipos sin batería.</summary>
    public float? BatteryLevel { get; private set; }

    /// <summary>Cambió el uso de cámara o micrófono.</summary>
    public event Action? PrivacyChanged;

    /// <summary>Se conectó el cargador (porcentaje 0..1).</summary>
    public event Action<float>? ChargingStarted;

    /// <summary>La batería bajó del 20 % sin cargador.</summary>
    public event Action<float>? BatteryLow;

    public SystemIndicators(Dispatcher dispatcher)
    {
        _poll = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => Poll(), dispatcher);
        _poll.Start();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        CheckPower(initial: true);
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.StatusChange) _poll.Dispatcher.BeginInvoke(() => CheckPower(initial: false));
    }

    private void CheckPower(bool initial)
    {
        var status = Forms.SystemInformation.PowerStatus;
        if (status.BatteryChargeStatus.HasFlag(Forms.BatteryChargeStatus.NoSystemBattery))
        {
            BatteryLevel = null;
            return;
        }

        var plugged = status.PowerLineStatus == Forms.PowerLineStatus.Online;
        var percent = Math.Clamp(status.BatteryLifePercent, 0f, 1f);
        BatteryLevel = percent;
        if (!initial && plugged && _pluggedIn == false) ChargingStarted?.Invoke(percent);
        _pluggedIn = plugged;

        if (plugged || percent > 0.25f) _lowWarned = false;
        else if (!_lowWarned && percent <= 0.2f)
        {
            _lowWarned = true;
            BatteryLow?.Invoke(percent);
        }
    }

    private void Poll()
    {
        CheckPower(initial: false);
        var camera = InUse("webcam");
        var microphone = InUse("microphone");
        if (camera == CameraInUse && microphone == MicrophoneInUse) return;
        CameraInUse = camera;
        MicrophoneInUse = microphone;
        PrivacyChanged?.Invoke();
    }

    /// <summary>Una app está usando el dispositivo si tiene un inicio registrado y aún no un fin.</summary>
    private static bool InUse(string capability)
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey($@"{ConsentStore}\{capability}");
            if (root is null) return false;
            foreach (var name in root.GetSubKeyNames())
            {
                using var app = root.OpenSubKey(name);
                if (app is null) continue;
                if (name == "NonPackaged")
                {
                    foreach (var sub in app.GetSubKeyNames())
                    {
                        using var exe = app.OpenSubKey(sub);
                        if (IsActive(exe)) return true;
                    }
                }
                else if (IsActive(app)) return true;
            }
        }
        catch
        {
            // Sin acceso al registro: se asume que no hay uso.
        }
        return false;
    }

    private static bool IsActive(RegistryKey? key)
        => key?.GetValue("LastUsedTimeStop") is long stop && stop == 0
           && key.GetValue("LastUsedTimeStart") is long start && start > 0;

    public void Dispose()
    {
        _poll.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
