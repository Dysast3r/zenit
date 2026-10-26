# Crea en GitHub los primeros issues para nuevos contribuidores (requiere `gh auth login`).
# Uso, una vez publicado el repositorio: powershell -ExecutionPolicy Bypass -File tools/create-good-first-issues.ps1
$repo = 'Dysast3r/zenit'

$issues = @(
    @{ Title = 'Traducir Zenit al francés'; Labels = 'good first issue,translation'; Body = @'
Agrega `src/Zenit/Assets/i18n/fr.json` copiando `en.json` y traduciendo los valores (las claves quedan en inglés).
Luego suma `"fr"` al arreglo `Catalogs` y `French` al enum `AppLanguage` en `Services/Strings.cs`, y la opción en el selector de idioma de `SettingsWindow`.
'@ },
    @{ Title = 'Traducir Zenit al alemán'; Labels = 'good first issue,translation'; Body = 'Igual que la traducción al francés, con `de.json`. Ver `CONTRIBUTING.md` y `crowdin.yml`.' },
    @{ Title = 'Widget: indicadores económicos de Chile (mindicador.cl)'; Labels = 'good first issue,widget'; Body = @'
Crea `widgets/chile-indicadores.json` con UF, dólar y UTM desde `https://mindicador.cl/api`.
Guía: `widgets/README.md`. Prueba copiándolo a `%AppData%\Zenit\widgets` y pulsando **Recargar**.
'@ },
    @{ Title = 'Widget: próximo lanzamiento de SpaceX'; Labels = 'good first issue,widget'; Body = 'Usa una API pública y abierta de lanzamientos espaciales (HTTPS, sin clave), como Launch Library 2 de The Space Devs (`https://ll.thespacedevs.com/2.3.0/launches/upcoming/?limit=1&search=SpaceX`). Muestra el nombre de la misión y la fecha. Guía: `widgets/README.md`.' },
    @{ Title = 'Ícono de clima: granizo'; Labels = 'good first issue,design'; Body = 'Los códigos WMO 96 y 99 (tormenta con granizo) usan hoy el ícono de tormenta. Agrega `WeatherKind.Hail` y dibújalo en `Controls/WeatherGlyph.cs` con pequeñas esferas cayendo.' },
    @{ Title = 'Atajo de teclado global para abrir la isla'; Labels = 'good first issue,enhancement'; Body = 'Registra un atajo (por ejemplo Ctrl+Alt+Z) con `RegisterHotKey` en `Interop/NativeMethods.cs` y llama a `IslandWindow.Toggle()`. Que sea configurable en Configuración → General.' },
    @{ Title = 'Mostrar el porcentaje de batería en reposo'; Labels = 'good first issue,enhancement'; Body = 'Agrega `IdleContent.Battery` para que la isla en reposo muestre el nivel de batería en laptops (`Services/SystemIndicators.cs` ya lo lee con `SystemInformation.PowerStatus`). Agrégalo al selector de Configuración y al `switch` de `IdleContent` en `Views/IslandWindow.xaml.cs`.' },
    @{ Title = 'Capturas de pantalla para el README en inglés'; Labels = 'good first issue,documentation'; Body = 'Toma capturas de cada página de la isla con Windows en inglés y agrégalas a la sección English del README.' }
)

foreach ($issue in $issues) {
    gh issue create --repo $repo --title $issue.Title --body $issue.Body --label $issue.Labels
}
