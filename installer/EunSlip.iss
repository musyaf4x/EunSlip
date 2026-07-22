; EunSlip production installer (TASK-015).
; Build through scripts\Build-Installer.ps1 so publish and compiler inputs stay reproducible.

#define AppName "EunSlip"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "PT. EUNSUNG INDONESIA"
#define AppExeName "EunSlip.exe"
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif
#ifndef InstallerPrivileges
  #define InstallerPrivileges "admin"
#endif
#ifndef SharedDataRoot
  #define SharedDataRoot "{commonappdata}\EunSlip"
#endif
#ifndef InstallerBaseName
  #define InstallerBaseName "EunSlip-Setup-x64"
#endif

[Setup]
AppId={{A56A07F0-3CFA-4DDC-AE5B-0F298EE5B609}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\EunSlip
DefaultGroupName={#AppName}
PrivilegesRequired={#InstallerPrivileges}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename={#InstallerBaseName}
SetupIconFile=..\src\EunSlip.Desktop\Assets\eunslip.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
; Version 1 is intentionally unsigned. SmartScreen/AV approval belongs to IT.
; Never weaken or bypass Windows security controls in this installer.

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
; Shared operational state remains outside Program Files so upgrade and uninstall preserve it.
Name: "{#SharedDataRoot}"; Permissions: users-modify
Name: "{#SharedDataRoot}\database"; Permissions: users-modify
Name: "{#SharedDataRoot}\stamp"; Permissions: users-modify
Name: "{#SharedDataRoot}\oauth"; Permissions: users-modify
Name: "{#SharedDataRoot}\temp"; Permissions: users-modify
Name: "{#SharedDataRoot}\logs"; Permissions: users-modify
Name: "{#SharedDataRoot}\runtime"; Permissions: users-modify

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch EunSlip"; Flags: postinstall nowait skipifsilent unchecked

; Do not add {#SharedDataRoot} to [UninstallDelete]. Inno removes installed binaries
; from {app}; ProgramData history, OAuth token, stamp, settings, and logs are retained.
