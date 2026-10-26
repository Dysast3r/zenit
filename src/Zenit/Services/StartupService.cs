using Microsoft.Win32;

namespace Zenit.Services;

/// <summary>Inicio con Windows mediante HKCU\...\Run (por usuario, sin permisos de administrador).</summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Zenit";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled) key.SetValue(ValueName, Command);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Nombre del valor que usaban las versiones previas a 1.0 ("Isla Dinámica").</summary>
    private const string LegacyValueName = "IslaDinamica";

    /// <summary>Si el usuario movió el .exe, actualiza la ruta registrada (y migra el nombre anterior).</summary>
    public static void RefreshPathIfEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(LegacyValueName) is string)
            {
                key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
                key.SetValue(ValueName, Command);
            }
            if (key?.GetValue(ValueName) is string current && current != Command)
                key.SetValue(ValueName, Command);
        }
        catch
        {
            // Sin acceso al registro: se ignora.
        }
    }
}
