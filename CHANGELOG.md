# Changelog

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). The project uses [semantic versioning](https://semver.org/).

## [1.1.9] - 2026-10-27

### Fixed
- UF and UTM show up on the currencies page again: they now come from findic.cl, with mindicador.cl as a fallback, because mindicador.cl often rejects connections.

## [1.1.8] - 2026-10-27

### Added
- Indicators list in Settings → Currencies: turn on the major currencies (dollar, euro, pound, yen, yuan, Swiss franc, Latin American pesos…), crypto and Chile's UF and UTM with a switch each. Other currencies can still be added by code.
- UF and UTM come from mindicador.cl, converted to your local currency, with the change versus the previous value.

### Changed
- The currencies page shows up to 8 indicators: with more than 4 it switches to two rows of compact tiles.

## [1.1.7] - 2026-10-26

### Fixed
- The SpaceX launch widget no longer shows "Unavailable": it uses Launch Library 2's lightweight mode, which answers in under a second instead of about 20. It now shows the launch status instead of the mission name. If you installed it with 1.1.6, delete `spacex-next-launch.json` from the widgets folder and click "Install examples" again.

## [1.1.6] - 2026-10-26

### Added
- Next SpaceX launch widget: countdown, mission and date from Launch Library 2, no key needed ([#4](https://github.com/Dysast3r/zenit/issues/4)).
- Widgets can format dates: `"format": "relative"` shows a countdown that ticks every minute, and any .NET date format shows the date in your local time.

### Changed
- In widgets, a field without a label uses the full width of the card.

## [1.1.5] - 2026-10-26

### Added
- TIDAL and YouTube Music support (the installed app and the community desktop apps): they get the same priority as Spotify, and clicking the album art opens the app that's playing.

### Changed
- The island shows what's actually playing first: a paused Spotify no longer hides another music app that's playing.
- The "Spotify only" setting becomes "Music apps only" (Spotify, TIDAL and YouTube Music) and keeps what you had chosen.

### Fixed
- The volume bar shows up again when you scroll the mouse wheel over the island: scrolling cancels the hover expansion, and when the island is already expanded the bar briefly replaces the page dots.

## [1.1.4] - 2026-10-26

### Added
- If the last page you had open was Lyrics, the compact island keeps showing the line being sung, with the album art and the equalizer on either side.

## [1.1.3] - 2026-10-26

### Fixed
- The island's color glow no longer flickers or shows square edges while expanding: it's now drawn as layers of the outline instead of with a blur effect.
- While expanding, the bottom edge no longer shows a black strip as the island bounces: the album-art backdrop covers the whole island.

## [1.1.2] - 2026-10-26

### Added
- The idle island can show the time and the battery percentage (Settings → "Time and battery"). Devices without a battery show just the time ([#7](https://github.com/Dysast3r/zenit/issues/7)).

## [1.1.1] - 2026-10-26

### Changed
- The island styles are now called "Classic notch" and "Floating island", and the tagline is "The dynamic island for Windows": the app, the README and the banner no longer use iPhone model names.
- "About" and the README include a notice: Zenit is an independent project, not affiliated with Apple or Spotify, and their trademarks belong to their owners.
- The notes for each release on GitHub include its changes from the CHANGELOG, plus how to download and verify it.

## [1.1.0] - 2026-10-26

### Added
- New visual identity: logo, icon, banner, GIF and promo videos in navy, blue and cyan. Settings and the island use the new palette.
- Configurable center of the compact island: time, date, temperature or the scrolling song title (or nothing).
- Google Calendar and Outlook meetings through their private iCal link: a heads-up before they start and a countdown as a live activity.
- Per-app volume page: the system and every app playing audio, each with its own slider and mute.
- Choose the island's display, or show it on all screens at once.
- Respects Windows Do not disturb and focus sessions: notifications, songs and meetings stay quiet, while volume, the timer and low battery still show.
- Notifications from web services that arrive through the browser (WhatsApp Web, Gmail, Telegram, Discord, Teams…) show the service's name and color.
- Code signing ready for SignPath Foundation (free for open source), as well as Azure Artifact Signing, with signature verification before publishing.
- CI checks that the translation catalogs are up to date and flags untranslated texts.

### Changed
- Zenit is installed with an installer (`Zenit-Setup-<version>-x64.exe`) published with every GitHub release: no admin rights, a Start menu entry and uninstall from Settings. It's no longer distributed through winget.
- Installed copies update silently with the new installer; portable copies keep using the `.zip`.
- The style card's border (notch or floating) animates in smoothly when you pick it.
- The island window was split into smaller parts (states, alerts, gestures, animation, controls), with no change in behavior.
- Promo videos are no longer stored in the repository: they're generated with `marketing/motion`.

## [1.0.0] - 2026-10-26

### Added
- An island with two styles, a classic notch attached to the edge or floating, with adjustable size and position.
- Spotify music (and other players, optionally): album art, a draggable progress bar, controls, a blurred album-art backdrop and an equalizer that reacts to the audio.
- Synced lyrics from LRCLIB.
- Timer and stopwatch with a live activity on the compact island.
- System alerts: volume (and the mouse wheel over the island to change it), charging and low battery, and camera and microphone dots when in use.
- Windows notifications inside the island (requires the packaged MSIX / Microsoft Store version).
- Swipeable pages: music, lyrics, clock and date, timer, weather, currencies, world clocks and widgets.
- Community widgets: data from any public API declared in JSON, without running code.
- Weather for your current location or any city, with an hourly forecast and animated icons (Open-Meteo).
- More than 300 currencies and cryptocurrencies with their daily change.
- A Windows 11-style settings panel and a language picker: Spanish, English and Portuguese (Windows' language by default).
- Automatic updates from GitHub with SHA-256 verification.
- Hidden from screenshots and from full-screen apps.
- Verifiable releases: build provenance attestation, SHA-256 sums, CodeQL, OpenSSF Scorecard and optional code signing.
- Distribution through winget, a website at [danidev.cl/zenit](https://danidev.cl/zenit) and a translation catalog for Crowdin.

[1.1.9]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.9
[1.1.8]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.8
[1.1.7]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.7
[1.1.6]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.6
[1.1.5]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.5
[1.1.4]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.4
[1.1.3]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.3
[1.1.2]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.2
[1.1.1]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.1
[1.1.0]: https://github.com/Dysast3r/zenit/releases/tag/v1.1.0
[1.0.0]: https://github.com/Dysast3r/zenit/releases/tag/v1.0.0
