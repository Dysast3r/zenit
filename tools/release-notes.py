"""Arma las notas de una versión para GitHub Releases: sus cambios del CHANGELOG más cómo descargar y verificar.

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
    print(f"""## Novedades

{changes(version)}

## Descargar

| Tu equipo | Archivo |
|---|---|
| **Windows 10 / 11 (casi todos los PC)** | `Zenit-Setup-{version}-x64.exe` |
| Windows en ARM (Surface Pro X, Snapdragon) | `Zenit-Setup-{version}-arm64.exe` |

Ábrelo con doble clic: se instala en segundos, sin permisos de administrador, y Zenit se actualiza sola desde ahí. Para desinstalar: Configuración de Windows → Aplicaciones → Zenit.
¿Prefieres no instalar nada? Los `.zip` traen la versión portable.

## Verificar la descarga

Cada archivo fue compilado por GitHub Actions desde este repositorio y lleva una atestación firmada.

```powershell
gh attestation verify .\\Zenit-Setup-{version}-x64.exe --repo {REPOSITORY}
Get-FileHash .\\Zenit-Setup-{version}-x64.exe -Algorithm SHA256   # compara con SHA256SUMS.txt
```""")


if __name__ == "__main__":
    main()
