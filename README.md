<p align="center">
  <img src="docs/banner.png" alt="Zenit: la isla dinámica para Windows" width="100%">
</p>

<p align="center">
  <a href="https://github.com/Dysast3r/zenit/releases/latest"><img alt="Última versión" src="https://img.shields.io/github/v/release/Dysast3r/zenit?style=flat-square&color=0a84ff"></a>
  <a href="https://github.com/Dysast3r/zenit/releases"><img alt="Descargas" src="https://img.shields.io/github/downloads/Dysast3r/zenit/total?style=flat-square&color=22d3ee"></a>
  <a href="LICENSE"><img alt="Licencia MIT" src="https://img.shields.io/badge/licencia-MIT-34d399?style=flat-square"></a>
  <a href="https://github.com/Dysast3r/zenit/actions/workflows/build.yml"><img alt="Build" src="https://img.shields.io/github/actions/workflow/status/Dysast3r/zenit/build.yml?style=flat-square&label=build"></a>
  <a href="https://github.com/Dysast3r/zenit/actions/workflows/codeql.yml"><img alt="CodeQL" src="https://img.shields.io/github/actions/workflow/status/Dysast3r/zenit/codeql.yml?style=flat-square&label=CodeQL"></a>
  <a href="https://scorecard.dev/viewer/?uri=github.com/Dysast3r/zenit"><img alt="OpenSSF Scorecard" src="https://api.scorecard.dev/projects/github.com/Dysast3r/zenit/badge?style=flat-square"></a>
</p>

<p align="center">
  <a href="https://danidev.cl/zenit"><b>danidev.cl/zenit</b></a> · Desarrollado por <a href="https://danidev.cl"><b>Danidev.cl</b></a>
</p>

<p align="center">
  <b>Español</b> · <a href="#english">English</a>
</p>

---

**Zenit** es una isla dinámica para Windows 10 y 11, inspirada en la del iPhone. Muestra lo que suena en Spotify, TIDAL o YouTube Music con carátula, progreso y controles, y además el clima, tipos de cambio, relojes mundiales y la fecha. Todo con animaciones de resorte fluidas como las de iOS.

