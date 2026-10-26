using System.IO;
using System.Reflection;
using System.Windows;

namespace Zenit.Services;

/// <summary>Datos públicos del proyecto (versión, repositorio y licencia).</summary>
public static class AppInfo
{
    public const string RepositoryUrl = "https://github.com/Dysast3r/zenit";
    public const string LicenseName = "MIT";
    public const string DeveloperName = "Danidev.cl";
    public const string DeveloperUrl = "https://danidev.cl";

    public static string Version =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "1.0.0";

    /// <summary>Texto completo de la licencia, incluido dentro del ejecutable.</summary>
    public static string LicenseText()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/LICENSE.txt"));
        if (resource is null) return "MIT License";
        using var reader = new StreamReader(resource.Stream);
        return reader.ReadToEnd();
    }
}
