# Registro de cambios

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/). El proyecto usa [versionado semántico](https://semver.org/lang/es/).

## [1.1.6] - 2026-10-26

### Añadido
- Widget del próximo lanzamiento de SpaceX: cuenta regresiva, misión y fecha desde Launch Library 2, sin clave ([#4](https://github.com/Dysast3r/zenit/issues/4)).
- Los widgets formatean fechas: `"format": "relative"` muestra una cuenta regresiva que avanza cada minuto, y cualquier formato de fecha de .NET la muestra en tu hora local.

### Cambiado
- En los widgets, un campo sin etiqueta usa todo el ancho de la tarjeta.

## [1.1.5] - 2026-10-26

### Añadido
- Compatibilidad con TIDAL y YouTube Music (la app instalada y las de escritorio de la comunidad): tienen la misma prioridad que Spotify, y un clic en la carátula abre la app que está sonando.

### Cambiado
- La isla muestra primero lo que está sonando: Spotify en pausa ya no tapa a otra app de música que esté reproduciendo.
- El ajuste "Solo Spotify" pasa a ser "Solo apps de música" (Spotify, TIDAL y YouTube Music) y conserva lo que tenías elegido.

### Corregido
- La barra de volumen al girar la rueda del mouse sobre la isla vuelve a verse: girar la rueda cancela la expansión por el cursor, y con la isla ya expandida la barra reemplaza un momento a los puntos de página.

## [1.1.4] - 2026-10-26

### Añadido
- Si la última página abierta fue Letras, la isla compacta sigue mostrando la línea que se canta, con la carátula y el ecualizador a los lados.

## [1.1.3] - 2026-10-26

### Corregido
- El resplandor de color de la isla ya no parpadea ni muestra bordes cuadrados al expandirse: ahora se dibuja como capas del contorno en vez de con un efecto de desenfoque.
- Al expandirse, el borde inferior ya no muestra una franja negra mientras la isla rebota: la carátula de fondo cubre toda la isla.

## [1.1.2] - 2026-10-26

### Añadido
- La isla en reposo puede mostrar la hora y el porcentaje de batería (Configuración → "Hora y batería"). En equipos sin batería muestra solo la hora ([#7](https://github.com/Dysast3r/zenit/issues/7)).

## [1.1.1] - 2026-10-26

### Cambiado
- Los estilos de la isla se llaman "Notch clásico" e "Isla flotante", y el lema pasa a ser "La isla dinámica para Windows": la app, el README y el banner ya no usan nombres de modelos de iPhone.
- "Acerca de" y el README incluyen un aviso: Zenit es un proyecto independiente, sin relación con Apple ni Spotify, y sus marcas pertenecen a sus dueños.
- Las notas de cada versión en GitHub Releases incluyen sus cambios del CHANGELOG, además de cómo descargar y verificar.

## [1.1.0] - 2026-10-26

### Añadido
- Nueva identidad visual: logo, ícono, banner, GIF y videos promocionales en azul marino, azul y cian. La configuración y la isla usan la nueva paleta.
- Centro de la isla compacta configurable: hora, fecha, temperatura o el título de la canción en marquesina (o nada).
- Reuniones de Google Calendar y Outlook mediante su enlace iCal privado: aviso antes de empezar y cuenta regresiva como actividad en vivo.
- Página de volumen por aplicación: el sistema y cada app con audio, con deslizador y silencio propios.
- Elegir el monitor de la isla, o mostrarla en todas las pantallas a la vez.
- Respeta "No molestar" y las sesiones de concentración de Windows: calla notificaciones, canciones y reuniones, pero sigue mostrando el volumen, el temporizador y la batería baja.
- Las notificaciones de servicios web que llegan por el navegador (WhatsApp Web, Gmail, Telegram, Discord, Teams…) se muestran con el nombre y el color del servicio.
- Firma de código lista para SignPath Foundation (gratis para código abierto), además de Azure Artifact Signing, con verificación de la firma antes de publicar.
- CI revisa que los catálogos de traducción estén al día y avisa de los textos sin traducir.

### Cambiado
- Zenit se instala con un instalador (`Zenit-Setup-<versión>-x64.exe`) publicado en cada versión de GitHub Releases: sin permisos de administrador, con acceso en el menú Inicio y desinstalación desde Configuración. Se deja de distribuir por winget.
- Las copias instaladas se actualizan con el instalador nuevo, en silencio; las portables siguen usando el `.zip`.
- El borde de la tarjeta de estilo (notch o flotante) aparece con una animación suave al elegirla.
- La ventana de la isla se dividió en partes más pequeñas (estados, avisos, gestos, animación, controles), sin cambios de comportamiento.
- Los videos promocionales ya no se guardan en el repositorio: se generan con `marketing/motion`.

## [1.0.0] - 2026-10-26

### Añadido
- Isla con dos estilos: notch clásico pegado al borde y flotante, con tamaño y posición ajustables.
- Música de Spotify (y de otros reproductores, opcional): carátula, progreso arrastrable, controles, carátula desenfocada de fondo y ecualizador reactivo al audio.
- Letras sincronizadas desde LRCLIB.
- Temporizador y cronómetro con actividad en vivo en la isla compacta.
- Avisos del sistema: volumen (y la rueda del mouse sobre la isla para cambiarlo), carga y batería baja, y puntos de cámara y micrófono en uso.
- Notificaciones de Windows dentro de la isla (requiere la versión empaquetada MSIX / Microsoft Store).
- Páginas deslizables: música, letras, reloj y fecha, temporizador, clima, monedas, relojes mundiales y widgets.
- Widgets de la comunidad: datos de cualquier API pública declarados en JSON, sin ejecutar código.
- Clima con ubicación actual o cualquier ciudad, pronóstico por horas e íconos animados (Open-Meteo).
- Más de 300 monedas y criptomonedas con su variación diaria.
- Panel de configuración con el estilo de Windows 11 y selector de idioma: español, inglés y portugués (por defecto, el de Windows).
- Actualizaciones automáticas desde GitHub con verificación SHA-256.
- Oculta en capturas de pantalla y en aplicaciones a pantalla completa.
- Releases verificables: atestación de procedencia, sumas SHA-256, CodeQL, OpenSSF Scorecard y firma de código opcional.
- Distribución con winget, sitio web en [danidev.cl/zenit](https://danidev.cl/zenit) y catálogo de traducción para Crowdin.

[1.1.6]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.6
[1.1.5]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.5
[1.1.4]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.4
[1.1.3]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.3
[1.1.2]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.2
[1.1.1]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.1
[1.1.0]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.0
[1.0.0]: https://github.com/Dysast3r/zenit/releases/tag/v1.0.0
