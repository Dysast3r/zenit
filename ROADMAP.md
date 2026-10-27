# Roadmap

Ideas to make Zenit the best dynamic island on Windows. Want to pick one up? Look for the [`good first issue`](https://github.com/Dysast3r/zenit/labels/good%20first%20issue) and [`help wanted`](https://github.com/Dysast3r/zenit/labels/help%20wanted) labels, or open a new issue.

## ✅ Done in 1.0

- [x] GIF in the README and a 15-second promo video (landscape and portrait)
- [x] Double-click installer in every GitHub release, no admin rights needed (`docs/DISTRIBUTION.md`)
- [x] Windows notifications inside the island (on in the MSIX / Microsoft Store version)
- [x] Timer and stopwatch as a live activity
- [x] System indicators: volume (and the mouse wheel), battery charging, camera and microphone dots
- [x] Synced lyrics and a blurred album-art backdrop
- [x] Community widgets: any public API with a JSON file, no code
- [x] Automatic updates that check the SHA-256 before installing
- [x] Spanish, English and Portuguese; catalog ready for Crowdin
- [x] Website at [danidev.cl/zenit](https://danidev.cl/zenit) with an animated demo
- [x] Repository labels and the first `good first issue`s
- [x] Classic notch and floating island, adjustable size, hidden from screenshots

## ✅ Done in 1.1

- [x] Support for several displays: choose which one shows the island, or all of them
- [x] Battery level on the idle island
- [x] Lyrics keep showing on the compact island
- [x] TIDAL and YouTube Music, with the music that's playing taking priority
- [x] Volume bar while the island is expanded
- [x] Smoother glow when the island expands
- [x] Dates and live countdowns in widgets, and a SpaceX launch widget

## 🔥 Next

- [ ] **MSIX package and Microsoft Store**: Microsoft signing, no SmartScreen warning and notifications on.
- [ ] **Code signing** for the portable `.exe`: the workflow is ready for SignPath and Azure; it needs the approval or the account (see `docs/SIGNING.md`).
- [ ] Verify the build provenance attestation inside the updater, not just the SHA-256 ([#14](https://github.com/Dysast3r/zenit/issues/14)).
- [ ] A real audio visualizer by frequency band (FFT with loopback capture).

## 🎨 Visual touches

- [x] Light theme ([#15](https://github.com/Dysast3r/zenit/issues/15)) and a customizable, animated neon edge.
- [ ] More themes: "Liquid Glass" and minimal.
- [ ] Charging animation when the charger is plugged in ([#16](https://github.com/Dysast3r/zenit/issues/16)).
- [x] Album-art transition when the song changes.

## 🧩 Integrations

- [x] Per-app volume control.
- [x] Audio output picker (speakers, headphones, Bluetooth).
- [x] Outlook or Google calendar: "Meeting in 5 min".
- [ ] Discord: who's talking in the voice channel.
- [x] Pomodoro with stats.

## 🧑‍💻 Community

- [ ] More languages: French, German, Italian, Japanese… (with Crowdin) ([#1](https://github.com/Dysast3r/zenit/issues/1), [#2](https://github.com/Dysast3r/zenit/issues/2), [#9](https://github.com/Dysast3r/zenit/issues/9), [#10](https://github.com/Dysast3r/zenit/issues/10)).
- [ ] Community widget gallery on the website.
- [ ] Automated tests for the services: weather, currencies, date formats, widgets ([#13](https://github.com/Dysast3r/zenit/issues/13)).
