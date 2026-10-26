# Zenit · motion graphic promocional

Spot de 15 s (30 fps, H.264 + AAC) en dos formatos:

| Formato | Archivo |
|---|---|
| Horizontal 1920×1080 | `docs/media/zenit-promo-16x9.mp4` (+ póster `.png`) |
| Vertical 1080×1920 (Reels / TikTok / Shorts) | `docs/media/zenit-promo-9x16.mp4` (+ póster `.png`) |

Solo música y animación, sin voz. La música es original: está sintetizada desde cero con numpy, así que no tiene problemas de derechos.

## Archivos

- `index.html`: toda la animación en Canvas 2D. Cada cuadro es una función pura del tiempo (`render(ctx, t)`), así que el render es determinista. Admite `?fmt=16x9|9x16` y `?t=segundos` para previsualizar un instante.
- `music.py`: banda sonora synthwave a 120 BPM (pad, bombo, clap, hats, sub bass con sidechain, arpegio, riser e impacto final). Normaliza a unos −14 LUFS con pico ≤ −1 dBFS y hace un fade out de 0,8 s.
- `render.py`: abre la página en Chromium headless (Playwright), renderiza cada cuadro con motion blur (obturador de 180°: 6 sub-cuadros, o 16 en los movimientos rápidos para que no queden "fantasmas") y dithering, y lo envía por pipe al ffmpeg que trae `imageio-ffmpeg` (libx264 crf 18, preset slow, yuv420p BT.709, +faststart, AAC 192k).

## Guion (120 BPM: 1 pulso = 0,5 s)

| Tiempo | Qué pasa |
|---|---|
| 0–2 s | Sube un brillo desde abajo y aparece "El punto más alto de tu pantalla.". La isla nace como un punto en el borde superior y se estira con un resorte hasta la píldora compacta. |
| 2 s (drop) | La isla se expande a la tarjeta de reproducción: carátula, ecualizador al ritmo del bombo, barra de progreso y controles. En 3,5 s se pulsa "siguiente" y cambia la canción. |
| 4,5 / 6 / 7,5 s | Swipes entre páginas: Clima (sol y nube animados, pronóstico por hora), Monedas (USD/EUR/BTC con contadores y sparklines) y Relojes (esferas analógicas cuyas manecillas giran con resorte). |
| 9–12 s | La isla se contrae y aparece tipografía cinética, una palabra por pulso: Spotify · Clima · Monedas · Relojes · Notch o flotante · Código abierto. En 11 s la isla pasa de notch a píldora flotante. |
| 12–15 s | Impacto: la píldora vuela y se convierte en el ícono de la app. Luego entran "Zenit", el tagline, `github.com/Dysast3r/zenit` y "por Danidev.cl". |

En vertical la composición se rearma, no se recorta: la isla vive en el borde superior de una "pantalla" de escritorio, los títulos son más grandes y van en dos líneas, y todo queda dentro de márgenes seguros para la interfaz de las redes sociales (aprox. 250 px arriba y 360 px abajo).

## Cómo volver a renderizar

Requisitos: Python 3.11+ y la fuente Segoe UI Variable (viene con Windows 11).

```powershell
# 1. Entorno aislado (fuera del repo)
python -m venv $env:TEMP\zenit-motion-venv
& $env:TEMP\zenit-motion-venv\Scripts\python -m pip install numpy scipy pyloudnorm playwright imageio-ffmpeg
& $env:TEMP\zenit-motion-venv\Scripts\python -m playwright install chromium

# 2. Música
cd marketing\motion
& $env:TEMP\zenit-motion-venv\Scripts\python music.py $env:TEMP\zenit-music.wav

# 3. Videos y pósters (cada formato tarda unos minutos)
& $env:TEMP\zenit-motion-venv\Scripts\python render.py --fmt 16x9 --audio $env:TEMP\zenit-music.wav --out ..\..\docs\media\zenit-promo-16x9.mp4 --poster ..\..\docs\media\zenit-promo-16x9.png --poster-t 14
& $env:TEMP\zenit-motion-venv\Scripts\python render.py --fmt 9x16 --audio $env:TEMP\zenit-music.wav --out ..\..\docs\media\zenit-promo-9x16.mp4 --poster ..\..\docs\media\zenit-promo-9x16.png --poster-t 14
```

Para revisar cuadros sueltos sin codificar video:

```powershell
& $env:TEMP\zenit-motion-venv\Scripts\python render.py --fmt 9x16 --samples 1 --stills 1.4 3 6.8 9.7 13.9 --stills-dir $env:TEMP\zenit-stills
```

Para previsualizar en el navegador, sirve la raíz del repo (`python -m http.server` desde la raíz) y abre `http://localhost:8000/marketing/motion/index.html?fmt=16x9&t=3`. Hay que usar un servidor porque el ícono se carga desde `docs/icon.png`.

No se versionan los videos, los pósters, los cuadros intermedios ni el venv: `docs/media/*.mp4` y los pósters están en `.gitignore`. Para tenerlos, renderízalos con los pasos de arriba.
