# TASK-016 UAT Release Candidate — Verification Evidence

## Scope and decision

Acceptance criteria were taken from `EunSlip_Coding_Agent_Context.md` TASK-016:
the full automated suite, a 500-row baseline, one real Gmail recipient, and a
self-contained internal-UAT release build. The release candidate is **approved
for internal UAT** on 22 July 2026. The installer remains Authenticode unsigned;
"approved" here is operational sign-off, not a digital-signature claim.

Only synthetic data was used. The Gmail gate resolves the authenticated
owner's address at runtime and never writes it, credentials, OAuth tokens, or a
message ID to source or test output.

## TDD history

| Stage | Evidence | Result |
|---|---|---|
| Baseline acceptance test | `ReleaseCandidateBaselineTests` | GREEN: exactly 500 rows imported and validated off-thread; 500 PDFs generated, opened as one page, and deleted serially; peak temporary PDF count was one |
| Gmail identity RED | `GmailAuthorizationTests` initially referenced the missing profile resolver | Expected compile-time RED (`ResolveAccountAsync` absent) |
| Gmail identity GREEN | Connected identity was first resolved through Gmail `GetProfile` | Unit tests GREEN, but real UAT exposed `403 insufficient authentication scopes` |
| OpenID scope RED | Added `ParseUserInfoEmail` contract before implementation | Expected compile-time RED (`ParseUserInfoEmail` absent) |
| OpenID scope GREEN | Identity now comes from OpenID UserInfo using the existing `openid email` scopes | 4/4 scoped tests GREEN; no Gmail read scope added |
| Real Gmail UAT | `RealGmailUatTests` with `EUNSLIP_RUN_REAL_GMAIL_UAT=1` | GREEN: one dummy PDF accepted by Gmail API for the authenticated owner's account in 9 seconds; minimal history recorded and temporary PDF removed |

## Release gate results

| Guarantee | Command/check | Result |
|---|---|---|
| All automated tests remain green | `dotnet test EunSlip.slnx --configuration Release --no-restore` | PASS, 263/263: Core 85, Infrastructure 99, Desktop 79 |
| 500-row baseline completes with bounded temporary files | `dotnet test ... --filter FullyQualifiedName~ReleaseCandidateBaselineTests` | PASS, 500 valid rows and 500 one-page PDFs in 21 seconds; no PDF remained |
| Release publish is self-contained win-x64 | `scripts\Build-Installer.ps1` | PASS; `coreclr.dll` and `hostfxr.dll` present |
| Installer artifact is reproducible | `installer\output\EunSlip-Setup-x64.exe` | PASS; version 1.0.0; 52,687,830 bytes; SHA-256 `042B297FCB87B170E31A32CBBC59FCFBBFE97836F712FA1151ED95A97E773E36` |
| Installer lifecycle is safe | `scripts\Test-InstallerLifecycle.ps1` | PASS: clean install, 1.0.0→1.0.1 upgrade, binary removal, and shared-data preservation |
| Published desktop executable starts correctly | Windows UI Automation against published `EunSlip.exe` | PASS: visible maximized window, About rendered, process remained alive; observed bounds 1938×1098 |
| One real Gmail recipient succeeds | Opt-in `RealGmailUatTests` | PASS: one dummy attachment sent to the authenticated owner account; no production payroll data |
| Dependencies have no known advisory | `dotnet list EunSlip.slnx package --vulnerable --include-transitive` | PASS: no vulnerable direct or transitive packages reported |
| Sensitive runtime artifacts are not tracked | tracked-file scan for OAuth/client-secret/token/database/signing artifacts | PASS |

## Manual observation boundary

Gmail returned a successful send result. A read-only inbox check was attempted,
but the available browser was not signed in; receipt rendering and any Gmail
send-as display-name override could therefore not be observed automatically.
The operator should confirm the received message, visible sender, attachment
name, PDF readability, and stamp in the inbox before production payroll use.
This does not require or justify adding Gmail read scope to EunSlip.

The current x64 host has a Windows build numbered 26200 and already has the
.NET desktop runtime. Windows Sandbox was unavailable, so a separate clean
Windows 10/11 VM without .NET was not run here. Self-contained runtime contents
and real installer lifecycle were verified; IT should retain its normal clean-VM
compatibility check when promoting beyond internal UAT.

## Checkpoints

- `beddb52` — executable 500-row release baseline.
- `7f239a1` / `2ed4a3c` — Gmail identity RED/GREEN.
- `685b8c2` / `f6ca0b2` — OpenID identity RED/GREEN after the real-scope failure.
- `6914099` — opt-in one-recipient Gmail UAT gate.
