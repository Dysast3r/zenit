# Firma de código

Sin firma, Windows SmartScreen muestra "Windows protegió su PC" la primera vez que alguien abre el `.exe`. La firma con un certificado de confianza elimina esa advertencia y garantiza que el archivo no fue modificado.

El workflow `build.yml` ya trae los pasos de las dos opciones, desactivados. Para activar una solo hay que crear sus secretos y variables en GitHub; el código no cambia. Con cualquiera activa, el paso **Verificar firma** detiene la publicación si el `.exe` no quedó firmado.

## Opción A: SignPath Foundation (gratis para código abierto)

1. Solicita el programa en <https://signpath.org/apply>. Requisitos: licencia OSI (MIT cumple), repositorio público y builds reproducibles en CI (ya configurados).
2. Cuando te aprueben, en SignPath:
   - Crea el proyecto con el slug `zenit` y conéctalo a este repositorio de GitHub.
   - Crea una *artifact configuration* para un `.exe` suelto (el artefacto que sube el workflow es un zip con `Zenit.exe`, que se firma con Authenticode).
   - Crea la política de firma `release-signing` y un usuario CI con su API token.
3. En GitHub → Settings → Secrets and variables → Actions:
   - **Secret:** `SIGNPATH_API_TOKEN`
   - **Variables:** `SIGNPATH_ENABLED` = `true`, `SIGNPATH_ORGANIZATION_ID`. Si usaste otros nombres, también `SIGNPATH_PROJECT_SLUG` y `SIGNPATH_POLICY_SLUG`.
4. El próximo tag `v*` sube el `.exe` sin firmar como artefacto, SignPath lo firma y el paquete se arma con la versión firmada.

## Opción B: Azure Artifact Signing (de pago, unos USD 10 al mes)

1. Crea una cuenta de **Artifact Signing** en Azure y completa la validación de identidad. Revisa antes si está disponible para tu país y para personas naturales.
2. Crea un *certificate profile* de tipo **Public Trust**.
3. Crea un *App Registration* en Microsoft Entra ID con el rol **Artifact Signing Certificate Profile Signer** sobre la cuenta.
4. En GitHub → Settings → Secrets and variables → Actions:
   - **Secrets:** `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`
   - **Variables:** `SIGNING_ENABLED` = `true`, `SIGNING_ENDPOINT` (p. ej. `https://eus.codesigning.azure.net/`), `SIGNING_ACCOUNT`, `SIGNING_PROFILE`
5. El próximo tag `v*` publicará el `.exe` firmado.

## Mientras no haya firma

La atestación de procedencia y `SHA256SUMS.txt` permiten igual que cualquiera compruebe que el archivo salió de este repositorio sin modificaciones (ver [SECURITY.md](../SECURITY.md)). La reputación de SmartScreen también crece sola a medida que más personas descargan la app.
