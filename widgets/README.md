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
| `fields[].format` | Formato numérico de .NET: `N0`, `N2`, `0.0`, `+0.00;-0.00`. |
| `fields[].prefix` / `suffix` | Texto antes y después del valor. |

## Ejemplos incluidos

- [`github-stars.json`](github-stars.json): estrellas, forks e issues de este repositorio.
- [`bitcoin.json`](bitcoin.json): precio de BTC en dólares (Coinbase).
- [`aire-santiago.json`](aire-santiago.json): calidad del aire en Santiago (Open-Meteo). Cambia `latitude` y `longitude` por tu ciudad.

Otras ideas: indicadores de Chile con [mindicador.cl](https://mindicador.cl) (UF, dólar, UTM), estado de tus repositorios, precio de criptomonedas o resultados deportivos de APIs abiertas.

¿Creaste uno útil? Compártelo con un pull request en esta carpeta.
