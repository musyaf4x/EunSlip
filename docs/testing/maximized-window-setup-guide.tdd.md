# Maximized Window and Setup Guide — TDD Evidence

## Source

- Design: `docs/superpowers/specs/2026-07-22-maximized-window-setup-guide-design.md`
- Plan: `docs/superpowers/plans/2026-07-22-maximized-window-setup-guide.md`

## User Journey

As an EunSlip operator, I want the app to open maximized and retain the complete setup tutorial so that the working area is immediately usable and Gmail OAuth setup remains self-guided.

## RED / GREEN Evidence

| Guarantee | Test | RED | GREEN |
|---|---|---|---|
| Main window declares maximized startup | `ThemeContractTests.MainWindow_StartsMaximized` | Failed: `WindowState="Maximized"` absent | Passed after XAML change |
| Settings retains the seven-step Google Cloud/OAuth tutorial | `ThemeContractTests.SettingsView_KeepsCompleteGoogleCloudSetupTutorial` | Failed: full guide header/instructions absent | Passed after tutorial restoration |

Targeted command:

`dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~ThemeContractTests"`

- RED: 2 failed, 3 passed.
- GREEN: 5 passed, 0 failed.

## Final Verification

- `dotnet build EunSlip.slnx --no-restore --verbosity minimal`: succeeded with 0 warnings and 0 errors.
- `dotnet test EunSlip.slnx --no-build --logger "console;verbosity=minimal"`: 243 passed, 0 failed, 0 skipped.
- Existing compiled-view smoke coverage confirms the updated Settings XAML constructs successfully.

## Known Gaps

No new dependency or business-logic path was introduced. A separate live UI session was not forced because another EunSlip instance owned by the main checkout was already open; the startup state is protected by a XAML contract and the view by compiled smoke coverage.
