# TASK-015 Installer and Operational Docs — TDD Evidence

## Source

Acceptance criteria were taken from `EunSlip_Coding_Agent_Context.md` TASK-015:
produce the `EunSlip-Setup-x64.exe` build path, Google Cloud setup guide,
deployment guide, and verify clean install, upgrade, and uninstall preservation.

## User journeys

1. As IT, I can produce a versioned self-contained win-x64 installer with one
   command and record its SHA-256.
2. As an administrator, I can install or upgrade EunSlip without losing shared
   history, OAuth, stamp, settings, or logs.
3. As an operator, I can uninstall application binaries while shared data is
   intentionally retained.
4. As a Google Workspace administrator, I can configure the minimum OAuth
   scopes and provision the desktop client without committing credentials.

## RED / GREEN history

- The initial contract tests were written before the scripts and guides. Their
  runtime RED was not executed because the referenced files did not exist, so
  failure was certain; the test project build passed with 0 warnings/errors.
- First GREEN run: `InstallerContractTests` passed 5/5 after adding the
  installer, scripts, release metadata, and guides.
- First real build exposed an unhandled per-user Inno Setup path. The resolver
  was extended to `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`.
- First lifecycle run exposed compiler output contaminating a function return
  value. The harness now separates diagnostic output from the setup path.
- Changing WPF assembly/target identity to obtain `EunSlip.exe` broke pack URI
  resolution. The final build preserves the `EunSlip.Desktop` assembly identity
  and creates a tested `EunSlip.exe` publish alias with matching deps/runtime
  configuration files.

## Verification results

| Guarantee | Command/check | Result |
|---|---|---|
| Contract requirements are present | `dotnet test ... --filter FullyQualifiedName~InstallerContractTests` | PASS, 5/5 |
| Release build compiles cleanly | `dotnet build EunSlip.slnx --no-restore` | PASS, 0 warnings, 0 errors |
| All automated tests remain green | `dotnet test EunSlip.slnx --no-build --no-restore` | PASS, 257/257 |
| Production publish is self-contained win-x64 | `scripts\Build-Installer.ps1 -Version 1.0.0` | PASS; `coreclr.dll`, `hostfxr.dll`, and runtime config present |
| Required setup artifact is produced | `installer\output\EunSlip-Setup-x64.exe` | PASS; 52,675,425 bytes; version 1.0.0 |
| Artifact integrity is recordable | SHA-256 | `EE1CE1DD6BDBD61B0FC1237214C391517FB311B156BD2427A15D5E255C59E2A2` |
| Signing decision is explicit | Authenticode inspection | `NotSigned`, expected for v1 |
| Clean install succeeds | `scripts\Test-InstallerLifecycle.ps1` | PASS |
| Upgrade 1.0.0 to 1.0.1 preserves data markers | Same lifecycle test | PASS |
| Uninstall removes binaries | Same lifecycle test | PASS |
| Uninstall preserves database/OAuth/stamp markers | Same lifecycle test | PASS |
| Published release executable starts | Windows UI Automation smoke test of `EunSlip.exe` | PASS; About rendered and process remained alive |
| Dependencies have no known advisory | `dotnet list EunSlip.slnx package --vulnerable --include-transitive` | PASS; no vulnerable packages reported |

## Security and operational review

- Production installer remains `PrivilegesRequired=admin`; normal application
  execution is not elevated.
- Sandbox lifecycle builds override privileges and shared paths only at compile
  time, and never touch the real `C:\ProgramData\EunSlip`.
- Recursive cleanup validates the resolved target remains below `artifacts` or
  the specifically named TEMP sandbox.
- Installer output, publish output, OAuth files, tokens, and credentials are not
  tracked by Git.
- Documentation tells IT to verify unsigned artifacts rather than disabling
  SmartScreen or antivirus.
- OAuth client JSON is provisioned after installation and protected by the
  application's existing DPAPI flow; it is not embedded in the installer.

## Known environment boundary

The lifecycle test executes real Inno Setup installers in a non-admin TEMP
sandbox to avoid mutating the developer machine's production ProgramData. The
production `admin` directive and ACL contract are statically verified. A clean
Windows 10/11 VM without preinstalled .NET remains an environment-level release
check for TASK-016 UAT; the self-contained runtime contents are verified here.

## Checkpoints

- `9cf3eb7` — initial packaging contracts.
- `49e7acf` — release executable metadata contract.
- `a76c143` — installer workflow and operational guides.
- `da1e095` — compiler discovery and lifecycle harness hardening.
- `6975c7b` — WPF-safe release executable packaging.
