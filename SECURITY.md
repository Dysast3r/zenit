# Security policy

## Supported versions

Only the latest version published in [Releases](https://github.com/Dysast3r/zenit/releases) receives security fixes.

## Reporting a vulnerability

**Don't open a public issue.** Use GitHub's private reporting:

1. Go to the repository's **Security** tab.
2. Click **Report a vulnerability**.
3. Describe the problem, how to reproduce it and its impact.

I reply within 7 days. Once the issue is fixed, a security advisory is published and the reporter is credited, if they wish.

## What the app does and doesn't do

- It doesn't ask for admin rights (`asInvoker`) and installs no services or drivers.
- It stores no passwords, tokens or personal data. Its settings live in `%AppData%\Zenit\settings.json`.
- Music info is read locally through the Windows `GlobalSystemMediaTransportControls` API. Only if you turn on the **Lyrics** page are the song's title, artist and duration sent to LRCLIB to look up the lyrics.
- The equalizer only reads the system volume level; it never records audio. The camera and microphone indicators read the Windows privacy registry and never access the devices.
- Every connection uses HTTPS and only happens when the related feature is on:

  | Feature | Servers |
  |---|---|
  | Weather and city search | `api.open-meteo.com`, `geocoding-api.open-meteo.com` |
  | Your location's name | `api.bigdatacloud.net` or `ipapi.co` |
  | Currencies | `cdn.jsdelivr.net`, `currency-api.pages.dev` |
  | Lyrics | `lrclib.net` |
  | Updates | `api.github.com`, `github.com` |
  | Widgets | Only the URLs of the widgets you install |
  | Meetings | Only the iCal links of the calendars you add |

- Widgets don't run code: they're JSON files that declare an HTTPS URL and which fields to show. Widget and calendar URLs can only reach public addresses.
- Updates are installed only if the file's SHA-256 matches the `SHA256SUMS.txt` of the same version.

## Verifying a download is genuine

Every version is built on GitHub Actions from this repository and carries a build provenance attestation signed with Sigstore:

```powershell
gh attestation verify .\Zenit-Setup-<version>-x64.exe --repo Dysast3r/zenit
```

You can also compare its hash with `SHA256SUMS.txt`:

```powershell
Get-FileHash .\Zenit-Setup-<version>-x64.exe -Algorithm SHA256
```
