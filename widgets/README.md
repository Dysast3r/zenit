# Community widgets

**English** · [Español](README.es.md)

A widget is a `.json` file that tells Zenit which public API to query and what data to show. It appears on the **Widgets** page of the expanded island.

**Safe by design:** a widget can't run code. It only makes a read-only HTTPS request (GET), picks a few values and shows them as text.

## Install

1. Settings → **Widgets** → **Open folder**. It's `%AppData%\Zenit\widgets`.
2. Copy the `.json` there and click **Reload**.
3. Turn on the **Widgets** page in Settings → Pages.

Up to 8 widgets are loaded and the island shows 4 at a time.

## Format

```json
{
  "name": "Bitcoin",
  "icon": "₿",
  "accent": "#F7931A",
  "url": "https://api.coinbase.com/v2/prices/BTC-USD/spot",
  "refreshMinutes": 5,
  "fields": [
    { "label": "Price", "path": "data.amount", "format": "N0", "prefix": "$", "suffix": " USD" }
  ]
}
```

| Field | Description |
|---|---|
| `name` | The widget's title. |
| `icon` | A character or emoji for the badge. |
| `accent` | The badge color, in hex. |
| `url` | A public API that returns JSON. **Must be HTTPS** and need no key. |
| `refreshMinutes` | How often it updates (1 to 1440). |
| `fields` | Up to 4 values. The first one is shown large. |
| `fields[].path` | Path inside the JSON: `data.items[0].price`. |
| `fields[].format` | Numbers: a .NET format (`N0`, `N2`, `0.0`, `+0.00;-0.00`). Dates (ISO 8601 or Unix seconds): `relative` for a countdown that ticks every minute ("in 5 h 12 min", "3 min ago"), or a .NET date format (`d MMM, HH:mm`) in your local time. |
| `fields[].label` | Text next to the value. Leave it empty (`""`) so a long value can use the full width. |
| `fields[].prefix` / `suffix` | Text before and after the value. |

## Included examples

- [`github-stars.json`](github-stars.json): stars, forks and issues of this repository.
- [`bitcoin.json`](bitcoin.json): the BTC price in US dollars (Coinbase).
- [`aire-santiago.json`](aire-santiago.json): air quality in Santiago, Chile (Open-Meteo). Change `latitude` and `longitude` to your city.
- [`spacex-next-launch.json`](spacex-next-launch.json): countdown to the next SpaceX launch, with the mission and date ([Launch Library 2](https://thespacedevs.com/llapi), no key; it allows 15 requests per hour, so it updates every 30 minutes).

The bundled examples use Spanish labels; change `label` to whatever you like.

More ideas: Chilean indicators from [mindicador.cl](https://mindicador.cl) (UF, dollar, UTM), the status of your repositories, crypto prices or sports results from open APIs.

Made a useful one? Share it with a pull request to this folder.
