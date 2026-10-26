"""Extrae los textos traducibles del código y actualiza los catálogos de idiomas.

Busca Strings.T("es", "en"), Strings.F("es", "en", ...) y {s:Tr Es='…', En='…'} en src/Zenit.
- Escribe src/Zenit/Assets/i18n/en.json (texto en inglés → sí mismo): es la fuente para Crowdin.
- En cada otro catálogo (pt.json, …) agrega las claves nuevas vacías y elimina las que ya no existen.

Uso: python tools/extract-strings.py           actualiza los catálogos
     python tools/extract-strings.py --check   solo revisa (CI): falla si los catálogos no están al día
                                                y avisa de los textos que faltan por traducir
"""
import html
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SOURCE = ROOT / "src" / "Zenit"
I18N = SOURCE / "Assets" / "i18n"

CS_LITERAL = r'"((?:[^"\\]|\\.)*)"'
CS_CALL = re.compile(r"\b(?:Strings\.)?[TF]\(\s*" + CS_LITERAL + r"\s*,\s*" + CS_LITERAL)
CS_ESCAPE = re.compile(r"\\(u[0-9a-fA-F]{4}|.)")
CS_SIMPLE_ESCAPES = {"n": "\n", "t": "\t", "r": "\r", "0": "\0"}

# Los atributos pueden venir en cualquier orden: Es=… En=… o En=… Es=….
XAML_VALUE = r"'((?:[^']|'')*)'"
XAML_TR = re.compile(r"\{s:Tr\s+Es=" + XAML_VALUE + r",\s*En=" + XAML_VALUE + r"\}")
XAML_TR_EN_FIRST = re.compile(r"\{s:Tr\s+En=" + XAML_VALUE + r",\s*Es=" + XAML_VALUE + r"\}")


def unescape_cs(text: str) -> str:
    def replace(match: re.Match) -> str:
        escape = match.group(1)
        if len(escape) == 5:  # \uXXXX
            return chr(int(escape[1:], 16))
        return CS_SIMPLE_ESCAPES.get(escape, escape)  # \\ y \" quedan como el carácter mismo

    return CS_ESCAPE.sub(replace, text)


def unescape_xaml(text: str) -> str:
    return html.unescape(text.replace("''", "'"))


def write_json(path: pathlib.Path, data: dict[str, str]) -> None:
    # El repositorio guarda los catálogos con saltos de línea LF (ver .gitattributes).
    with path.open("w", encoding="utf-8", newline="\n") as file:
        file.write(json.dumps(data, ensure_ascii=False, indent=2) + "\n")


def collect() -> list[str]:
    found: set[str] = set()
    for path in SOURCE.rglob("*.cs"):
        if "obj" in path.parts or "bin" in path.parts:
            continue
        for _, en in CS_CALL.findall(path.read_text(encoding="utf-8-sig")):
            found.add(unescape_cs(en))
    for path in SOURCE.rglob("*.xaml"):
        if "obj" in path.parts or "bin" in path.parts:
            continue
        text = path.read_text(encoding="utf-8-sig")
        for _, en in XAML_TR.findall(text):
            found.add(unescape_xaml(en))
        for en, _ in XAML_TR_EN_FIRST.findall(text):
            found.add(unescape_xaml(en))
    return sorted(s for s in found if s.strip())


def check() -> int:
    """Revisa sin escribir. Devuelve 1 si alguien agregó o quitó textos sin volver a ejecutar el script."""
    keys = collect()
    failed = False

    english = I18N / "en.json"
    current = json.loads(english.read_text(encoding="utf-8-sig") or "{}") if english.exists() else {}
    added = [k for k in keys if k not in current]
    removed = [k for k in current if k not in keys]
    if added or removed:
        failed = True
        print(f"::error file=src/Zenit/Assets/i18n/en.json::en.json no está al día: {len(added)} textos nuevos y "
              f"{len(removed)} que ya no existen. Ejecuta python tools/extract-strings.py y sube los catálogos.")
        for key in added[:10]:
            print(f"  + {key}")
        for key in removed[:10]:
            print(f"  - {key}")

    for catalog in sorted(I18N.glob("*.json")):
        if catalog.name == "en.json":
            continue
        data = json.loads(catalog.read_text(encoding="utf-8-sig") or "{}")
        if set(data) != set(keys):
            failed = True
            print(f"::error file=src/Zenit/Assets/i18n/{catalog.name}::{catalog.name} no tiene las mismas claves que el código. "
                  f"Ejecuta python tools/extract-strings.py.")
            continue
        # Los textos vacíos se completan en Crowdin: avisan, pero no detienen el build.
        missing = [k for k in keys if not data.get(k)]
        if missing:
            print(f"::warning file=src/Zenit/Assets/i18n/{catalog.name}::{len(missing)} textos sin traducir en {catalog.name}")
            for key in missing[:10]:
                print(f"  · {key}")
        else:
            print(f"{catalog.name}: completo ({len(keys)} textos)")

    return 1 if failed else 0


def main() -> None:
    # La consola de Windows no siempre usa UTF-8: así los textos con tildes se ven bien en cualquier terminal.
    sys.stdout.reconfigure(encoding="utf-8")
    if "--check" in sys.argv[1:]:
        sys.exit(check())

    keys = collect()
    I18N.mkdir(parents=True, exist_ok=True)
    write_json(I18N / "en.json", {k: k for k in keys})

    for catalog in I18N.glob("*.json"):
        if catalog.name == "en.json":
            continue
        current = json.loads(catalog.read_text(encoding="utf-8-sig") or "{}")
        updated = {k: current.get(k, "") for k in keys}
        write_json(catalog, updated)
        missing = sum(1 for v in updated.values() if not v)
        print(f"{catalog.name}: {len(keys)} textos, {missing} sin traducir")

    print(f"en.json: {len(keys)} textos")


if __name__ == "__main__":
    main()
