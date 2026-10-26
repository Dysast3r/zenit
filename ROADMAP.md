# Hoja de ruta

Ideas para hacer de Zenit la mejor isla dinámica de Windows. ¿Quieres tomar una? Busca las etiquetas [`good first issue`](https://github.com/Dysast3r/zenit/labels/good%20first%20issue) y [`help wanted`](https://github.com/Dysast3r/zenit/labels/help%20wanted), o abre un issue nuevo.

## ✅ Hecho en 1.0

- [x] GIF en el README y video promocional de 15 s (horizontal y vertical)
- [x] Instalador con doble clic en cada versión de GitHub Releases, sin permisos de administrador (`docs/DISTRIBUTION.md`)
- [x] Notificaciones de Windows dentro de la isla (activas en la versión MSIX / Microsoft Store)
- [x] Temporizador y cronómetro como actividad en vivo
- [x] Indicadores del sistema: volumen (y rueda del mouse), carga de batería, puntos de cámara y micrófono
- [x] Letras sincronizadas y fondo con la carátula desenfocada
- [x] Widgets de la comunidad: cualquier API pública con un JSON, sin código
- [x] Actualizaciones automáticas que verifican el SHA-256 antes de instalar
- [x] Español, inglés y portugués; catálogo listo para Crowdin
- [x] Sitio web en [danidev.cl/zenit](https://danidev.cl/zenit) con demo animada
- [x] Etiquetas del repositorio y primeros `good first issue`
- [x] Notch clásico e isla flotante, tamaño ajustable, oculta en capturas

## 🔥 Próximo

- [ ] **Paquete MSIX y Microsoft Store**: firma de Microsoft, sin aviso de SmartScreen y notificaciones activas.
- [ ] **Firma de código** del `.exe` portable: el workflow ya está listo para SignPath y Azure; falta la aprobación o la cuenta (ver `docs/SIGNING.md`).
- [ ] Verificar la atestación de procedencia dentro del actualizador, además del SHA-256.
- [x] Soporte para varios monitores: elegir en cuál aparece, o en todos.
- [ ] Visualizador de audio real por bandas de frecuencia (FFT con captura loopback).

## 🎨 Diferenciadores visuales

- [ ] Temas: "Liquid Glass", neón, minimalista y uno claro.
- [ ] Animación de carga al conectar el cargador.
- [ ] Transición con la carátula al cambiar de canción.

## 🧩 Integraciones

- [x] Control de volumen por aplicación.
- [ ] Selector de salida de audio (parlantes, audífonos, Bluetooth).
- [x] Calendario de Outlook o Google: "Reunión en 5 min".
- [ ] Discord: quién habla en el canal de voz.
- [ ] Pomodoro con estadísticas.

## 🧑‍💻 Comunidad

- [ ] Más idiomas: francés, alemán, italiano, japonés… (con Crowdin).
- [ ] Galería de widgets de la comunidad en el sitio web.
- [ ] Pruebas automatizadas de los servicios (clima, monedas, formatos de fecha, widgets).
