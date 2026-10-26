# Distribución: instalador y Microsoft Store

## Instalador (GitHub Releases)

Cada versión se publica en [Releases](https://github.com/Dysast3r/zenit/releases) con:

| Archivo | Para qué |
|---|---|
| `Zenit-Setup-<versión>-x64.exe` / `-arm64.exe` | **Descarga principal.** Instalador por usuario: sin permisos de administrador, acceso en el menú Inicio, opción de iniciar con Windows y desinstalación desde Configuración → Aplicaciones. |
| `Zenit-<versión>-win-x64.zip` / `-win-arm64.zip` | Versión portable, sin instalar nada. También la usa el actualizador de las copias portables. |
| `SHA256SUMS.txt` | Sumas SHA-256 de todos los archivos. |

### Publicar una versión

1. Sube la versión en `src/Zenit/Zenit.csproj` (`<Version>`) y mueve lo de "Sin publicar" del `CHANGELOG.md` a la versión nueva.
2. Crea y sube la etiqueta:
   ```powershell
   git tag v1.1.0
   git push origin v1.1.0
   ```
3. El workflow `build.yml` compila x64 y ARM64, arma los `.zip` y los instaladores con [Inno Setup](https://jrsoftware.org/isinfo.php) (`packaging/installer/Zenit.iss`), calcula las sumas, firma la procedencia y crea el Release.

### Cómo se actualiza

- **Instalada con el instalador:** la app descarga el instalador nuevo, verifica su SHA-256 y lo ejecuta en silencio; Windows muestra la versión correcta en Aplicaciones.
- **Portable:** descarga el `.zip`, verifica su SHA-256 y reemplaza los archivos en su carpeta.

## Microsoft Store

Publicar en la Store da tres ventajas: instalación con un clic, actualizaciones automáticas y **la firma de Microsoft**, que elimina el aviso de SmartScreen. Además habilita las **notificaciones de Windows dentro de la isla**, porque la app pasa a tener identidad de paquete.

1. Crea una cuenta de desarrollador individual en [Partner Center](https://partner.microsoft.com/dashboard/registration). Para personas naturales hoy es gratuita.
2. Reserva el nombre **Zenit**. No uses marcas de Apple en el nombre ni en las palabras clave (iPhone, Dynamic Island): la Store rechaza las fichas que pueden confundir con otro producto.
3. Empaqueta como MSIX: en Visual Studio agrega un *Windows Application Packaging Project* que referencie `src/Zenit`, y declara la capacidad `userNotificationListener` en el manifiesto.
4. Sube el `.msixupload`, completa la ficha con capturas, `docs/banner.png` y el video promocional (se genera en local con `marketing/motion`, no está en el repo), y envía a certificación.

La Store firma el paquete, así que para ese canal no necesitas un certificado propio.
