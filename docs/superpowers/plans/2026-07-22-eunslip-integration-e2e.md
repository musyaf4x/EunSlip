# EunSlip Redesign Integration, Accessibility, and E2E Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate the redesigned pages and workflow into a consistently localized, automatable, accessibility-conscious release candidate and prove it through full build, tests, PDF review, and safe desktop E2E.

**Architecture:** Keep localization at the presentation boundary through resource keys and one enum-status converter. Add stable WPF Automation IDs without changing behavior, then use a single STA smoke test to catch XAML/resource construction failures. Final verification exercises the real desktop executable with dummy payroll data but stops before any external Gmail send or destructive action.

**Tech Stack:** .NET 10, WPF UI Automation properties, CommunityToolkit.Mvvm, xUnit, Windows Computer Use, PDFsharp, Poppler `pdftoppm`.

## Global Constraints

- Execute after the shell/pages, workflow/history, and PDF redesign plans are complete and green.
- No new application dependency is permitted in this plan.
- Every status visible in Indonesian UI must be localized; raw enum and Boolean names are prohibited.
- Stable Automation IDs are a compatibility contract and must not depend on localized display text.
- Keyboard focus must remain visible and follow reading order.
- Desktop E2E uses dummy payroll data only and must stop before `KONFIRMASI & KIRIM` unless a fake/non-network coordinator is in use.
- Do not connect/disconnect Gmail, alter OAuth credentials, delete stamp/history, publish, push, package, or deploy during this plan.
- TASK 15 installer work and TASK 16 real-Gmail UAT remain outside this plan.
- Completion claims require fresh command output from the full suite and build.

## File Map

### Create

- `src/EunSlip.Desktop/Converters/StatusTextConverter.cs` — localized Batch/Recipient/Attempt/Run mode display.
- `tests/EunSlip.Desktop.Tests/StatusTextConverterTests.cs` — status localization contract.
- `tests/EunSlip.Desktop.Tests/ViewSmokeTests.cs` — STA construction test for all views and MainWindow resources.
- `docs/qa/2026-07-22-eunslip-v1-redesign-verification.md` — final commands, results, E2E matrix, and PDF evidence links.

### Modify

- `src/EunSlip.Desktop/Localization/Strings.resx` — Indonesian enum/status copy.
- `src/EunSlip.Desktop/Localization/Strings.en.resx` — English enum/status copy.
- `src/EunSlip.Desktop/ViewModels/HomeViewModel.cs` — localized batch summary.
- `src/EunSlip.Desktop/MainWindow.xaml` — navigation Automation IDs.
- `src/EunSlip.Desktop/Views/HomeView.xaml` — Home Automation IDs.
- `src/EunSlip.Desktop/Views/WizardView.xaml` — Wizard Automation IDs and localized status converter use.
- `src/EunSlip.Desktop/Views/HistoryView.xaml` — History Automation IDs and localized status converter use.
- `src/EunSlip.Desktop/Views/SettingsView.xaml` — Settings Automation IDs.
- `tests/EunSlip.Desktop.Tests/ThemeContractTests.cs` — Automation ID structural contract.
- `tests/EunSlip.Desktop.Tests/WorkflowViewContractTests.cs` — Wizard/History Automation ID contract.

---

### Task 1: Localize Every Operational Status

**Files:**
- Create: `src/EunSlip.Desktop/Converters/StatusTextConverter.cs`
- Create: `tests/EunSlip.Desktop.Tests/StatusTextConverterTests.cs`
- Modify: `src/EunSlip.Desktop/Localization/Strings.resx`
- Modify: `src/EunSlip.Desktop/Localization/Strings.en.resx`
- Modify: `src/EunSlip.Desktop/ViewModels/HomeViewModel.cs`
- Modify: `src/EunSlip.Desktop/Views/HistoryView.xaml`
- Modify: `src/EunSlip.Desktop/Views/WizardView.xaml`

