; Instalador de Zenit (Inno Setup 6).
; Instala por usuario en %LOCALAPPDATA%\Programs\Zenit, sin pedir permisos de administrador: así el
; actualizador de la app puede reemplazar sus archivos sin elevar.
;
; Lo compila el workflow build.yml; a mano:
;   iscc /DAppVersion=1.1.0 /DNumericVersion=1.1.0 /DArch=x64 /DSourceDir=..\..\out /DOutputDir=..\..\dist Zenit.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef NumericVersion
  #define NumericVersion "0.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\out"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\dist"
#endif

#define AppName "Zenit"
#define AppExe "Zenit.exe"
#define Publisher "Danidev.cl"
#define RepoUrl "https://github.com/Dysast3r/zenit"

[Setup]
; Identificador fijo: con él Windows reconoce las versiones nuevas como actualización, no como otra app.
AppId={{6C7B1D52-3F0E-4E1B-9C2A-5A0E3F8D2B47}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#Publisher}
AppPublisherURL=https://danidev.cl
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
AppCopyright=Copyright © 2026 Daniel Moraga · Danidev.cl
VersionInfoVersion={#NumericVersion}
VersionInfoProductVersion={#NumericVersion}
VersionInfoDescription=Instalador de {#AppName}
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
UsePreviousTasks=yes
ArchitecturesAllowed={#Arch == "arm64" ? "arm64" : "x64compatible"}
ArchitecturesInstallIn64BitMode={#Arch == "arm64" ? "arm64" : "x64compatible"}
MinVersion=10.0.19041
; La app vive en la bandeja: se cierra sola antes de copiar los archivos nuevos.
CloseApplications=force
RestartApplications=no
SetupIconFile=..\..\src\Zenit\Assets\icon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
WizardSizePercent=100
OutputDir={#OutputDir}
OutputBaseFilename=Zenit-Setup-{#AppVersion}-{#Arch}
Compression=lzma2/ultra64
SolidCompression=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
spanish.StartWithWindows=Iniciar Zenit con Windows
english.StartWithWindows=Start Zenit with Windows
portuguese.StartWithWindows=Iniciar o Zenit com o Windows
spanish.Launch=Abrir Zenit
english.Launch=Open Zenit
portuguese.Launch=Abrir o Zenit

[Tasks]
Name: "startup"; Description: "{cm:StartWithWindows}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Inicio con Windows: la misma clave que usa la opción de la app, para que ambas estén de acuerdo.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Zenit"; \
  ValueData: """{app}\{#AppExe}"""; Tasks: startup
; Al desinstalar se borra siempre, aunque el usuario la haya activado desde la app.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Zenit"; \
  Flags: uninsdeletevalue dontcreatekey

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent
; Actualización silenciosa desde la propia app: se vuelve a abrir sola al terminar.
Filename: "{app}\{#AppExe}"; Flags: nowait; Check: WizardSilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#AppExe} /F"; Flags: runhidden; RunOnceId: "CloseZenit"
