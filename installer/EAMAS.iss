; EAMAS — Employee Activity Monitoring & Analytics System
; Inno Setup 6 installer script

#define AppName      "EAMAS"
#define AppFullName  "Employee Activity Monitoring && Analytics System"
#define AppVersion   "1.0.0"
#define AppPublisher "EAMAS"
#define AppExeName   "EAMAS.exe"
#define AppId        "{{3F8A2B1C-D4E5-4F67-8901-ABCDEF234567}"

[Setup]
AppId={#AppId}
AppName={#AppFullName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=
AppSupportURL=
AppUpdatesURL=

; Install into Program Files\EAMAS by default; allow user to change
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}

; Output
OutputDir=.
OutputBaseFilename=EAMAS-Setup-{#AppVersion}

; Visuals
SetupIconFile=..\assets\EAMAS.ico
UninstallDisplayIcon={app}\{#AppExeName}
WizardStyle=modern
WizardSizePercent=100

; Compression – lzma2 ultra gives smallest file
Compression=lzma2/ultra64
SolidCompression=yes

; Require Windows 10 1809+ (needed for .NET 8)
MinVersion=10.0.17763

; Need admin to write to Program Files
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Close any running EAMAS instance before upgrade
CloseApplications=yes
CloseApplicationsFilter={#AppExeName}

; Show "Launch EAMAS" checkbox at the end
DisableProgramGroupPage=yes
AllowNoIcons=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ── Optional tasks the user can tick/untick ─────────────────────────────────

[Tasks]
Name: "desktopicon"; \
  Description: "Create a &desktop shortcut"; \
  GroupDescription: "Additional shortcuts:"; \
  Flags: unchecked

Name: "startupitem"; \
  Description: "Start EAMAS automatically when &Windows starts (runs in system tray)"; \
  GroupDescription: "Startup:"; \
  Flags: unchecked

; ── Files to install ────────────────────────────────────────────────────────

[Files]
; Single self-contained executable — no .NET runtime required on target machine
Source: "..\build\publish\{#AppExeName}"; \
  DestDir: "{app}"; \
  Flags: ignoreversion

; ── Shortcuts ───────────────────────────────────────────────────────────────

[Icons]
; Start Menu
Name: "{group}\{#AppFullName}"; \
  Filename: "{app}\{#AppExeName}"; \
  Comment: "Launch EAMAS – Employee Activity Monitoring"

; Uninstall entry in Start Menu
Name: "{group}\Uninstall {#AppName}"; \
  Filename: "{uninstallexe}"

; Desktop (optional task)
Name: "{autodesktop}\{#AppName}"; \
  Filename: "{app}\{#AppExeName}"; \
  Comment: "Launch EAMAS – Employee Activity Monitoring"; \
  Tasks: desktopicon

; ── Registry ────────────────────────────────────────────────────────────────

[Registry]
; Windows startup entry (optional task) — system-wide so every user gets EAMAS on login
Root: HKLM; \
  Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; \
  ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExeName}"""; \
  Flags: uninsdeletevalue; \
  Tasks: startupitem

; ── Run after install ───────────────────────────────────────────────────────

[Run]
Filename: "{app}\{#AppExeName}"; \
  Description: "Launch {#AppFullName}"; \
  Flags: nowait postinstall skipifsilent

; ── Uninstall: close the app first if running ───────────────────────────────

[UninstallRun]
Filename: "taskkill.exe"; \
  Parameters: "/F /IM {#AppExeName}"; \
  Flags: skipifdoesntexist runhidden; \
  RunOnceId: "KillEAMAS"

; ── Custom messages ─────────────────────────────────────────────────────────

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nEAMAS monitors employee application usage and takes periodic screenshots for productivity tracking.%n%nIt is recommended that you close all other applications before continuing.