**Interfaces:**
- Consumes: `BatchStatus`, `RecipientStatus`, `AttemptStatus`, `AttemptType`, `PayrollRunMode`, and `Strings.Get`.
- Produces: `StatusTextConverter.Convert` and resource keys named `<EnumType>_<EnumValue>`.

- [ ] **Step 1: Write failing converter tests**

```csharp
using System.Globalization;
using EunSlip.Core.Persistence;
using EunSlip.Desktop.Converters;
using EunSlip.Desktop.Localization;
using EunSlip.Desktop.ViewModels;

namespace EunSlip.Desktop.Tests;

public sealed class StatusTextConverterTests
{
    private readonly StatusTextConverter _converter = new();

    [Theory]
    [InlineData(BatchStatus.Interrupted, "BatchStatus_Interrupted")]
    [InlineData(RecipientStatus.Failed, "RecipientStatus_Failed")]
    [InlineData(AttemptStatus.Sent, "AttemptStatus_Sent")]
    [InlineData(AttemptType.RecoveryRetry, "AttemptType_RecoveryRetry")]
    [InlineData(PayrollRunMode.FailedRetry, "PayrollRunMode_FailedRetry")]
    public void Convert_UsesLocalizedResourceKey(object status, string key)
    {
        object result = _converter.Convert(status, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(Strings.Get(key), result);
        Assert.NotEqual(status.ToString(), result);
    }

    [Fact]
    public void Convert_NullUsesEmDash()
    {
        Assert.Equal("—", _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
    }
}
```

- [ ] **Step 2: Run converter tests and verify the converter is absent**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~StatusTextConverterTests
```

Expected: build FAIL because `StatusTextConverter` and status resources do not exist.

- [ ] **Step 3: Implement the converter**

```csharp
using System.Globalization;
using System.Windows.Data;
using EunSlip.Core.Persistence;
using EunSlip.Desktop.Localization;
using EunSlip.Desktop.ViewModels;

namespace EunSlip.Desktop.Converters;