Es nativa (C# / .NET 10), liviana, de código abierto y **no pide cuentas, claves ni permisos de administrador**.

> **¿Por qué Zenit?** El cénit es el punto más alto del cielo, justo encima de ti. Ahí vive la isla: en lo más alto de tu pantalla.

<p align="center">
  <img src="docs/media/zenit-demo.gif" alt="Zenit en acción: la isla se expande, cambia de canción, muestra el clima, las monedas, el volumen y un temporizador" width="720">
</p>

## ✨ Funciones

| | |
|---|---|
| 🎵 **Música** | Spotify, TIDAL y YouTube Music (y opcionalmente cualquier reproductor): carátula, título, progreso arrastrable y controles, con la carátula desenfocada de fondo. El ecualizador reacciona al audio real. |
| 🎤 **Letras sincronizadas** | La línea que suena en el momento justo, desde [LRCLIB](https://lrclib.net). Si cierras la isla en la página de letras, la isla compacta sigue mostrando la línea actual. |
| ⏱️ **Temporizador y cronómetro** | Actividad en vivo: la cuenta regresiva queda visible en la isla compacta. |
| 📅 **Reuniones** | Avisa antes de cada reunión de Google Calendar u Outlook y muestra la cuenta regresiva en la isla. Basta con el enlace iCal privado: sin cuentas ni contraseñas. |
| 🎚️ **Volumen por aplicación** | Una página con el volumen del sistema y de cada app que suena (Spotify, Chrome, Discord…), con su propio deslizador y silencio. |
| 🔔 **Avisos del sistema** | Volumen (y la rueda del mouse sobre la isla para cambiarlo), carga de batería, puntos verde y naranja de cámara y micrófono, y notificaciones de Windows en la versión de la Microsoft Store. |
| 🧩 **Widgets de la comunidad** | Datos de cualquier API pública con un simple archivo JSON, sin código. [Crea el tuyo](widgets/README.md). |
| 🏝️ **Dos estilos** | Notch clásico pegado al borde con curvas cóncavas, o isla flotante separada del borde. |
| 👆 **Gestos** | Cursor o clic para expandir · deslizar para cambiar de canción · deslizar la isla expandida para cambiar de página · touchpad horizontal. |
| 🌦️ **Clima** | Ubicación actual o cualquier ciudad del mundo, pronóstico por horas e íconos vectoriales animados. |
| 💱 **Monedas** | Más de 300 monedas y criptomonedas con su variación diaria. |
| 🕐 **Reloj y fecha** | 12/24 h, segundos, seis formatos de fecha más uno personalizado, semana y día del año. |
| 🌍 **Relojes mundiales** | Hasta 4 ciudades con esferas analógicas de día y de noche. |
| 🙈 **Discreta** | No aparece en capturas, grabaciones ni al compartir pantalla. Se oculta en juegos y videos a pantalla completa, y respeta el modo No molestar de Windows. |
| 🖥️ **Varios monitores** | Elige en qué pantalla aparece la isla, o tenla en todas a la vez. |
| ⚙️ **Configurable** | Panel con el estilo de Windows 11: idioma, tamaño, posición, color, páginas, lo que muestra la isla en reposo (hora, fecha, clima, moneda o batería) y la compacta al centro (hora, fecha, temperatura o la canción), y más. |
| 🔄 **Actualizaciones verificadas** | Avisa cuando hay una versión nueva y comprueba su SHA-256 antes de instalarla. |
| 🌐 **Español, inglés y portugués** | Sigue el idioma de Windows o elige uno. ¿Hablas otro idioma? [Ayuda a traducir](CONTRIBUTING.md). |

## 📥 Instalación

1. Descarga **`Zenit-Setup-<versión>-x64.exe`** desde [**Releases**](https://github.com/Dysast3r/zenit/releases/latest) (`arm64` si tu PC es ARM, como un Surface Pro X).
2. Ábrelo con doble clic. Se instala en segundos, sin permisos de administrador ni .NET aparte.
3. La isla aparece arriba de la pantalla. Haz doble clic en el ícono de la bandeja para abrir la configuración.

Zenit se actualiza sola y se desinstala desde **Configuración de Windows → Aplicaciones**. ¿Prefieres no instalar nada? Descarga el `.zip` portable de la misma página y abre `Zenit.exe`.

> [!NOTE]
> Mientras el ejecutable no tenga firma de código, Windows SmartScreen puede mostrar "Windows protegió su PC". Pulsa **Más información → Ejecutar de todas formas**. Antes de hacerlo, puedes verificar que el archivo es auténtico (ver abajo).

## 🔐 Verificar la descarga

Cada versión se compila **públicamente en GitHub Actions** desde este código. Lleva una [atestación de procedencia](https://docs.github.com/actions/security-for-github-actions/using-artifact-attestations) firmada con Sigstore, que prueba que el archivo salió de este repositorio y nadie lo modificó:

```powershell
gh attestation verify .\Zenit-Setup-<versión>-x64.exe --repo Dysast3r/zenit
```

También puedes comparar el hash con `SHA256SUMS.txt`, que viene en cada Release:

```powershell
Get-FileHash .\Zenit-Setup-<versión>-x64.exe -Algorithm SHA256
```

## 🛡️ Privacidad y seguridad

- **Sin cuentas, claves ni telemetría.**
- La música se lee localmente con la API oficial de Windows (`GlobalSystemMediaTransportControls`), no con la API web de cada servicio. Solo si activas la página **Letras** se envían el título y el artista a LRCLIB.
- El ecualizador solo lee el **nivel** de volumen; nunca graba audio.
- Se ejecuta sin privilegios elevados y no instala servicios.
- Solo conecta por HTTPS, y solo si activas la página correspondiente:

  | Dato | Servicio |
  |---|---|
  | Clima y búsqueda de ciudades | [Open-Meteo](https://open-meteo.com) |
  | Nombre de tu ubicación | [BigDataCloud](https://www.bigdatacloud.com) o [ipapi.co](https://ipapi.co) |
  | Monedas | [currency-api](https://github.com/fawazahmed0/exchange-api) (jsDelivr / Cloudflare) |
  | Letras | [LRCLIB](https://lrclib.net) |
  | Actualizaciones | GitHub Releases |
  | Widgets | Solo las URL de los widgets que tú instales |
  | Reuniones | Solo los enlaces iCal de los calendarios que tú agregues |

- Análisis automático con **CodeQL** y **OpenSSF Scorecard**. Las dependencias de CI se actualizan con Dependabot y las acciones están fijadas por commit.
- ¿Encontraste un problema de seguridad? Revisa [SECURITY.md](SECURITY.md).

## 🛠️ Compilar desde el código

Requisitos: Windows 10 2004+ y el [SDK de .NET 10](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/Dysast3r/zenit
cd zenit
dotnet run --project src/Zenit
```

En Debug existen atajos para probar la interfaz sin música: `--demo idle|compact|peek|expanded|marquee|meeting|mixer|lyrics|compact-lyrics|clock|timer|live|volume|expanded-volume|weather|currency|world|widgets|tour` y `--settings`.

```
src/Zenit
├── Animation/   Resortes físicos y seguimiento de velocidad para gestos
├── Controls/    Forma del notch, ecualizador, íconos de clima, reloj analógico
├── Interop/     Win32: ventana superpuesta, exclusión de capturas, pantalla completa
├── Services/    Música, clima, ubicación, monedas, audio, ajustes e idiomas
└── Views/       Isla, páginas del carrusel, configuración y bandeja
```

Sin paquetes NuGet de terceros. ¿Quieres aportar? Lee [CONTRIBUTING.md](CONTRIBUTING.md), busca issues con la etiqueta [`good first issue`](https://github.com/Dysast3r/zenit/labels/good%20first%20issue) y revisa el [ROADMAP](ROADMAP.md). Las traducciones se hacen en [Crowdin](crowdin.yml) o con un pull request a `src/Zenit/Assets/i18n`.

## 📜 Licencia y avisos

[MIT](LICENSE) © 2026 Daniel Moraga · [Danidev.cl](https://danidev.cl). Puedes usar, modificar y redistribuir el código libremente, manteniendo el aviso de copyright.

Proyecto independiente, sin relación con Apple, Spotify, TIDAL ni Google. "Dynamic Island" y "iPhone" son marcas de Apple Inc.; "Spotify", de Spotify AB; "TIDAL", de TIDAL Music AS; "YouTube Music", de Google LLC.

---

<a id="english"></a>

## English

**Zenit** is a dynamic island for Windows 10 and 11, inspired by the one on the iPhone. It shows what's playing on Spotify, TIDAL or YouTube Music with album art, progress and controls, plus the weather, exchange rates, world clocks and the date, all with smooth, springy animations.

It's native (C# / .NET 10), lightweight, open source, and **needs no accounts, API keys or admin rights**.

> **Why "Zenit"?** The zenith is the highest point in the sky, right above you. That's where the island lives: at the very top of your screen.

### ✨ Features

| | |
|---|---|
| 🎵 **Music** | Spotify, TIDAL and YouTube Music (and optionally any other player): album art, title, a draggable progress bar and controls, with the blurred artwork as a backdrop. The equalizer reacts to the actual audio. |
| 🎤 **Synced lyrics** | The current line, right on time, from [LRCLIB](https://lrclib.net). Close the island on the lyrics page and the compact island keeps showing the current line. |
| ⏱️ **Timer and stopwatch** | A live activity: the countdown stays visible on the compact island. |
| 📅 **Meetings** | Get a heads-up before every Google Calendar or Outlook meeting, with a countdown on the island. All it needs is the calendar's private iCal link: no accounts or passwords. |
| 🎚️ **Per-app volume** | A page with the system volume and every app playing audio (Spotify, Chrome, Discord…), each with its own slider and mute. |
| 🔔 **System alerts** | Volume (scroll over the island to change it), battery charging, green and orange camera and microphone dots, and Windows notifications in the Microsoft Store version. |
| 🧩 **Community widgets** | Data from any public API with a plain JSON file, no code. [Make your own](widgets/README.md). |
| 🏝️ **Two looks** | A classic notch attached to the top edge with concave curves, or a floating island. |
| 👆 **Gestures** | Hover or click to expand · swipe to change songs · swipe the expanded island to switch pages · horizontal touchpad scrolling. |
| 🌦️ **Weather** | Your current location or any city in the world, an hourly forecast and animated vector icons. |
| 💱 **Currencies** | 300+ currencies and cryptocurrencies with their daily change. |
| 🕐 **Clock and date** | 12/24-hour time, seconds, six date formats plus a custom one, week and day of the year. |
| 🌍 **World clocks** | Up to 4 cities with analog faces for day and night. |
| 🙈 **Discreet** | Hidden from screenshots, recordings and screen sharing. It steps aside for full-screen games and videos, and respects Windows Do not disturb. |
| 🖥️ **Multiple displays** | Choose which screen shows the island, or show it on all of them. |
| ⚙️ **Customizable** | A Windows 11-style settings panel: language, size, position, color, pages, what the idle island shows (time, date, weather, currency or battery) and what the compact island shows in the middle (time, date, temperature or the scrolling song title), and more. |
| 🔄 **Verified updates** | Lets you know when a new version is out and checks its SHA-256 before installing it. |
| 🌐 **English, Spanish and Portuguese** | Follows your Windows language, or pick one yourself. Speak another language? [Help translate](CONTRIBUTING.md). |

### 📥 Installation

1. Download **`Zenit-Setup-<version>-x64.exe`** from [**Releases**](https://github.com/Dysast3r/zenit/releases/latest) (`arm64` if your PC runs on ARM, like a Surface Pro X).
2. Double-click it. It installs in seconds, with no admin rights and no separate .NET install.
3. The island appears at the top of your screen. Double-click the tray icon to open Settings.

Zenit keeps itself up to date and can be uninstalled from **Windows Settings → Apps**. Rather not install anything? Grab the portable `.zip` from the same page and run `Zenit.exe`.

> [!NOTE]
> Until the installer is code-signed, Windows SmartScreen may show "Windows protected your PC". Click **More info → Run anyway**. Before you do, you can check that the file is genuine (see below).

### 🔐 Verify your download

Every release is **built in the open on GitHub Actions** from this code. Each file carries a [build provenance attestation](https://docs.github.com/actions/security-for-github-actions/using-artifact-attestations) signed with Sigstore, which proves it came from this repository and hasn't been tampered with:

```powershell
gh attestation verify .\Zenit-Setup-<version>-x64.exe --repo Dysast3r/zenit
```

You can also compare its hash with the `SHA256SUMS.txt` included in every release:

```powershell
Get-FileHash .\Zenit-Setup-<version>-x64.exe -Algorithm SHA256
```

### 🛡️ Privacy and security

- **No accounts, no API keys, no telemetry.**
- Music info is read locally through the official Windows API (`GlobalSystemMediaTransportControls`), not each service's web API. Only if you turn on the **Lyrics** page are the song title and artist sent to LRCLIB.
- The equalizer only reads the volume **level**; it never records audio.
- It runs without elevated privileges and installs no services.
- It only connects over HTTPS, and only when you enable the related feature:

  | Data | Service |
  |---|---|
  | Weather and city search | [Open-Meteo](https://open-meteo.com) |
  | Your location's name | [BigDataCloud](https://www.bigdatacloud.com) or [ipapi.co](https://ipapi.co) |
  | Currencies | [currency-api](https://github.com/fawazahmed0/exchange-api) (jsDelivr / Cloudflare) |
  | Lyrics | [LRCLIB](https://lrclib.net) |
  | Updates | GitHub Releases |
  | Widgets | Only the URLs of the widgets you install |
  | Meetings | Only the iCal links of the calendars you add |

- Automated analysis with **CodeQL** and **OpenSSF Scorecard**. CI dependencies are kept current by Dependabot, and every action is pinned to a commit.
- Found a security issue? See [SECURITY.md](SECURITY.md).

### 🛠️ Build from source

Requirements: Windows 10 2004 or later and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/Dysast3r/zenit
cd zenit
dotnet run --project src/Zenit
```

Debug builds include shortcuts to try the UI without music: `--demo idle|compact|peek|expanded|marquee|meeting|mixer|lyrics|compact-lyrics|clock|timer|live|volume|expanded-volume|weather|currency|world|widgets|tour` and `--settings`.

No third-party NuGet packages. Want to contribute? Read [CONTRIBUTING.md](CONTRIBUTING.md), look for issues labeled [`good first issue`](https://github.com/Dysast3r/zenit/labels/good%20first%20issue) and check the [roadmap](ROADMAP.md). Translations happen on [Crowdin](crowdin.yml) or through a pull request to `src/Zenit/Assets/i18n`.

### 📜 License and notices

[MIT](LICENSE) © 2026 Daniel Moraga · [Danidev.cl](https://danidev.cl). You're free to use, modify and redistribute the code as long as you keep the copyright notice.

Independent project, not affiliated with Apple, Spotify, TIDAL or Google. "Dynamic Island" and "iPhone" are trademarks of Apple Inc.; "Spotify" of Spotify AB; "TIDAL" of TIDAL Music AS; "YouTube Music" of Google LLC.

---

<p align="center">
  Hecho con 💜 en Chile por <a href="https://danidev.cl"><b>Danidev.cl</b></a> · Made with 💜 in Chile by <a href="https://danidev.cl"><b>Danidev.cl</b></a>
</p>
