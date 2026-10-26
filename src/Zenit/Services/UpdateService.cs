using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Zenit.Services;

/// <summary>Versión nueva: el .zip (portable) y, si la publicación lo trae, el instalador de la misma arquitectura.</summary>
public sealed record UpdateInfo(
    Version Version, string Tag, string ZipName, string ZipUrl, string SumsUrl, string PageUrl,
    string? SetupName = null, string? SetupUrl = null);

/// <summary>
/// Actualizaciones desde GitHub Releases. Antes de instalar verifica que el SHA-256 del archivo
/// coincida con el SHA256SUMS.txt de la misma versión: eso detecta descargas corruptas o incompletas,
/// pero no prueba la autoría (ambos vienen de la misma publicación); la autenticidad depende de GitHub.
/// </summary>
public static class UpdateService
{
    private const string LatestRelease = "https://api.github.com/repos/Dysast3r/zenit/releases/latest";

    private static string RuntimeId => RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";

    /// <summary>Arquitectura en el nombre del instalador: Zenit-Setup-1.1.0-x64.exe.</summary>
    private static string SetupArch => RuntimeId["win-".Length..];

    /// <summary>
    /// Zenit se instaló con el instalador (no es la versión portable): junto al .exe está su desinstalador.
    /// Así se actualiza con el instalador nuevo y Windows muestra la versión correcta en Aplicaciones instaladas.
    /// </summary>
    private static bool IsInstalled => File.Exists(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "unins000.exe"));

    public static Version CurrentVersion => Version.TryParse(AppInfo.Version.Split('-')[0], out var v) ? Normalize(v) : new Version(0, 0, 0);

    /// <summary>Marca que deja el script cuando no pudo copiar los archivos nuevos.</summary>
    private static string FailureMarker => Path.Combine(AppSettings.Folder, "update-failed.txt");

    /// <summary>Solo mayor.menor.revisión: así "1.0.0.0" no cuenta como más nueva que "1.0.0".</summary>
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    /// <summary>¿Falló la última instalación? Devuelve true una sola vez y borra la marca.</summary>
    public static bool ConsumeFailedInstall()
    {
        try
        {
            if (!File.Exists(FailureMarker)) return false;
            File.Delete(FailureMarker);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Devuelve la nueva versión disponible, o null si ya está al día o no hay conexión.</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var json = await Net.GetJsonAsync(LatestRelease);
        if (json is null) return null;
        var root = json.RootElement;

        var tag = root.Text("tag_name") ?? "";
        if (!Version.TryParse(tag.TrimStart('v').Split('-')[0], out var parsed)) return null;
        var version = Normalize(parsed);
        if (version <= CurrentVersion) return null;
        if (!root.TryGetProperty("assets", out var assets)) return null;

        string? zipName = null, zipUrl = null, sumsUrl = null, setupName = null, setupUrl = null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.Text("name") ?? "";
            var url = asset.Text("browser_download_url") ?? "";
            if (name.EndsWith($"-{RuntimeId}.zip", StringComparison.OrdinalIgnoreCase)) (zipName, zipUrl) = (name, url);
            else if (name.StartsWith("Zenit-Setup-", StringComparison.OrdinalIgnoreCase) && name.EndsWith($"-{SetupArch}.exe", StringComparison.OrdinalIgnoreCase))
                (setupName, setupUrl) = (name, url);
            else if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)) sumsUrl = url;
        }
        if (zipName is null || zipUrl is null || sumsUrl is null) return null;
        return new UpdateInfo(version, tag, zipName, zipUrl, sumsUrl, root.Text("html_url") ?? AppInfo.RepositoryUrl, setupName, setupUrl);
    }

    /// <summary>Descarga, verifica y aplica la actualización. Si todo va bien, la app se cierra y se reabre actualizada.</summary>
    public static async Task InstallAsync(UpdateInfo update, IProgress<double>? progress = null)
    {
        if (IsInstalled && update.SetupName is not null && update.SetupUrl is not null)
        {
            await InstallWithSetupAsync(update, update.SetupName, update.SetupUrl, progress);
            return;
        }

        var appDir = Path.GetDirectoryName(Environment.ProcessPath)!;
        // Sin permiso de escritura (p. ej. en Archivos de programa) el script fallaría después de cerrar la app.
        try
        {
            var probe = Path.Combine(appDir, $".zenit-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            throw new InvalidDataException(Strings.T("No hay permiso para escribir en la carpeta de Zenit. Actualiza manualmente.",
                "Zenit's folder is not writable. Please update manually."), ex);
        }

        var work = WorkFolder(update);
        var zip = await DownloadVerifiedAsync(update, update.ZipName, update.ZipUrl, work, progress);

        var extracted = Path.Combine(work, "files");
        ZipFile.ExtractToDirectory(zip, extracted);
        if (!File.Exists(Path.Combine(extracted, "Zenit.exe"))) throw new InvalidDataException("Zenit.exe");

        // En un .cmd, "%" se interpreta como variable: se duplica para que la ruta llegue literal.
        static string Cmd(string path) => path.Replace("%", "%%");
        var script = Path.Combine(work, "update.cmd");
        File.WriteAllText(script, $"""
            @echo off
            chcp 65001 >nul
            :wait
            tasklist /FI "PID eq {Environment.ProcessId}" | find "{Environment.ProcessId}" >nul && (timeout /t 1 /nobreak >nul & goto wait)
            robocopy "{Cmd(extracted)}" "{Cmd(appDir)}" /E /NFL /NDL /NJH /NJS /NP >nul
            if errorlevel 8 (
                if not exist "{Cmd(AppSettings.Folder)}" mkdir "{Cmd(AppSettings.Folder)}"
                echo robocopy %ERRORLEVEL% > "{Cmd(FailureMarker)}"
            )
            start "" "{Cmd(Path.Combine(appDir, "Zenit.exe"))}"
            """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // Con /s, cmd quita solo la primera y la última comilla: la ruta queda entre comillas aunque tenga & ( ) ^ @.
        Process.Start(new ProcessStartInfo("cmd.exe", $"/d /s /c \"\"{script}\"\"") { CreateNoWindow = true, UseShellExecute = false });
        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>
    /// Ejecuta el instalador nuevo en modo silencioso: cierra Zenit, reemplaza los archivos y la vuelve a abrir.
    /// Si el instalador no logra copiar, la app anterior queda intacta.
    /// </summary>
    private static async Task InstallWithSetupAsync(UpdateInfo update, string setupName, string setupUrl, IProgress<double>? progress)
    {
        var setup = await DownloadVerifiedAsync(update, setupName, setupUrl, WorkFolder(update), progress);
        Process.Start(new ProcessStartInfo(setup, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS") { UseShellExecute = false });
        System.Windows.Application.Current.Shutdown();
    }

    private static string WorkFolder(UpdateInfo update)
    {
        var work = Path.Combine(Path.GetTempPath(), "Zenit-update", update.Version.ToString());
        if (Directory.Exists(work)) Directory.Delete(work, recursive: true);
        Directory.CreateDirectory(work);
        return work;
    }

    /// <summary>Descarga un archivo de la publicación y comprueba que su SHA-256 coincida con SHA256SUMS.txt.</summary>
    private static async Task<string> DownloadVerifiedAsync(UpdateInfo update, string name, string url, string work, IProgress<double>? progress)
    {
        // El nombre viene de la API: sin rutas, solo el archivo.
        var path = Path.Combine(work, Path.GetFileName(name));
        await Net.DownloadAsync(url, path, progress);
        var sums = Encoding.UTF8.GetString(await Net.GetBytesAsync(update.SumsUrl));

        var expected = sums.Split('\n')
            .Select(line => line.Trim().Split((char[])[' ', '*'], StringSplitOptions.RemoveEmptyEntries))
            .FirstOrDefault(parts => parts.Length == 2 && parts[1].Equals(name, StringComparison.OrdinalIgnoreCase))?[0];
        if (expected is null) throw new InvalidDataException(Strings.T("La versión no publica la suma SHA-256 de este archivo.", "The release does not list a SHA-256 for this file."));

        await using (var stream = File.OpenRead(path))
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(Strings.T("La descarga no coincide con su SHA-256. Se canceló la actualización.", "The download does not match its SHA-256. Update cancelled."));
        }
        return path;
    }
}
