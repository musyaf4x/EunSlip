# EunSlip Logo Branding - TDD Evidence

## User Journey

As an EunSlip operator, I want consistent product branding in the application and Windows executable so the app is recognizable in both the UI and desktop shell.

## RED / GREEN Evidence

| Guarantee | Test | RED | GREEN |
|---|---|---|---|
| Sidebar and About use the supplied black EunSlip logo | `ThemeContractTests.Branding_UsesSuppliedEunSlipLogoInShellAndAbout` | Failed before logo resources and UI references existed | Passed after the exact source logo was packaged in `Assets` |
| Executable uses a multi-size icon derived from `eunslip-logo-bg-01.png` | `ThemeContractTests.DesktopExecutable_UsesMultiSizeIconDerivedFromBackgroundLogo` | Failed before `ApplicationIcon` and the generated ICO existed | Passed with a six-size ICO and matching window artwork |
| Logo resources resolve when compiled XAML constructs the window | `ViewSmokeTests.AllViews_ConstructWithCompiledThemeOnStaThread` | Failed when linked and root-relative resources could not be located | Passed after packaging physical resources and using assembly-qualified pack URIs |

Targeted verification:

`dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --filter "FullyQualifiedName~Branding_UsesSuppliedEunSlipLogoInShellAndAbout|FullyQualifiedName~DesktopExecutable_UsesMultiSizeIconDerivedFromBackgroundLogo|FullyQualifiedName~AllViews_ConstructWithCompiledThemeOnStaThread" --no-restore`

- GREEN: 3 passed, 0 failed.

## Final Verification

- `dotnet build EunSlip.slnx --no-restore`: succeeded with 0 warnings and 0 errors.
- `dotnet test EunSlip.slnx --no-build --no-restore`: 245 passed, 0 failed, 0 skipped.
- Counts: Core 85, Infrastructure 93, Desktop 67.

## Implementation Notes

- The supplied black transparent logo is used in the sidebar and About identity card.
- `eunslip-logo-bg-01.png` is the source artwork for both the WPF window icon and the generated Windows executable icon.
- The ICO contains 16, 32, 48, 64, 128, and 256 pixel variants.
