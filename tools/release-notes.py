"""Arma las notas de una versión para GitHub Releases (en inglés): sus cambios del CHANGELOG más cómo descargar y verificar.

Uso: python tools/release-notes.py 1.1.1 > notes.md
El workflow build.yml lo usa al publicar cada etiqueta v*.
"""
import os
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
REPOSITORY = os.environ.get("GITHUB_REPOSITORY", "Dysast3r/zenit")


def changes(version: str) -> str:
    """La sección "## [versión]" del CHANGELOG, sin su título, hasta la siguiente versión o los enlaces del final."""
    text = (ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
    match = re.search(rf"^## \[{re.escape(version)}\][^\n]*\n(.*?)(?=^## \[|^\[[^\]]+\]: |\Z)", text, re.S | re.M)
    if not match:
        sys.exit(f"CHANGELOG.md no tiene una sección para {version}")
    return match.group(1).strip()


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    version = sys.argv[1].removeprefix("v")
    print(f"""## What's new

{changes(version)}

## Download

| Your PC | File |
|---|---|
| **Windows 10 / 11 (almost every PC)** | `Zenit-Setup-{version}-x64.exe` |
| Windows on ARM (Surface Pro X, Snapdragon) | `Zenit-Setup-{version}-arm64.exe` |

Double-click it: it installs in seconds, with no admin rights, and Zenit keeps itself up to date from there. To uninstall: Windows Settings → Apps → Zenit.
Rather not install anything? The `.zip` files contain the portable version.

## Verify your download

Every file was built by GitHub Actions from this repository and carries a signed attestation.

```powershell
gh attestation verify .\\Zenit-Setup-{version}-x64.exe --repo {REPOSITORY}
Get-FileHash .\\Zenit-Setup-{version}-x64.exe -Algorithm SHA256   # compare with SHA256SUMS.txt
```""")


if __name__ == "__main__":
    main()
