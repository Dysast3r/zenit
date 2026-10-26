using System.Runtime.InteropServices;

namespace Zenit.Interop;

internal static partial class NativeMethods
{
    public const int WM_MOUSEHWHEEL = 0x020E;

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_APPWINDOW = 0x00040000;
    private const long WS_EX_NOACTIVATE = 0x08000000;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private static readonly nint HWND_TOPMOST = -1;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hWnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hWnd, int index, nint value);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hWnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("shell32.dll")]
    private static partial int SHQueryUserNotificationState(out int state);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowDisplayAffinity(nint hWnd, uint affinity);

    private const uint WDA_NONE = 0x0;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

    /// <summary>
    /// Excluye la ventana de capturas de pantalla, grabaciones y screen sharing (Windows 10 2004+).
    /// Sigue viéndose en el monitor con normalidad.
    /// </summary>
    public static void ExcludeFromCapture(nint hwnd, bool exclude)
        => SetWindowDisplayAffinity(hwnd, exclude ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);

    /// <summary>Fuera de Alt+Tab y de la barra de tareas, y sin robar el foco al hacer clic.</summary>
    public static void MakeOverlay(nint hwnd)
    {
        var style = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        style = (style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (nint)style);
    }

    public static void KeepTopmost(nint hwnd)
        => SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

    /// <summary>Juego, video o presentación a pantalla completa (QUNS_BUSY, D3D o modo presentación).</summary>
    public static bool IsFullscreenAppRunning()
        => SHQueryUserNotificationState(out var state) == 0 && state is 2 or 3 or 4;

    // ───────────────────────── Procesos ─────────────────────────

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [LibraryImport("kernel32.dll")]
    private static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageName(nint process, uint flags, [Out] char[] name, ref uint size);

    /// <summary>
    /// Ruta del ejecutable de un proceso. Usa el acceso mínimo, que Windows concede incluso a procesos
    /// aislados (el servicio de audio de Chrome o Edge), donde Process.MainModule falla.
    /// </summary>
    public static string? ProcessPath(uint processId)
    {
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (handle == 0) return null;
        try
        {
            var buffer = new char[1024];
            var size = (uint)buffer.Length;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    // ───────────────────────── Monitores ─────────────────────────

    private const uint SWP_NOZORDER = 0x0004;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(POINT point, uint flags);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    /// <summary>Escala (1.0 = 100 %) del monitor que contiene el punto, en píxeles físicos.</summary>
    public static double DpiScaleAt(int x, int y)
    {
        var monitor = MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST);
        return GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpi, out _) == 0 && dpi > 0 ? dpi / 96.0 : 1.0;
    }

    /// <summary>Mueve la ventana a una posición en píxeles físicos, sin cambiar su tamaño ni su orden.</summary>
    public static void MoveWindow(nint hwnd, int x, int y)
        => SetWindowPos(hwnd, 0, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

    // ───────────────────────── No molestar ─────────────────────────

    /// <summary>
    /// Estado de "No molestar" / Asistente de concentración que publica el shell de Windows
    /// (WNF_SHEL_QUIETHOURS_ACTIVE_PROFILE_CHANGED): 0 = apagado, 1 = solo prioridad, 2 = solo alarmas.
    /// No está documentado, pero es lo que usan las apps de terceros; si falla se toma como apagado.
    /// </summary>
    private const ulong WNF_SHEL_QUIETHOURS_ACTIVE_PROFILE_CHANGED = 0x0D83063EA3BF1C75;

    [LibraryImport("ntdll.dll")]
    private static partial int NtQueryWnfStateData(ref ulong stateName, nint typeId, nint explicitScope,
        out uint changeStamp, out int buffer, ref int bufferSize);

    /// <summary>"No molestar" (Windows 11), una sesión de concentración o el modo presentación están activos.</summary>
    public static bool IsDoNotDisturbOn()
    {
        try
        {
            var name = WNF_SHEL_QUIETHOURS_ACTIVE_PROFILE_CHANGED;
            var size = sizeof(int);
            if (NtQueryWnfStateData(ref name, 0, 0, out _, out var profile, ref size) == 0 && size >= sizeof(int) && profile > 0)
                return true;
        }
        catch
        {
            // Sin ntdll o API no disponible: se sigue con el resto de señales.
        }

        try
        {
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.Shell.FocusSessionManager")
                && Windows.UI.Shell.FocusSessionManager.IsSupported
                && Windows.UI.Shell.FocusSessionManager.GetDefault().IsFocusActive)
                return true;
        }
        catch
        {
            // Sesiones de concentración no disponibles en esta versión de Windows.
        }

        return SHQueryUserNotificationState(out var state) == 0 && state == 4;
    }
}
