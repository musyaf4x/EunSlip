# About View Crash — TDD Evidence

## User journey

As an EunSlip operator, I can open **About** from the sidebar without the
application terminating, so product and diagnostic information remains
accessible.

## Root cause

WPF's default binding mode for `Run.Text` attempted to write back to the
read-only `AboutViewModel.Version` property while the About view was laid out.
The same risk applied to the read-only `Company` and `LogFolder` properties.

## RED / GREEN evidence

| Stage | Command or check | Result |
|---|---|---|
| RED | `dotnet test tests\EunSlip.Desktop.Tests\EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~ViewSmokeTests` | Failed with `InvalidOperationException`: TwoWay binding cannot work on read-only `Version`. |
| GREEN | Same scoped test after adding `Mode=OneWay` to the three About `Run.Text` bindings | Passed: 1/1. |
| Desktop E2E | Launch app, invoke `NavAbout` through Windows UI Automation, wait for the About heading, and assert the process remains alive | Passed: About rendered and process remained alive. |
| Build | `dotnet build EunSlip.slnx --no-restore` | Passed: 0 warnings, 0 errors. |
| Regression | `dotnet test EunSlip.slnx --no-build --no-restore` | Passed: 252/252. |

## Guarantee

`ViewSmokeTests` now hosts `AboutView` with its real view model in a visible WPF
window and advances the dispatcher through layout. This exercises the binding
attachment path that previously crashed only after navigating to About.

## Checkpoints

- `ec85967` — RED reproducer.
- `61fb3cf` — minimal GREEN fix.
