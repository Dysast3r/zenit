# Cómo contribuir

¡Gracias por querer mejorar Zenit! Las contribuciones en español o inglés son bienvenidas.

## Antes de empezar

- Para cambios grandes, abre primero un issue y conversemos el enfoque.
- Los errores de seguridad se reportan en privado (ver [SECURITY.md](SECURITY.md)).

## Entorno

1. Windows 10 2004 o posterior.
2. [SDK de .NET 10](https://dotnet.microsoft.com/download).
3. `dotnet run --project src/Zenit`

En Debug existen atajos para probar la interfaz sin música:

```powershell
dotnet run --project src/Zenit -- --demo expanded
# idle | compact | peek | expanded | lyrics | compact-lyrics | clock | timer | live | volume | weather | currency | world | widgets | tour
dotnet run --project src/Zenit -- --settings
```

## Pautas

- Sigue el estilo del código existente: C# moderno, nombres en inglés y comentarios en español que expliquen el *porqué*.
- Sin dependencias NuGet de terceros, salvo que se justifique en el PR.
- Todo texto visible va en español e inglés (`Strings.T("…", "…")` o `{s:Tr Es='…', En='…'}`).
- Las animaciones usan los resortes de `Animation/Spring.cs`, no curvas con duración fija.
- Las conexiones externas solo por HTTPS, a través de `Services/Net.cs`.
- El build Release no debe tener advertencias (están configuradas como error).

## Traducciones

Los textos están en el código en español e inglés, y los demás idiomas viven en `src/Zenit/Assets/i18n/<código>.json`, con el texto en inglés como clave.

1. Ejecuta `python tools/extract-strings.py` para regenerar `en.json` y ver qué falta traducir en cada idioma.
2. Traduce los valores vacíos (o hazlo en Crowdin, que usa `crowdin.yml`).
3. Para un idioma nuevo, súmalo a `Catalogs` y a `AppLanguage` en `Services/Strings.cs`.

Para probar una traducción sin recompilar, copia el `.json` a `%AppData%\Zenit\i18n\`.

## Pull requests

1. Crea una rama desde `main`.
2. Comprueba que `dotnet build -c Release` pase sin advertencias.
3. Describe qué cambia y por qué, con capturas si afecta la interfaz.

Al contribuir aceptas que tu aporte se publique bajo la [licencia MIT](LICENSE) del proyecto.
