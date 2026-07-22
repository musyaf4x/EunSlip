# English UI Consistency - TDD Evidence

## Source

No separate plan file was provided. The user journey and guarantees were derived from the reported English interface inconsistency.

## User Journey

As an EunSlip operator using English, I want every page, dialog, status message, and accessibility label to use English so the interface does not switch languages unexpectedly.

## RED / GREEN Evidence

| Guarantee | Test | RED | GREEN |
|---|---|---|---|
| User-facing XAML uses bindings or localization resources | `LocalizationContractTests.UserFacingXaml_UsesBindingsOrLocalizedMarkup` | Failed on hardcoded copy across the shell and all five pages | Passed after XAML copy moved to bilingual resources |
| Code-behind and ViewModels do not expose known Indonesian UI literals in English mode | `LocalizationContractTests.CodeBehindAndViewModels_DoNotContainKnownIndonesianUiCopy` | Failed on file dialogs, settings/history statuses, email defaults, About copy, and the sending close dialog | Passed after all paths used `Strings.Get` |
| XAML can resolve copy for the selected UI culture | `LocalizationContractTests.LocExtension_ResolvesCopyForCurrentUiCulture` | Failed because no XAML localization extension existed | Passed with `LocExtension` resolving `en-US` copy |
| Default and English resources remain structurally complete | `LocalizationContractTests.LocalizationResources_HaveMatchingKeys` and `AllStaticLocalizationReferences_HaveResources` | Resource parity passed initially; static-reference coverage was added during GREEN hardening | Passed with matching key sets and no unresolved static references |
| Compiled views and setup tutorial remain intact | `ViewSmokeTests.AllViews_ConstructWithCompiledThemeOnStaThread` and `ThemeContractTests.SettingsView_KeepsCompleteGoogleCloudSetupTutorial` | Legacy tutorial assertion initially expected Indonesian XAML literals | Passed while retaining all seven setup steps in both languages |

Initial valid RED command:

`dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --filter "FullyQualifiedName~LocalizationContractTests" --no-restore`

- RED: 3 failed, 2 passed.
- Additional close-dialog RED: 1 failed, 0 passed.

Targeted GREEN command:

`dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --filter "FullyQualifiedName~LocalizationContractTests|FullyQualifiedName~SettingsView_KeepsCompleteGoogleCloudSetupTutorial|FullyQualifiedName~AllViews_ConstructWithCompiledThemeOnStaThread" --no-restore`

- GREEN: 8 passed, 0 failed.

## Final Verification

- `dotnet build EunSlip.slnx --no-restore`: succeeded with 0 warnings and 0 errors.
- `dotnet test EunSlip.slnx --no-build --no-restore`: 252 passed, 0 failed, 0 skipped.
- Counts: Core 85, Infrastructure 93, Desktop 74.
- A final source audit found no known Indonesian UI terms outside the localization resource files.

## Known Gap

A second live EunSlip instance was not forced during this run because the application uses a single-instance lock. Compiled-view smoke coverage validates every updated XAML surface; a user can perform the final visual language review after restarting with `en-US` selected.
