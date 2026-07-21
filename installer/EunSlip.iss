; EunSlip installer — Inno Setup skeleton (spec §14).
; Compile with ISCC once Inno Setup 6 is installed (prerequisite for TASK-015).
; Requires: publish output at src\EunSlip.Desktop\bin\Release\net10.0-windows\win-x64\publish
;   dotnet publish src\EunSlip.Desktop -c Release -r win-x64 --self-contained

#define AppName "EunSlip"
#define AppVersion "1.0.0"
#define AppPublisher "PT. EUNSUNG INDONESIA"
#define AppExeName "EunSlip.Desktop.exe"
#define PublishDir "..\src\EunSlip.Desktop\bin\Release\net10.0-windows\win-x64\publish"

[Setup]
AppId={{A56A07F0-3CFA-4DDC-AE5B-0F298EE5B609}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\EunSlip
DefaultGroupName={#AppName}
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=EunSlip-Setup-x64
Compression=lzma2
SolidCompression=yes
; Unsigned in v1 by decision (spec §23 risk 6): SmartScreen/AV may require IT approval.
; Do NOT add flags that bypass or weaken Windows security features.

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
; Shared data root lives outside program files so upgrades/uninstall preserve it.
; ACL: users-modify across all subdirs. This is the spec-accepted (§13/§23 risk 4)
; machine-shared security scope — the trusted-accounting-computer assumption.
; Compensating controls: AES-GCM + DPAPI(LocalMachine) encrypt NIK/email/tokens
; at rest, so users-modify grants filesystem access but not plaintext.
Name: "{commonappdata}\EunSlip"; Permissions: users-modify
Name: "{commonappdata}\EunSlip\database"; Permissions: users-modify
Name: "{commonappdata}\EunSlip\stamp"; Permissions: users-modify
Name: "{commonappdata}\EunSlip\oauth"; Permissions: users-modify
Name: "{commonappdata}\EunSlip\temp"; Permissions: users-modify
Name: "{commonappdata}\EunSlip\logs"; Permissions: users-modify
Name: "{commonappdata}\EunSlip\runtime"; Permissions: users-modify

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[UninstallDelete]
; Binaries only. {commonappdata}\EunSlip (database, Gmail token, stamp, settings, logs)
; is intentionally preserved per spec §14; permanent data removal is a separate IT action.
Type: filesandordirs; Name: "{app}"
