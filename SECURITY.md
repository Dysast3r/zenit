# Política de seguridad

## Versiones con soporte

Solo la última versión publicada en [Releases](https://github.com/Dysast3r/zenit/releases) recibe correcciones de seguridad.

## Reportar una vulnerabilidad

**No abras un issue público.** Usa el reporte privado de GitHub:

1. Ve a la pestaña **Security** del repositorio.
2. Pulsa **Report a vulnerability**.
3. Describe el problema, cómo reproducirlo y su impacto.

Respondo en un plazo de 7 días. Una vez corregido el problema, se publica un aviso de seguridad y se reconoce a quien lo reportó, si así lo desea.

## Qué hace la app y qué no

- No pide permisos de administrador (`asInvoker`) ni instala servicios ni controladores.
- No guarda contraseñas, tokens ni datos personales. Sus ajustes viven en `%AppData%\Zenit\settings.json`.
- La información de música se lee localmente con la API de Windows `GlobalSystemMediaTransportControls`. Solo si activas la página **Letras**, el título, el artista y la duración de la canción se envían a LRCLIB para buscar la letra.
- El ecualizador solo lee el nivel de volumen del sistema; nunca graba audio. Los indicadores de cámara y micrófono leen el registro de privacidad de Windows y no acceden a los dispositivos.
- Todas las conexiones son HTTPS y solo ocurren si la función correspondiente está activa:

  | Función | Servidores |
  |---|---|
  | Clima y búsqueda de ciudades | `api.open-meteo.com`, `geocoding-api.open-meteo.com` |
  | Nombre de tu ubicación | `api.bigdatacloud.net` o `ipapi.co` |
  | Monedas | `cdn.jsdelivr.net`, `currency-api.pages.dev` |
  | Letras | `lrclib.net` |
  | Actualizaciones | `api.github.com`, `github.com` |
  | Widgets | Solo las URL de los widgets que tú instales |

- Los widgets no ejecutan código: son archivos JSON que declaran una URL HTTPS y qué campos mostrar.
- Las actualizaciones se instalan solo si el SHA-256 del archivo coincide con `SHA256SUMS.txt` de la misma versión.

## Verificar que una descarga es auténtica

Cada versión se compila en GitHub Actions desde este repositorio y lleva una atestación de procedencia firmada con Sigstore:

```powershell
gh attestation verify .\Zenit-1.0.0-win-x64.zip --repo Dysast3r/zenit
```

También puedes comparar el hash con `SHA256SUMS.txt`:

```powershell
Get-FileHash .\Zenit-1.0.0-win-x64.zip -Algorithm SHA256
```
