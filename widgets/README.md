# Widgets de la comunidad

Un widget es un archivo `.json` que le dice a Zenit qué API pública consultar y qué datos mostrar. Aparece en la página **Widgets** de la isla expandida.

**Es seguro por diseño:** un widget no puede ejecutar código. Solo hace una petición HTTPS de lectura (GET), extrae algunos valores y los muestra como texto.

## Instalar

1. Configuración → **Widgets** → **Abrir carpeta**. Es `%AppData%\Zenit\widgets`.
2. Copia ahí el `.json` y pulsa **Recargar**.
3. Activa la página **Widgets** en Configuración → Páginas.

Se cargan hasta 8 widgets y la isla muestra 4 a la vez.

## Formato

```json
{
  "name": "Bitcoin",
  "icon": "₿",
  "accent": "#F7931A",
  "url": "https://api.coinbase.com/v2/prices/BTC-USD/spot",
  "refreshMinutes": 5,
  "fields": [
    { "label": "Precio", "path": "data.amount", "format": "N0", "prefix": "$", "suffix": " USD" }
  ]
}
```

| Campo | Descripción |
|---|---|
| `name` | Título del widget. |
| `icon` | Un carácter o emoji para la insignia. |
| `accent` | Color de la insignia, en hexadecimal. |
| `url` | API pública que devuelve JSON. **Debe ser HTTPS** y no requerir clave. |
| `refreshMinutes` | Cada cuánto se actualiza (1 a 1440). |
| `fields` | Hasta 4 valores. El primero se muestra en grande. |
| `fields[].path` | Ruta dentro del JSON: `data.items[0].price`. |
| `fields[].format` | Números: formato de .NET (`N0`, `N2`, `0.0`, `+0.00;-0.00`). Fechas (ISO 8601 o segundos Unix): `relative` para una cuenta regresiva que avanza cada minuto ("en 5 h 12 min", "hace 3 min"), o un formato de fecha de .NET (`d MMM, HH:mm`) en tu hora local. |
| `fields[].label` | Texto junto al valor. Déjalo vacío (`""`) para que un valor largo use todo el ancho. |
| `fields[].prefix` / `suffix` | Texto antes y después del valor. |

## Ejemplos incluidos

- [`github-stars.json`](github-stars.json): estrellas, forks e issues de este repositorio.
- [`bitcoin.json`](bitcoin.json): precio de BTC en dólares (Coinbase).
- [`aire-santiago.json`](aire-santiago.json): calidad del aire en Santiago (Open-Meteo). Cambia `latitude` y `longitude` por tu ciudad.
- [`spacex-next-launch.json`](spacex-next-launch.json): cuenta regresiva al próximo lanzamiento de SpaceX, con la misión y la fecha ([Launch Library 2](https://thespacedevs.com/llapi), sin clave; permite 15 consultas por hora, así que se actualiza cada 30 minutos).

Otras ideas: indicadores de Chile con [mindicador.cl](https://mindicador.cl) (UF, dólar, UTM), estado de tus repositorios, precio de criptomonedas o resultados deportivos de APIs abiertas.

¿Creaste uno útil? Compártelo con un pull request en esta carpeta.
