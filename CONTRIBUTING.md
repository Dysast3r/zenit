# Contributing

**English** · [Español](CONTRIBUTING.es.md)

Thanks for wanting to make Zenit better! Contributions in English or Spanish are welcome.

## Before you start

- For big changes, open an issue first so we can agree on the approach.
- Security issues are reported privately (see [SECURITY.md](SECURITY.md)).
- Looking for something to do? Check the [`good first issue`](https://github.com/Dysast3r/zenit/labels/good%20first%20issue) and [`help wanted`](https://github.com/Dysast3r/zenit/labels/help%20wanted) labels, or the [roadmap](ROADMAP.md).

## Setup

1. Windows 10 2004 or later.
2. [.NET 10 SDK](https://dotnet.microsoft.com/download).
3. `dotnet run --project src/Zenit`

Debug builds include shortcuts to try the UI with sample data, no music needed:

```powershell
dotnet run --project src/Zenit -- --demo expanded
# idle | compact | peek | expanded | marquee | meeting | mixer | lyrics | compact-lyrics | clock | timer | live | volume | expanded-volume | weather | currency | world | widgets | tour
dotnet run --project src/Zenit -- --settings
```

## Guidelines

- Follow the existing code style: modern C# and English names. Code comments are in Spanish and explain the *why*; comments in English are fine in your contributions.
- No third-party NuGet dependencies unless the PR explains why.
- Every visible text comes in Spanish and English (`Strings.T("…", "…")` or `{s:Tr Es='…', En='…'}`).
- Animations use the springs in `Animation/Spring.cs`, not fixed-duration curves.
- External connections only over HTTPS, through `Services/Net.cs`.
- The Release build must have no warnings (they're treated as errors).

## Translations

Texts live in the code in Spanish and English, and every other language lives in `src/Zenit/Assets/i18n/<code>.json`, keyed by the English text.

1. Run `python tools/extract-strings.py` to regenerate `en.json` and see what's missing in each language.
2. Translate the empty values (or do it on Crowdin, which uses `crowdin.yml`).
3. For a new language, add it to `Catalogs` and `AppLanguage` in `Services/Strings.cs`, plus the option in the language picker (`BuildStaticChoices` in `Views/SettingsWindow.xaml.cs`).

To try a translation without rebuilding, copy the `.json` to `%AppData%\Zenit\i18n\`.

## Pull requests

1. Create a branch from `main`.
2. Make sure `dotnet build -c Release` passes with no warnings.
3. Describe what changes and why, with screenshots if it affects the UI.

By contributing, you agree that your work is published under the project's [MIT license](LICENSE).