public sealed class StatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? key = value switch
        {
            BatchStatus status => $"BatchStatus_{status}",
            RecipientStatus status => $"RecipientStatus_{status}",
            AttemptStatus status => $"AttemptStatus_{status}",
            AttemptType status => $"AttemptType_{status}",
            PayrollRunMode status => $"PayrollRunMode_{status}",
            null => null,
            _ => throw new NotSupportedException($"Unsupported status type: {value.GetType().FullName}"),
        };

        return key is null ? "—" : Strings.Get(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 4: Add complete Indonesian and English status resources**

Add keys for every enum member:

```xml
<data name="BatchStatus_Draft" xml:space="preserve"><value>Draf</value></data>
<data name="BatchStatus_Ready" xml:space="preserve"><value>Siap</value></data>
<data name="BatchStatus_Sending" xml:space="preserve"><value>Mengirim</value></data>
<data name="BatchStatus_Completed" xml:space="preserve"><value>Selesai</value></data>
<data name="BatchStatus_Interrupted" xml:space="preserve"><value>Terputus</value></data>
<data name="RecipientStatus_Pending" xml:space="preserve"><value>Menunggu</value></data>
<data name="RecipientStatus_Sending" xml:space="preserve"><value>Mengirim</value></data>
<data name="RecipientStatus_Sent" xml:space="preserve"><value>Terkirim</value></data>
<data name="RecipientStatus_Failed" xml:space="preserve"><value>Gagal</value></data>
<data name="AttemptStatus_Pending" xml:space="preserve"><value>Menunggu</value></data>
<data name="AttemptStatus_Sent" xml:space="preserve"><value>Terkirim</value></data>
<data name="AttemptStatus_Failed" xml:space="preserve"><value>Gagal</value></data>
<data name="AttemptType_Normal" xml:space="preserve"><value>Normal</value></data>
<data name="AttemptType_FailedRetry" xml:space="preserve"><value>Kirim ulang gagal</value></data>
<data name="AttemptType_RecoveryRetry" xml:space="preserve"><value>Pemulihan</value></data>
<data name="PayrollRunMode_Normal" xml:space="preserve"><value>Proses normal</value></data>
<data name="PayrollRunMode_FailedRetry" xml:space="preserve"><value>Kirim ulang gagal</value></data>
<data name="PayrollRunMode_RecoveryRetry" xml:space="preserve"><value>Pemulihan batch</value></data>
```

Add the same keys to the English file with these exact values:

```xml
<data name="BatchStatus_Draft" xml:space="preserve"><value>Draft</value></data>
<data name="BatchStatus_Ready" xml:space="preserve"><value>Ready</value></data>
<data name="BatchStatus_Sending" xml:space="preserve"><value>Sending</value></data>
<data name="BatchStatus_Completed" xml:space="preserve"><value>Completed</value></data>
<data name="BatchStatus_Interrupted" xml:space="preserve"><value>Interrupted</value></data>
<data name="RecipientStatus_Pending" xml:space="preserve"><value>Pending</value></data>
<data name="RecipientStatus_Sending" xml:space="preserve"><value>Sending</value></data>
<data name="RecipientStatus_Sent" xml:space="preserve"><value>Sent</value></data>
<data name="RecipientStatus_Failed" xml:space="preserve"><value>Failed</value></data>
<data name="AttemptStatus_Pending" xml:space="preserve"><value>Pending</value></data>
<data name="AttemptStatus_Sent" xml:space="preserve"><value>Sent</value></data>
<data name="AttemptStatus_Failed" xml:space="preserve"><value>Failed</value></data>
<data name="AttemptType_Normal" xml:space="preserve"><value>Normal</value></data>
<data name="AttemptType_FailedRetry" xml:space="preserve"><value>Failed retry</value></data>
<data name="AttemptType_RecoveryRetry" xml:space="preserve"><value>Recovery retry</value></data>
<data name="PayrollRunMode_Normal" xml:space="preserve"><value>Normal process</value></data>
<data name="PayrollRunMode_FailedRetry" xml:space="preserve"><value>Failed retry</value></data>
<data name="PayrollRunMode_RecoveryRetry" xml:space="preserve"><value>Batch recovery</value></data>
```

- [ ] **Step 5: Consume localized statuses**

Register the converter in History/Wizard resources:

```xml
<c:StatusTextConverter x:Key="StatusText" />
```

Use it for batch, recipient, attempt type, and run-mode bindings:

```xml
<DataGridTextColumn Header="STATUS" Binding="{Binding Status, Converter={StaticResource StatusText}}" Width="100" />
<DataGridTextColumn Header="TIPE" Binding="{Binding LatestAttemptType, Converter={StaticResource StatusText}}" Width="120" />
```

Change Home's recent summary to:

```csharp
string statusText = Strings.Get($"BatchStatus_{latest.Status}");
RecentBatchSummary = $"{latest.Period} · {statusText} · {latest.SentCount}/{latest.RecipientCount}";
```

- [ ] **Step 6: Run localization tests and desktop build**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~StatusTextConverterTests|FullyQualifiedName~HomeViewModelTests|FullyQualifiedName~HistoryViewModelTests"
dotnet build src/EunSlip.Desktop/EunSlip.Desktop.csproj --no-restore
```

Expected: tests PASS; build succeeds with 0 warnings and 0 errors.

- [ ] **Step 7: Commit localized operational state**

```powershell
git add src/EunSlip.Desktop/Converters/StatusTextConverter.cs src/EunSlip.Desktop/Localization src/EunSlip.Desktop/ViewModels/HomeViewModel.cs src/EunSlip.Desktop/Views/HistoryView.xaml src/EunSlip.Desktop/Views/WizardView.xaml tests/EunSlip.Desktop.Tests/StatusTextConverterTests.cs
git commit -m "ui: localize operational statuses"
```

---

### Task 2: Establish Stable Automation IDs

**Files:**
- Modify: `src/EunSlip.Desktop/MainWindow.xaml`
- Modify: `src/EunSlip.Desktop/Views/HomeView.xaml`
- Modify: `src/EunSlip.Desktop/Views/WizardView.xaml`
- Modify: `src/EunSlip.Desktop/Views/HistoryView.xaml`
- Modify: `src/EunSlip.Desktop/Views/SettingsView.xaml`
- Modify: `tests/EunSlip.Desktop.Tests/ThemeContractTests.cs`
- Modify: `tests/EunSlip.Desktop.Tests/WorkflowViewContractTests.cs`

**Interfaces:**
- Consumes: WPF `AutomationProperties.AutomationId`.
- Produces: stable identifiers for navigation, payroll, History, and Settings actions.

- [ ] **Step 1: Add failing structural Automation ID tests**

Add this helper/test to `ThemeContractTests`:

```csharp
[Fact]
public void ShellAndStaticPages_DefineStableAutomationIds()
{
    string combined = string.Join("\n",
        ReadRepositoryFile("src", "EunSlip.Desktop", "MainWindow.xaml"),
        ReadRepositoryFile("src", "EunSlip.Desktop", "Views", "HomeView.xaml"),
        ReadRepositoryFile("src", "EunSlip.Desktop", "Views", "SettingsView.xaml"));

    foreach (string id in new[]
    {
        "NavHome", "NavPayroll", "NavHistory", "NavSettings", "NavAbout",
        "StartPayroll", "ConnectGmail", "DisconnectGmail", "OAuthSecretInput",
        "SaveOAuthSecret", "PickStamp", "RequestRemoveStamp", "LanguageSelector",
    })
    {
        Assert.Contains($"AutomationProperties.AutomationId=\"{id}\"", combined, StringComparison.Ordinal);
    }
}
```

Add to `WorkflowViewContractTests`:

```csharp
[Fact]
public void WorkflowViews_DefineStableAutomationIds()
{
    string combined = string.Join("\n",
        ReadFile("src", "EunSlip.Desktop", "Views", "WizardView.xaml"),
        ReadFile("src", "EunSlip.Desktop", "Views", "HistoryView.xaml"));

    foreach (string id in new[]
    {
        "PayrollFilePath", "PickPayrollFile", "PayrollPeriod", "PaymentDate",
        "ValidationGrid", "PreviewPdf", "ConfirmSend", "WizardBack", "WizardNext", "ResultsGrid",
        "BatchGrid", "RecipientGrid", "RetryFailed", "RecoverInterrupted",
        "RequestDelete", "ConfirmDelete", "CancelDelete",
    })
    {
        Assert.Contains($"AutomationProperties.AutomationId=\"{id}\"", combined, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run contract tests and verify IDs are absent**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~ThemeContractTests|FullyQualifiedName~WorkflowViewContractTests"
```

Expected: FAIL listing the first missing Automation ID.

- [ ] **Step 3: Add exact IDs to the matching controls**

Use the ID from the tests on each control. Examples:

```xml
<Button AutomationProperties.AutomationId="NavPayroll"
        Content="{Binding NavPayrollProcess}" Command="{Binding GoPayrollCommand}" />

<TextBox AutomationProperties.AutomationId="PayrollPeriod"
         Text="{Binding Period, UpdateSourceTrigger=PropertyChanged}"
         Style="{StaticResource FieldInput}" />

<Button AutomationProperties.AutomationId="ConfirmSend"
        Content="KONFIRMASI &amp; KIRIM" Command="{Binding ConfirmSendCommand}"
        Style="{StaticResource PrimaryButton}" />

<DataGrid AutomationProperties.AutomationId="BatchGrid"
          ItemsSource="{Binding Batches}" SelectedItem="{Binding SelectedBatch}" />

<ComboBox AutomationProperties.AutomationId="LanguageSelector"
          SelectedItem="{Binding SelectedLanguage}" ItemsSource="{StaticResource Languages}" />
```

Add `AutomationProperties.Name` when a control's visible text is dynamic or absent. Do not use localized content as the Automation ID.

- [ ] **Step 4: Run structural contracts and desktop build**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~ThemeContractTests|FullyQualifiedName~WorkflowViewContractTests"
dotnet build src/EunSlip.Desktop/EunSlip.Desktop.csproj --no-restore
```

Expected: tests PASS; build succeeds with 0 warnings and 0 errors.

- [ ] **Step 5: Commit automation contracts**

```powershell
git add src/EunSlip.Desktop/MainWindow.xaml src/EunSlip.Desktop/Views tests/EunSlip.Desktop.Tests/ThemeContractTests.cs tests/EunSlip.Desktop.Tests/WorkflowViewContractTests.cs
git commit -m "test: stabilize desktop automation targets"
```

---

### Task 3: Add an STA XAML Construction Smoke Test

**Files:**
- Create: `tests/EunSlip.Desktop.Tests/ViewSmokeTests.cs`

**Interfaces:**
- Consumes: compiled `Theme.xaml`, all five page controls, and `MainWindow`.
- Produces: one deterministic test that catches missing resources, invalid bindings at construction, and malformed XAML.

- [ ] **Step 1: Write the STA construction test**

```csharp
using System.Threading;
using System.Windows;
using EunSlip.Desktop.Views;

namespace EunSlip.Desktop.Tests;

public sealed class ViewSmokeTests
{
    [Fact]
    public void AllViews_ConstructWithCompiledThemeOnStaThread()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                Application application = Application.Current ?? new Application();
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/EunSlip.Desktop;component/Theme.xaml", UriKind.Absolute),
                });

                _ = new HomeView();
                _ = new WizardView();
                _ = new HistoryView();
                _ = new SettingsView();
                _ = new AboutView();
                _ = new EunSlip.Desktop.MainWindow(null!);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "WPF view construction timed out.");
        Assert.Null(failure);
    }
}
```

- [ ] **Step 2: Run the smoke test**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~ViewSmokeTests
```

Expected: PASS. If it fails, the exception identifies the exact resource or XAML construction fault.

- [ ] **Step 3: Fix only demonstrated XAML/resource failures**

For a missing resource, correct the resource key or merge order. For malformed XAML, correct the reported element/attribute. For a MainWindow null-data-context construction error, change the smoke test to instantiate only the five page controls and retain MainWindow coverage through `dotnet build`; do not weaken production constructor invariants.

- [ ] **Step 4: Run the entire Desktop test project**

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore
```

Expected: all Desktop tests PASS; 0 failed.

- [ ] **Step 5: Commit the XAML smoke gate**

```powershell
git add tests/EunSlip.Desktop.Tests/ViewSmokeTests.cs src/EunSlip.Desktop
git commit -m "test: smoke-test compiled WPF views"
```

---

### Task 4: Run the Final Verification Matrix

**Files:**
- Create: `docs/qa/2026-07-22-eunslip-v1-redesign-verification.md`
- Verify: `artifacts/pdf/eunslip-reference-sample.pdf`
- Verify: `artifacts/pdf/eunslip-reference-sample-1.png`

**Interfaces:**
- Consumes: all prior redesign plans.
- Produces: current build/test evidence, desktop E2E findings, and a clear TASK 13/14 readiness decision.

- [ ] **Step 1: Verify repository state before running gates**

```powershell
git status --short
git diff --check
```

Expected: no unexpected/uncommitted changes and no whitespace errors. Preserve unrelated user changes if present and document them rather than modifying them.

- [ ] **Step 2: Run a clean full build**

```powershell
dotnet build EunSlip.slnx --no-restore
```

Expected: build succeeds with 0 warnings and 0 errors.

- [ ] **Step 3: Run the complete automated suite**

```powershell
dotnet test EunSlip.slnx --no-build
```

Expected: all Core, Infrastructure, and Desktop tests PASS; 0 failed.

- [ ] **Step 4: Verify PDF evidence is current**

Regenerate and rasterize using the commands in `2026-07-22-eunslip-payslip-pdf-redesign.md`. Inspect the PNG against `payslip-layout-reference.png`; confirm every PDF acceptance item is still satisfied after integration.

- [ ] **Step 5: Run desktop E2E at baseline viewport**

Launch the Debug executable and use Windows Computer Use. Record PASS/FAIL for:

1. Home operational status, CTA, recent batch, interrupted notice, and active nav.
2. Wizard Select input hierarchy and enabled-state validation.
3. Validate table row height, summary, warnings, and no clipping.
4. Preview generation/opening and email field layout.
5. Confirm readiness matching Settings and enabled confirm button; stop before send.
6. History empty state, selection persistence, detail metadata, action gating, retry entry, and recovery entry.
7. Settings loading → ready transition, two-column layout, disclosure, and non-destructive controls only.
8. About identity, legal copy, diagnostics path, and baseline fit.

- [ ] **Step 6: Run desktop layout checks at minimum size and 150% scale**

At `1024 × 680`, confirm no horizontal overflow, page titles/actions remain reachable, tables do not clip vertically, and Settings uses local scrolling only when necessary. At 150% Windows scaling, repeat Home, Confirm, History detail, and Settings checks. Do not change system scaling through automation; use the user's existing safe display controls or report the scale check as manually unavailable.

- [ ] **Step 7: Verify sending lock with test doubles only**

Use `MainViewModelTests.SendingState_DisablesNavigationAndClose` and the fake coordinator tests as authoritative evidence. Do not initiate a real Gmail send to test the lock.

- [ ] **Step 8: Write the verification report with actual results**

The report must contain this completed structure, replacing each count/date with the observed value and omitting no row:

```markdown
# EunSlip v1 Redesign Verification — 2026-07-22

## Automated gates

| Gate | Result | Evidence |
|---|---|---|
| Full build | PASS | 0 warnings, 0 errors |
| Core tests | PASS | `dotnet test EunSlip.slnx --no-build` |
| Infrastructure tests | PASS | `dotnet test EunSlip.slnx --no-build` |
| Desktop tests | PASS | `dotnet test EunSlip.slnx --no-build` |
| PDF raster review | PASS | `artifacts/pdf/eunslip-reference-sample-1.png` |

## Desktop E2E

| Surface | Result | Notes |
|---|---|---|
| Home | PASS | readiness, CTA, recent/interrupted state, active nav verified |
| Wizard steps 1–4 | PASS | readiness matched Settings; stopped before send |
| History | PASS | selection, detail, gating, retry/recovery entry verified |
| Settings | PASS | loading transition and layout verified; no destructive action |
| About | PASS | identity, legal copy, and diagnostics verified |
| Minimum viewport | PASS | no horizontal overflow or clipped controls |
| 150% scaling | PASS only when exercised; otherwise NOT RUN | record the exact display limitation when the check cannot be performed safely |

## Scope decision

TASK 13 and TASK 14 acceptance blockers are closed. TASK 15 installer/operational documentation and TASK 16 real-Gmail UAT remain pending.
```

Do not write PASS for any gate that was not actually run.

- [ ] **Step 9: Commit the verified integration state**

```powershell
git add docs/qa/2026-07-22-eunslip-v1-redesign-verification.md
git commit -m "docs: record EunSlip redesign verification"
```

- [ ] **Step 10: Perform final clean-state verification**

```powershell
git status --short
git log --oneline -8
```

Expected: clean worktree and a reviewable sequence of focused redesign commits. Do not push, merge, package, or start TASK 15/16 without separate user direction.
