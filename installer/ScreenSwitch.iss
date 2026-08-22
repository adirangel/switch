; Inno Setup script for ScreenSwitch.
;
; Deliberately a *per-user* install: PrivilegesRequired=lowest keeps the whole thing inside the
; user's own profile and never asks for administrator rights. That matters more than it looks —
; the application itself only ever writes to HKCU, so requiring elevation here would be the one
; thing that turns it from "a small tool I ran" into "an installation", which is exactly the line
; a managed work machine refuses to cross.
;
; Build:
;   iscc /DAppExe=..\publish\ScreenSwitch.exe installer\ScreenSwitch.iss

#define AppName "ScreenSwitch"
#define AppPublisher "ScreenSwitch"
#define AppUrl "https://github.com/adirangel/switch"

#ifndef AppExe
  #define AppExe "..\publish\ScreenSwitch.exe"
#endif

#ifndef AppVersion
  ; Resolved against the .iss directory so the lookup does not depend on the compiler's cwd.
  #define AppVersion GetVersionNumbersString(AddBackslash(SourcePath) + AppExe)
#endif

[Setup]
; Never change AppId: it is how Windows recognises an existing install to upgrade in place.
AppId={{8F3A6C21-5D74-4E1B-9C3F-2A7E4B6D0915}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}/releases

; Per-user throughout: no elevation prompt, no admin account needed.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableDirPage=no
DisableProgramGroupPage=yes
AllowNoIcons=yes

OutputDir=..\installer-output
OutputBaseFilename=ScreenSwitch-Setup
SetupIconFile=..\src\ScreenSwitch\Resources\app.ico
UninstallDisplayIcon={app}\ScreenSwitch.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; The tray app holds a mutex and keeps its own .exe locked, so an upgrade over a running copy
; fails unless Setup is allowed to close it first.
CloseApplications=yes
RestartApplications=no

; Nothing here needs a 32-bit code path.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
; The five below ship with Inno Setup itself.
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "pt"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "he"; MessagesFile: "compiler:Languages\Hebrew.isl"
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"

; Chinese and Arabic are unofficial Inno translations and are not part of a standard install, so
; they are vendored under languages\. When they are absent the wizard shows English for those two
; while the application itself stays fully translated — see installer/languages/README.md.
#if FileExists(AddBackslash(SourcePath) + "languages\ChineseSimplified.isl")
Name: "zh-Hans"; MessagesFile: "languages\ChineseSimplified.isl"
#endif
#if FileExists(AddBackslash(SourcePath) + "languages\Arabic.isl")
Name: "ar"; MessagesFile: "languages\Arabic.isl"
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "{cm:AutoStartTask}"

[Files]
Source: "{#AppExe}"; DestDir: "{app}"; DestName: "ScreenSwitch.exe"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\ScreenSwitch.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\ScreenSwitch.exe"; Tasks: desktopicon

[Run]
; Auto-start goes through the application's own flag rather than a [Registry] entry here, so the
; Run key has exactly one writer and the tray menu's checkbox always agrees with reality.
Filename: "{app}\ScreenSwitch.exe"; Parameters: "--autostart on"; Tasks: autostart; Flags: runhidden waituntilterminated
Filename: "{app}\ScreenSwitch.exe"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Before the files go: otherwise the Run key is left pointing at an .exe that no longer exists,
; and Windows quietly tries to launch it at every login.
Filename: "{app}\ScreenSwitch.exe"; Parameters: "--autostart off"; RunOnceId: "ClearAutoStart"; Flags: runhidden waituntilterminated

[CustomMessages]
; The config file under %APPDATA% is user data and is deliberately left behind on uninstall.
en.AutoStartTask=Start ScreenSwitch automatically with Windows
es.AutoStartTask=Iniciar ScreenSwitch automáticamente con Windows
fr.AutoStartTask=Lancer ScreenSwitch automatiquement avec Windows
pt.AutoStartTask=Iniciar o ScreenSwitch automaticamente com o Windows
he.AutoStartTask=הפעל את ScreenSwitch אוטומטית עם Windows
ja.AutoStartTask=Windows と一緒に ScreenSwitch を自動起動する
#if FileExists(AddBackslash(SourcePath) + "languages\ChineseSimplified.isl")
zh-Hans.AutoStartTask=随 Windows 自动启动 ScreenSwitch
#endif
#if FileExists(AddBackslash(SourcePath) + "languages\Arabic.isl")
ar.AutoStartTask=تشغيل ScreenSwitch تلقائياً مع Windows
#endif
