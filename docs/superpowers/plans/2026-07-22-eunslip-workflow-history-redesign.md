# EunSlip Payroll Workflow and History Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair payroll confirmation, implement real failed-retry and interrupted-recovery flows, and redesign Wizard and History as safe operational workspaces.

**Architecture:** Introduce a small Desktop-layer `PayrollWizardEntry` value that selects Normal, FailedRetry, or RecoveryRetry behavior while continuing to use the existing Core recovery and batch coordinator boundaries. History emits a navigation request; `MainViewModel` owns navigation; the wizard verifies the original fingerprint before filtering resend rows and reuses the original batch with the correct `AttemptType`. Startup marks orphaned Sending batches as Interrupted without resetting recipients; destructive recovery preparation occurs only immediately before a confirmed recovery send.

**Tech Stack:** .NET 10, WPF, CommunityToolkit.Mvvm 8.4.2, SQLite through Microsoft.Data.Sqlite, xUnit 2.9.3.

## Global Constraints

- Execute after `2026-07-22-eunslip-shell-pages-redesign.md` is complete and green.
- Preserve the existing `IBatchCoordinator`, `IRecoveryService`, `IAppRepository`, encryption, Gmail, and PDF generator boundaries.
- Never resend a recipient whose persisted status is `Sent`.
- Failed retry records `AttemptType.FailedRetry`; interrupted recovery records `AttemptType.RecoveryRetry`; normal flow records `AttemptType.Normal`.
- A retry/recovery workbook must match the original batch fingerprint before recipient filtering.
- Do not call `PrepareForRecovery` when the user merely clicks History's recovery action.
- Once sending begins, navigation and window close are blocked until Results or terminal failure.
- User-facing History never displays encrypted NIK/email, full NIK, or unfiltered technical exception text.
- Do not create a new batch for retry/recovery; reuse the original batch and recipients.
- Do not transmit real payroll email during implementation or automated/manual E2E.
- Use scoped tests per task; full solution build/test occurs at the final checkpoint.

## File Map

### Create

- `src/EunSlip.Desktop/ViewModels/PayrollWizardEntry.cs` — immutable Normal/FailedRetry/RecoveryRetry entry state and `AttemptType` mapping.
- `src/EunSlip.Desktop/ViewModels/HistoryRecipientViewModel.cs` — PII-safe recipient and latest-attempt presentation.
- `tests/EunSlip.Desktop.Tests/WorkflowViewContractTests.cs` — structural lifecycle and automation contract for Wizard/History views.
- `tests/EunSlip.Desktop.Tests/ShellFixture.cs` — focused real-view-model fixture for shell navigation and sending-lock tests.

### Modify

- `src/EunSlip.Core/Persistence/IAppRepository.cs` — batch-scoped attempt query.
- `src/EunSlip.Infrastructure/Persistence/SqliteAppRepository.cs` — SQL implementation of the attempt query.
- `tests/EunSlip.Infrastructure.Tests/Persistence/SqliteAppRepositoryTests.cs` — attempt-query ordering and batch isolation.
- `src/EunSlip.Core/Recovery/IRecoveryService.cs` — non-destructive startup interruption marking operation.
- `src/EunSlip.Core/Recovery/RecoveryService.cs` — mark Sending batches Interrupted without preparing resend.
- `tests/EunSlip.Core.Tests/Recovery/RecoveryServiceTests.cs` — startup marking and recovery timing tests.
- `src/EunSlip.Desktop/App.xaml.cs` — call interruption marking instead of early recovery preparation.
- `src/EunSlip.Desktop/ViewModels/HistoryViewModel.cs` — master-detail projection, state-aware actions, and resume requests.
- `src/EunSlip.Desktop/Views/HistoryView.xaml` — corrected visibility and redesigned master-detail UI.
- `tests/EunSlip.Desktop.Tests/HistoryViewModelTests.cs` — detail projection, action gating, and resume events.
- `src/EunSlip.Desktop/ViewModels/PayrollWizardViewModel.cs` — entry modes, prerequisite refresh, fingerprint gate, filtering, and attempt type.
- `src/EunSlip.Desktop/Views/WizardView.xaml.cs` — invoke wizard lifecycle and open a generated preview outside the view model.
- `src/EunSlip.Desktop/Views/WizardView.xaml` — six-step progress treatment and redesigned step layouts.
- `tests/EunSlip.Desktop.Tests/PayrollWizardViewModelTests.cs` — confirmation, retry, recovery, preview, and sending-state regressions.
- `src/EunSlip.Desktop/ViewModels/MainViewModel.cs` — History-to-wizard coordination and sending lock.
- `src/EunSlip.Desktop/MainWindow.xaml.cs` — close interception while sending.
- `tests/EunSlip.Desktop.Tests/MainViewModelTests.cs` — navigation request and command-lock tests.
- `src/EunSlip.Desktop/Localization/Strings.resx` — Indonesian workflow/status/error copy.
- `src/EunSlip.Desktop/Localization/Strings.en.resx` — matching English workflow/status/error copy.
- `tests/EunSlip.Core.Tests/Batches/BatchCoordinatorTests.cs` — repository fake signature update.
- `tests/EunSlip.Desktop.Tests/SettingsViewModelTests.cs` — repository fake signature update.
- `tests/EunSlip.Desktop.Tests/HomeViewModelTests.cs` — repository fake signature update.

---

### Task 1: Add a Batch-Scoped Attempt Query

**Files:**
- Modify: `src/EunSlip.Core/Persistence/IAppRepository.cs`
- Modify: `src/EunSlip.Infrastructure/Persistence/SqliteAppRepository.cs`
- Modify: `tests/EunSlip.Infrastructure.Tests/Persistence/SqliteAppRepositoryTests.cs`
- Modify: every test fake listed in the File Map that implements `IAppRepository`

**Interfaces:**
- Consumes: existing `SendAttempts` and `BatchRecipients` schema.
- Produces: `IReadOnlyList<SendAttemptRecord> IAppRepository.ListAttempts(Guid batchId)` ordered newest first and isolated to one batch.

- [ ] **Step 1: Write the failing SQLite query test**

```csharp
[Fact]
public void ListAttempts_ReturnsOnlyRequestedBatchNewestFirst()
{
    Guid firstBatch = _repo.CreateBatch(NewBatch("JULY 2025"));
    Guid firstRecipient = _repo.AddRecipient(new BatchRecipientRecord(
        Guid.NewGuid(), firstBatch, "enc-1", "mail-1", "0001", RecipientStatus.Failed, DateTimeOffset.UtcNow));
    Guid secondBatch = _repo.CreateBatch(NewBatch("AUGUST 2025"));
    Guid secondRecipient = _repo.AddRecipient(new BatchRecipientRecord(
        Guid.NewGuid(), secondBatch, "enc-2", "mail-2", "0002", RecipientStatus.Failed, DateTimeOffset.UtcNow));

    DateTimeOffset older = new(2026, 7, 22, 1, 0, 0, TimeSpan.Zero);
    DateTimeOffset newer = older.AddMinutes(1);
    _repo.AddAttempt(new SendAttemptRecord(Guid.NewGuid(), firstRecipient, 1, AttemptType.Normal,
        older, older.AddSeconds(1), AttemptStatus.Failed, "Network", "safe", null));
    _repo.AddAttempt(new SendAttemptRecord(Guid.NewGuid(), firstRecipient, 1, AttemptType.FailedRetry,
        newer, newer.AddSeconds(1), AttemptStatus.Sent, null, null, "gmail-1"));
    _repo.AddAttempt(new SendAttemptRecord(Guid.NewGuid(), secondRecipient, 1, AttemptType.Normal,
        newer.AddMinutes(1), newer.AddMinutes(1).AddSeconds(1), AttemptStatus.Failed, "Network", "safe", null));

    IReadOnlyList<SendAttemptRecord> attempts = _repo.ListAttempts(firstBatch);

    Assert.Equal(2, attempts.Count);
    Assert.Equal(AttemptType.FailedRetry, attempts[0].AttemptType);
    Assert.Equal(AttemptType.Normal, attempts[1].AttemptType);
    Assert.All(attempts, attempt => Assert.Equal(firstRecipient, attempt.RecipientId));
}
```

- [ ] **Step 2: Run the persistence test and verify the missing method**

Run:

```powershell
dotnet test tests/EunSlip.Infrastructure.Tests/EunSlip.Infrastructure.Tests.csproj --no-restore --filter FullyQualifiedName~SqliteAppRepositoryTests.ListAttempts
```

Expected: build FAIL because `ListAttempts(Guid)` is not defined.

- [ ] **Step 3: Add the repository signature and SQLite implementation**

Add this member after `ListRecipients` in `IAppRepository`:

```csharp
IReadOnlyList<SendAttemptRecord> ListAttempts(Guid batchId);
```

Add this implementation after `GetLatestAttemptStatus` in `SqliteAppRepository`:

```csharp
public IReadOnlyList<SendAttemptRecord> ListAttempts(Guid batchId)
{
    using SqliteConnection connection = OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
        "SELECT a.Id, a.RecipientId, a.AttemptNumber, a.AttemptType, a.StartedAtUtc, " +
        "a.CompletedAtUtc, a.Status, a.ErrorCategory, a.ErrorMessage, a.GmailMessageId " +
        "FROM SendAttempts a INNER JOIN BatchRecipients r ON r.Id = a.RecipientId " +
        "WHERE r.BatchId = @batchId ORDER BY a.StartedAtUtc DESC, a.AttemptNumber DESC;";
    command.Parameters.AddWithValue("@batchId", batchId.ToString());

    using SqliteDataReader reader = command.ExecuteReader();
    List<SendAttemptRecord> attempts = [];
    while (reader.Read())
    {
        attempts.Add(new SendAttemptRecord(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetInt32(2),
            Enum.Parse<AttemptType>(reader.GetString(3)),
            DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
            reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
            Enum.Parse<AttemptStatus>(reader.GetString(6)),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9)));
    }

    return attempts;
}
```

Add this exact no-op implementation to test repositories that do not need attempt data:

```csharp
public IReadOnlyList<SendAttemptRecord> ListAttempts(Guid batchId) => [];
```

The History test repository will receive a real in-memory `Attempts` list in Task 3 instead of the no-op implementation.

- [ ] **Step 4: Run persistence, Core, and Desktop compilation tests**

Run:

```powershell
dotnet test tests/EunSlip.Infrastructure.Tests/EunSlip.Infrastructure.Tests.csproj --no-restore --filter FullyQualifiedName~SqliteAppRepositoryTests
dotnet build EunSlip.slnx --no-restore
```

Expected: persistence tests PASS; solution builds with 0 warnings and 0 errors.

- [ ] **Step 5: Commit the query contract**

```powershell
git add src/EunSlip.Core/Persistence/IAppRepository.cs src/EunSlip.Infrastructure/Persistence/SqliteAppRepository.cs tests
git commit -m "feat: query batch send attempts"
```

---

### Task 2: Separate Startup Interruption Marking from Recovery Preparation

**Files:**
- Modify: `src/EunSlip.Core/Recovery/IRecoveryService.cs`
- Modify: `src/EunSlip.Core/Recovery/RecoveryService.cs`
- Modify: `src/EunSlip.Desktop/App.xaml.cs`
- Modify: `tests/EunSlip.Core.Tests/Recovery/RecoveryServiceTests.cs`
- Modify: fake `IRecoveryService` implementations in Desktop tests

**Interfaces:**
- Consumes: `IAppRepository.FindInterruptedBatches` and `UpdateBatchStatus`.
- Produces: `IReadOnlyList<Guid> MarkDetectedBatchesInterrupted()` with no recipient reset or reconciliation side effect.

- [ ] **Step 1: Write the failing non-destructive startup test**

Extend the Recovery test fake with `public List<Guid> ResetBatches { get; } = [];` and make `ResetSendingRecipientsToPending` add to it. Then add:

```csharp
[Fact]
public void MarkDetectedBatchesInterrupted_ChangesBatchStatusWithoutPreparingRecipients()
{
    var (service, repository) = Setup();
    Guid batchId = SeedBatch(repository, "fp", ("NIK0001", RecipientStatus.Sending));
    repository.UpdateBatchStatus(batchId, BatchStatus.Sending, DateTimeOffset.UtcNow, null);

    IReadOnlyList<Guid> marked = service.MarkDetectedBatchesInterrupted();

    Assert.Single(marked, batchId);
    Assert.Equal(BatchStatus.Interrupted, repository.GetBatch(batchId)!.Status);
    Assert.Empty(repository.ResetBatches);
    Assert.Equal(RecipientStatus.Sending, repository.ListRecipients(batchId)[0].Status);
}
```

- [ ] **Step 2: Run the recovery test and verify the new operation is missing**

Run:

```powershell
dotnet test tests/EunSlip.Core.Tests/EunSlip.Core.Tests.csproj --no-restore --filter FullyQualifiedName~MarkDetectedBatchesInterrupted
```

Expected: build FAIL because `MarkDetectedBatchesInterrupted` does not exist.

- [ ] **Step 3: Implement marking in the recovery service**

Add to `IRecoveryService`:

```csharp
IReadOnlyList<Guid> MarkDetectedBatchesInterrupted();
```

Add to `RecoveryService`:

```csharp
public IReadOnlyList<Guid> MarkDetectedBatchesInterrupted()
{
    IReadOnlyList<Guid> batchIds = _repository.FindInterruptedBatches();
    foreach (Guid batchId in batchIds)
    {
        _repository.UpdateBatchStatus(batchId, BatchStatus.Interrupted, null, null);
    }

    return batchIds;
}
```

Keep `PrepareForRecovery` unchanged; its reconciliation and reset remain the operation used immediately before a confirmed RecoveryRetry send.

- [ ] **Step 4: Replace early preparation in `App.OnStartup`**

Replace the startup call and remove the old `PrepareInterruptedBatches` helper:

```csharp
_services.GetRequiredService<IRecoveryService>().MarkDetectedBatchesInterrupted();
```

Every fake recovery implementation receives this deterministic default:

```csharp
public IReadOnlyList<Guid> MarkDetectedBatchesInterrupted() => [];
```

- [ ] **Step 5: Run recovery tests and solution build**

Run:

```powershell
dotnet test tests/EunSlip.Core.Tests/EunSlip.Core.Tests.csproj --no-restore --filter FullyQualifiedName~RecoveryServiceTests
dotnet build EunSlip.slnx --no-restore
```

Expected: tests PASS; build succeeds with 0 warnings and 0 errors.

- [ ] **Step 6: Commit interruption marking**

```powershell
git add src/EunSlip.Core/Recovery src/EunSlip.Desktop/App.xaml.cs tests
git commit -m "fix: defer recovery preparation until send"
```

---

### Task 3: Implement History Master-Detail State and Resume Requests

**Files:**
- Create: `src/EunSlip.Desktop/ViewModels/PayrollWizardEntry.cs`
- Create: `src/EunSlip.Desktop/ViewModels/HistoryRecipientViewModel.cs`
- Modify: `src/EunSlip.Desktop/ViewModels/HistoryViewModel.cs`
- Modify: `src/EunSlip.Desktop/Views/HistoryView.xaml`
- Modify: `src/EunSlip.Desktop/Localization/Strings.resx`
- Modify: `src/EunSlip.Desktop/Localization/Strings.en.resx`
- Modify: `tests/EunSlip.Desktop.Tests/HistoryViewModelTests.cs`

**Interfaces:**
- Consumes: `IAppRepository.ListRecipients/ListAttempts`, batch statuses, and existing deletion behavior.
- Produces: `PayrollWizardEntry`, `ObservableCollection<HistoryRecipientViewModel> SelectedRecipients`, `event Action<PayrollWizardEntry>? ResumeRequested`, status-aware commands, and a stable master-detail page.

- [ ] **Step 1: Write failing History projection and action tests**

Update the History fake repository to store attempts and return them by batch. Add these tests:

```csharp
public List<SendAttemptRecord> Attempts { get; } = [];

public void AddAttempt(SendAttemptRecord attempt) => Attempts.Add(attempt);

public IReadOnlyList<SendAttemptRecord> ListAttempts(Guid batchId)
{
    HashSet<Guid> recipientIds = [.. Recipients.Where(recipient => recipient.BatchId == batchId)
        .Select(recipient => recipient.Id)];
    return [.. Attempts.Where(attempt => recipientIds.Contains(attempt.RecipientId))
        .OrderByDescending(attempt => attempt.StartedAtUtc)
        .ThenByDescending(attempt => attempt.AttemptNumber)];
}
```

```csharp
[Fact]
public void SelectingBatch_ProjectsLatestAttemptWithoutExposingEncryptedValues()
{
    HistoryViewModel vm = Create(out FakeRepository repository, out _);
    PayrollBatchRecord batch = Batch(Guid.NewGuid());
    repository.CreateBatch(batch);
    Guid recipientId = repository.AddRecipient(new BatchRecipientRecord(Guid.NewGuid(), batch.Id,
        "encrypted-nik", "encrypted-email", "0004", RecipientStatus.Failed, DateTimeOffset.UtcNow));
    repository.Attempts.Add(new SendAttemptRecord(Guid.NewGuid(), recipientId, 1, AttemptType.Normal,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, AttemptStatus.Failed, "Network", "technical", null));
    vm.LoadedCommand.Execute(null);

    vm.SelectedBatch = batch;

    HistoryRecipientViewModel detail = Assert.Single(vm.SelectedRecipients);
    Assert.Equal("0004", detail.NikHint);
    Assert.Equal(AttemptType.Normal, detail.LatestAttemptType);
    Assert.Equal("Network", detail.ErrorCategory);
    Assert.DoesNotContain("encrypted", detail.ErrorSummary, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void Actions_AreEnabledOnlyForMatchingBatchStates()
{
    HistoryViewModel vm = Create(out _, out _);
    PayrollBatchRecord retryable = Batch(Guid.NewGuid());
    PayrollBatchRecord interrupted = retryable with { Id = Guid.NewGuid(), Status = BatchStatus.Interrupted };
    PayrollBatchRecord clean = retryable with { Id = Guid.NewGuid(), FailedCount = 0 };

    Assert.True(vm.RetryFailedCommand.CanExecute(retryable));
    Assert.False(vm.RetryFailedCommand.CanExecute(clean));
    Assert.False(vm.RecoverCommand.CanExecute(retryable));
    Assert.True(vm.RecoverCommand.CanExecute(interrupted));
}

[Fact]
public void RecoveryAction_RequestsWizardNavigationWithoutPreparingBatch()
{
    HistoryViewModel vm = Create(out _, out FakeRecovery recovery);
    PayrollBatchRecord interrupted = Batch(Guid.NewGuid()) with { Status = BatchStatus.Interrupted };
    PayrollWizardEntry? requested = null;
    vm.ResumeRequested += entry => requested = entry;

    vm.RecoverCommand.Execute(interrupted);

    Assert.Equal(PayrollRunMode.RecoveryRetry, requested!.Mode);
    Assert.Equal(interrupted.Id, requested.BatchId);
    Assert.Empty(recovery.Prepared);
}
```

- [ ] **Step 2: Run History tests and verify the missing models/state**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~HistoryViewModelTests
```

Expected: build FAIL because the new models, attempt projection, event, and action predicates do not exist.

- [ ] **Step 3: Create the immutable wizard entry model**

```csharp
using EunSlip.Core.Persistence;

namespace EunSlip.Desktop.ViewModels;

public enum PayrollRunMode { Normal, FailedRetry, RecoveryRetry }

public sealed record PayrollWizardEntry(PayrollRunMode Mode, Guid? BatchId)
{
    public static PayrollWizardEntry Normal() => new(PayrollRunMode.Normal, null);

    public static PayrollWizardEntry FailedRetry(Guid batchId) =>
        new(PayrollRunMode.FailedRetry, batchId);

    public static PayrollWizardEntry RecoveryRetry(Guid batchId) =>
        new(PayrollRunMode.RecoveryRetry, batchId);

    public AttemptType AttemptType => Mode switch
    {
        PayrollRunMode.Normal => AttemptType.Normal,
        PayrollRunMode.FailedRetry => AttemptType.FailedRetry,
        PayrollRunMode.RecoveryRetry => AttemptType.RecoveryRetry,
        _ => throw new InvalidOperationException($"Unsupported payroll run mode: {Mode}"),
    };

    public bool IsResume => Mode != PayrollRunMode.Normal;
}
```

- [ ] **Step 4: Create the PII-safe History detail projection**

```csharp
using EunSlip.Core.Persistence;
using EunSlip.Desktop.Localization;

namespace EunSlip.Desktop.ViewModels;

public sealed class HistoryRecipientViewModel
{
    public HistoryRecipientViewModel(BatchRecipientRecord recipient, SendAttemptRecord? latestAttempt)
    {
        NikHint = recipient.NikHint ?? "—";
        Status = recipient.Status;
        LastUpdatedAtUtc = recipient.LastUpdatedAtUtc;
        LatestAttemptType = latestAttempt?.AttemptType;
        LatestAttemptNumber = latestAttempt?.AttemptNumber;
        LatestAttemptCompletedAtUtc = latestAttempt?.CompletedAtUtc;
        ErrorCategory = latestAttempt?.ErrorCategory ?? "—";
        ErrorSummary = latestAttempt?.Status == AttemptStatus.Failed
            ? Strings.Get("HistoryDeliveryFailedSummary")
            : "—";
    }

    public string NikHint { get; }
    public RecipientStatus Status { get; }
    public DateTimeOffset LastUpdatedAtUtc { get; }
    public AttemptType? LatestAttemptType { get; }
    public int? LatestAttemptNumber { get; }
    public DateTimeOffset? LatestAttemptCompletedAtUtc { get; }
    public string ErrorCategory { get; }
    public string ErrorSummary { get; }
}
```

- [ ] **Step 5: Replace History selection and action logic**

Use `ObservableCollection<HistoryRecipientViewModel>`. Build a latest-attempt lookup by recipient ID and emit navigation requests without mutating recovery state:

```csharp
public event Action<PayrollWizardEntry>? ResumeRequested;

partial void OnSelectedBatchChanged(PayrollBatchRecord? value)
{
    SelectedRecipients.Clear();
    if (value is null)
    {
        return;
    }

    try
    {
        Dictionary<Guid, SendAttemptRecord> latestAttempts = _repository.ListAttempts(value.Id)
            .GroupBy(attempt => attempt.RecipientId)
            .ToDictionary(group => group.Key, group => group
                .OrderByDescending(attempt => attempt.StartedAtUtc)
                .ThenByDescending(attempt => attempt.AttemptNumber)
                .First());

        foreach (BatchRecipientRecord recipient in _repository.ListRecipients(value.Id))
        {
            _ = latestAttempts.TryGetValue(recipient.Id, out SendAttemptRecord? latest);
            SelectedRecipients.Add(new HistoryRecipientViewModel(recipient, latest));
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to load batch recipient details");
        StatusMessage = Strings.Get("HistoryDetailLoadFailed");
    }
}

[RelayCommand(CanExecute = nameof(CanRetryFailed))]
private void RetryFailed(PayrollBatchRecord? batch)
{
    if (batch is not null)
    {
        ResumeRequested?.Invoke(PayrollWizardEntry.FailedRetry(batch.Id));
    }
}

[RelayCommand(CanExecute = nameof(CanRecover))]
private void Recover(PayrollBatchRecord? batch)
{
    if (batch is not null)
    {
        ResumeRequested?.Invoke(PayrollWizardEntry.RecoveryRetry(batch.Id));
    }
}

private static bool CanRetryFailed(PayrollBatchRecord? batch) =>
    batch is { Status: BatchStatus.Completed, FailedCount: > 0 };

private static bool CanRecover(PayrollBatchRecord? batch) =>
    batch is { Status: BatchStatus.Interrupted };
```

Keep delete confirmation, but its CanExecute predicate remains only `batch is not null`.

Add localized resource values for `HistoryDeliveryFailedSummary` and `HistoryDetailLoadFailed`:

```xml
<!-- Strings.resx -->
<data name="HistoryDeliveryFailedSummary" xml:space="preserve"><value>Pengiriman gagal. Periksa kategori dan log untuk detail teknis.</value></data>
<data name="HistoryDetailLoadFailed" xml:space="preserve"><value>Detail batch tidak dapat dimuat.</value></data>

<!-- Strings.en.resx -->
<data name="HistoryDeliveryFailedSummary" xml:space="preserve"><value>Delivery failed. Review the category and logs for technical details.</value></data>
<data name="HistoryDetailLoadFailed" xml:space="preserve"><value>Batch details could not be loaded.</value></data>
```

- [ ] **Step 6: Redesign History XAML and correct visibility**

Use a two-column `Grid` with batch master at width `360` and detail in the remaining width. The detail table must use `NullToVisibilityConverter`; the placeholder must use `NullToVisibilityInverseConverter`:

```xml
<DataGrid ItemsSource="{Binding SelectedRecipients}" Style="{StaticResource EditorialDataGrid}"
          AutoGenerateColumns="False" IsReadOnly="True"
          Visibility="{Binding SelectedBatch, Converter={StaticResource NullToVis}}">
    <DataGrid.Columns>
        <DataGridTextColumn Header="NIK" Binding="{Binding NikHint}" Width="70" />
        <DataGridTextColumn Header="STATUS" Binding="{Binding Status}" Width="90" />
        <DataGridTextColumn Header="TIPE" Binding="{Binding LatestAttemptType}" Width="100" />
        <DataGridTextColumn Header="PERC." Binding="{Binding LatestAttemptNumber}" Width="60" />
        <DataGridTextColumn Header="SELESAI" Binding="{Binding LatestAttemptCompletedAtUtc}" Width="150" />
        <DataGridTextColumn Header="KATEGORI" Binding="{Binding ErrorCategory}" Width="110" />
        <DataGridTextColumn Header="RINGKASAN" Binding="{Binding ErrorSummary}" Width="*" />
    </DataGrid.Columns>
</DataGrid>
<TextBlock Text="Pilih batch untuk melihat penerima dan percobaan pengiriman."
           Style="{StaticResource MutedText}" VerticalAlignment="Center" HorizontalAlignment="Center"
           Visibility="{Binding SelectedBatch, Converter={StaticResource NullToVisInverse}}" />
```

Bind action buttons directly to command CanExecute; remove the redundant `NullToBool` `IsEnabled` bindings. Use `DangerButton` for delete.

- [ ] **Step 7: Run History and persistence tests**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~HistoryViewModelTests
dotnet test tests/EunSlip.Infrastructure.Tests/EunSlip.Infrastructure.Tests.csproj --no-restore --filter FullyQualifiedName~SqliteAppRepositoryTests.ListAttempts
```

Expected: tests PASS; 0 failed.

- [ ] **Step 8: Commit the History behavior and layout**

```powershell
git add src/EunSlip.Desktop/ViewModels/PayrollWizardEntry.cs src/EunSlip.Desktop/ViewModels/HistoryRecipientViewModel.cs src/EunSlip.Desktop/ViewModels/HistoryViewModel.cs src/EunSlip.Desktop/Views/HistoryView.xaml src/EunSlip.Desktop/Localization tests/EunSlip.Desktop.Tests/HistoryViewModelTests.cs
git commit -m "feat: make History an operational workspace"
```

---

### Task 4: Repair Wizard Lifecycle and Confirmation Readiness

**Files:**
- Modify: `src/EunSlip.Desktop/ViewModels/PayrollWizardViewModel.cs`
- Modify: `src/EunSlip.Desktop/Views/WizardView.xaml.cs`
- Modify: `tests/EunSlip.Desktop.Tests/PayrollWizardViewModelTests.cs`
- Create: `tests/EunSlip.Desktop.Tests/WorkflowViewContractTests.cs`
- Modify: `src/EunSlip.Desktop/Localization/Strings.resx`
- Modify: `src/EunSlip.Desktop/Localization/Strings.en.resx`

**Interfaces:**
- Consumes: `PayrollWizardEntry` and existing Gmail/stamp services.
- Produces: `Begin(PayrollWizardEntry)`, `RefreshPrerequisitesAsync(CancellationToken)`, `IsPrerequisiteLoading`, localized readiness labels, `IsSending`, and a view Loaded hook.

- [ ] **Step 1: Write failing lifecycle and command invalidation tests**

Make `FakeGmail` and `FakeStampStore` mutable in the wizard tests. Add:

```csharp
private sealed class FakeGmail(bool connected, string? email = null) : IGmailAuthorization
{
    public bool Connected { get; set; } = connected;
    public Task<GoogleAccount?> ConnectAsync(string clientSecretJson, CancellationToken cancellationToken) =>
        Task.FromResult<GoogleAccount?>(Connected ? new GoogleAccount(email ?? "g@e.co") : null);
    public Task<GoogleAccount?> RestoreAsync(CancellationToken cancellationToken) =>
        Task.FromResult<GoogleAccount?>(Connected ? new GoogleAccount(email ?? "g@e.co") : null);
    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        Connected = false;
        return Task.CompletedTask;
    }
    public Task<bool> IsConnectedAsync(CancellationToken cancellationToken) => Task.FromResult(Connected);
}

private sealed class FakeStampStore(bool hasStamp) : ISharedFileStore
{
    public bool HasStamp { get; set; } = hasStamp;
    public string? GetActiveStampPath() => HasStamp ? "stamp.png" : null;
    public string ImportStamp(string sourcePath)
    {
        HasStamp = true;
        return "stamp.png";
    }
    public void RemoveStamp() => HasStamp = false;
}

private sealed class FakeRecovery : IRecoveryService
{
    public List<Guid> Prepared { get; } = [];
    public IReadOnlyList<string> FailedNiks { get; init; } = ["NIK0001"];
    public IReadOnlyList<string> RecoveryNiks { get; init; } = ["NIK0001"];
    public bool FingerprintMatches { get; init; } = true;
    public IReadOnlyList<Guid> DetectInterruptedBatches() => [];
    public IReadOnlyList<Guid> MarkDetectedBatchesInterrupted() => [];
    public void PrepareForRecovery(Guid batchId) => Prepared.Add(batchId);
    public RecoveryGate VerifyFingerprint(Guid batchId, BatchContext context, IReadOnlyList<PayrollRow> rows) =>
        FingerprintMatches
            ? new RecoveryGate(RecoveryGateResult.Match, "stored", "stored")
            : new RecoveryGate(RecoveryGateResult.Mismatch, "stored", "different");
    public IReadOnlyList<string> SelectRetryFailedNiks(Guid batchId) => FailedNiks;
    public IReadOnlyList<string> SelectRecoveryResendNiks(Guid batchId) => RecoveryNiks;
}

private static PayrollWizardViewModel Create(
    IPayrollWorkbookReader? reader = null,
    bool gmailConnected = true,
    bool hasStamp = true,
    IBatchCoordinator? coordinator = null,
    FakeRecovery? recovery = null,
    FakeGmail? gmail = null,
    FakeStampStore? stampStore = null) =>
    Create(out _, reader, gmailConnected, hasStamp, coordinator, recovery, gmail, stampStore);

private static PayrollWizardViewModel Create(
    out FakeRepository repository,
    IPayrollWorkbookReader? reader = null,
    bool gmailConnected = true,
    bool hasStamp = true,
    IBatchCoordinator? coordinator = null,
    FakeRecovery? recovery = null,
    FakeGmail? gmail = null,
    FakeStampStore? stampStore = null)
{
    repository = new FakeRepository();
    return new PayrollWizardViewModel(
        reader ?? new FakeReader(_ => new WorkbookReadResult(PayrollContract.Headers, [ValidInput(1)], [])),
        coordinator ?? new FakeCoordinator(),
        new FakePdfGenerator(),
        gmail ?? new FakeGmail(gmailConnected),
        stampStore ?? new FakeStampStore(hasStamp),
        repository,
        new PassThroughSecretStore(),
        new FakeTempFiles(),
        recovery ?? new FakeRecovery(),
        NullLogger<PayrollWizardViewModel>.Instance);
}
```

Add the lifecycle tests:

```csharp
[Fact]
public async Task Loaded_RefreshesPrerequisitesAndEnablesConfirm()
{
    PayrollWizardViewModel vm = Create();
    vm.Begin(PayrollWizardEntry.Normal());
    FillSelectStep(vm);
    await vm.NextCommand.ExecuteAsync(null);
    vm.CurrentStep = WizardStep.Confirm;

    Assert.False(vm.ConfirmSendCommand.CanExecute(null));
    await vm.LoadedCommand.ExecuteAsync(null);

    Assert.True(vm.GmailReady);
    Assert.True(vm.StampReady);
    Assert.True(vm.ConfirmSendCommand.CanExecute(null));
}

[Fact]
public async Task MovingFromPreviewToConfirm_RefreshesPrerequisites()
{
    FakeGmail gmail = new(false);
    FakeStampStore stamp = new(false);
    PayrollWizardViewModel vm = Create(gmail: gmail, stampStore: stamp);
    vm.Begin(PayrollWizardEntry.Normal());
    FillSelectStep(vm);
    await vm.NextCommand.ExecuteAsync(null);
    vm.CurrentStep = WizardStep.Preview;
    gmail.Connected = true;
    stamp.HasStamp = true;

    await vm.NextCommand.ExecuteAsync(null);

    Assert.Equal(WizardStep.Confirm, vm.CurrentStep);
    Assert.True(vm.ConfirmSendCommand.CanExecute(null));
}
```

Add a structural lifecycle test:

```csharp
using System.Text;

namespace EunSlip.Desktop.Tests;

public sealed class WorkflowViewContractTests
{
    private static string ReadFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EunSlip.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory!.FullName, .. parts]), Encoding.UTF8);
    }

    [Fact]
    public void WizardView_InvokesLoadedCommand()
    {
        string code = ReadFile("src", "EunSlip.Desktop", "Views", "WizardView.xaml.cs");
        Assert.Contains("LoadedCommand.ExecuteAsync", code, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run lifecycle tests and confirm the original bug**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~Loaded_RefreshesPrerequisites|FullyQualifiedName~MovingFromPreviewToConfirm|FullyQualifiedName~WorkflowViewContractTests"
```

Expected: FAIL because `Begin` is absent, WizardView never calls Loaded, and readiness changes do not invalidate `ConfirmSendCommand`.

- [ ] **Step 3: Add explicit lifecycle and readiness state**

Inject `IRecoveryService recovery` into `PayrollWizardViewModel`. Add the field, constructor parameter, and assignment:

```csharp
private readonly IRecoveryService _recovery;
private PayrollWizardEntry _entry = PayrollWizardEntry.Normal();

public PayrollWizardViewModel(
    IPayrollWorkbookReader reader,
    IBatchCoordinator coordinator,
    IPayslipPdfGenerator pdfGenerator,
    IGmailAuthorization gmail,
    ISharedFileStore stampStore,
    IAppRepository repository,
    ISecretStore secretStore,
    ITempFileService tempFiles,
    IRecoveryService recovery,
    ILogger<PayrollWizardViewModel> logger)
{
    _reader = reader;
    _coordinator = coordinator;
    _pdfGenerator = pdfGenerator;
    _gmail = gmail;
    _stampStore = stampStore;
    _repository = repository;
    _secretStore = secretStore;
    _tempFiles = tempFiles;
    _recovery = recovery;
    _logger = logger;
}

[ObservableProperty]
private bool _isPrerequisiteLoading;

public PayrollRunMode RunMode => _entry.Mode;
public bool IsResumeMode => _entry.IsResume;
public string GmailStatusText => IsPrerequisiteLoading
    ? Strings.Get("StatusChecking")
    : HasGmailConnection ? Strings.Get("StatusReady") : Strings.Get("StatusNotReady");
public string StampStatusText => IsPrerequisiteLoading
    ? Strings.Get("StatusChecking")
    : HasStamp ? Strings.Get("StatusReady") : Strings.Get("StatusNotReady");
public bool IsSending => CurrentStep == WizardStep.Send && IsBusy;

public bool Begin(PayrollWizardEntry entry)
{
    Reset();
    _entry = entry;
    OnPropertyChanged(nameof(RunMode));
    OnPropertyChanged(nameof(IsResumeMode));
    if (entry.BatchId is not Guid batchId)
    {
        return true;
    }

    PayrollBatchRecord? batch = _repository.GetBatch(batchId);
    if (batch is null)
    {
        ErrorMessage = Strings.Get("HistoryBatchMissing");
        return false;
    }

    _batchId = batch.Id;
    Period = batch.Period;
    PaymentDate = batch.PaymentDate.ToDateTime(TimeOnly.MinValue);
    return true;
}

public async Task RefreshPrerequisitesAsync(CancellationToken cancellationToken)
{
    IsPrerequisiteLoading = true;
    try
    {
        GoogleAccount? account = await _gmail.RestoreAsync(cancellationToken);
        ConnectedGmail = account?.Email;
        HasGmailConnection = account is not null;
        HasStamp = _stampStore.GetActiveStampPath() is not null;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Prerequisite refresh failed");
        HasGmailConnection = false;
        HasStamp = false;
        ErrorMessage = Strings.Get("PrerequisiteRefreshFailed");
    }
    finally
    {
        IsPrerequisiteLoading = false;
    }
}
```

`LoadedAsync` calls `RefreshPrerequisitesAsync(CancellationToken.None)` and `LoadEmailTemplate()`.

- [ ] **Step 4: Refresh before Confirm and invalidate every derived property**

Change the Preview branch in `NextAsync`:

```csharp
if (CurrentStep == WizardStep.Preview)
{
    await RefreshPrerequisitesAsync(CancellationToken.None);
    CurrentStep = WizardStep.Confirm;
    return;
}
```

Use these partial methods:

```csharp
partial void OnHasGmailConnectionChanged(bool value)
{
    OnPropertyChanged(nameof(GmailReady));
    OnPropertyChanged(nameof(GmailStatusText));
    OnPropertyChanged(nameof(IsReadyToConfirm));
    ConfirmSendCommand.NotifyCanExecuteChanged();
}

partial void OnHasStampChanged(bool value)
{
    OnPropertyChanged(nameof(StampReady));
    OnPropertyChanged(nameof(StampStatusText));
    OnPropertyChanged(nameof(IsReadyToConfirm));
    ConfirmSendCommand.NotifyCanExecuteChanged();
}

partial void OnIsPrerequisiteLoadingChanged(bool value)
{
    OnPropertyChanged(nameof(GmailStatusText));
    OnPropertyChanged(nameof(StampStatusText));
    OnPropertyChanged(nameof(IsReadyToConfirm));
    ConfirmSendCommand.NotifyCanExecuteChanged();
}

partial void OnIsBusyChanged(bool value)
{
    OnPropertyChanged(nameof(IsSending));
    OnPropertyChanged(nameof(IsReadyToConfirm));
    NextCommand.NotifyCanExecuteChanged();
    BackCommand.NotifyCanExecuteChanged();
    ConfirmSendCommand.NotifyCanExecuteChanged();
}
```

`CanConfirmSend` also requires `!IsPrerequisiteLoading`.

- [ ] **Step 5: Wire the view lifecycle**

Add this to `WizardView` immediately after `InitializeComponent()`:

```csharp
Loaded += async (_, _) =>
{
    if (DataContext is PayrollWizardViewModel vm)
    {
        await vm.LoadedCommand.ExecuteAsync(null);
    }
};
```

Add matching Indonesian/English values for `HistoryBatchMissing` and `PrerequisiteRefreshFailed`.

- [ ] **Step 6: Run lifecycle tests and desktop build**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~PayrollWizardViewModelTests|FullyQualifiedName~WorkflowViewContractTests"
dotnet build src/EunSlip.Desktop/EunSlip.Desktop.csproj --no-restore
```

Expected: tests PASS, including the former Langkah 4 blocker; build succeeds with 0 warnings and 0 errors.

- [ ] **Step 7: Commit lifecycle repair**

```powershell
git add src/EunSlip.Desktop/ViewModels/PayrollWizardViewModel.cs src/EunSlip.Desktop/Views/WizardView.xaml.cs src/EunSlip.Desktop/Localization tests/EunSlip.Desktop.Tests
git commit -m "fix: refresh wizard send prerequisites"
```

---

### Task 5: Implement Fingerprinted Retry and Recovery Runs

**Files:**
- Modify: `src/EunSlip.Desktop/ViewModels/PayrollWizardViewModel.cs`
- Modify: `tests/EunSlip.Desktop.Tests/PayrollWizardViewModelTests.cs`

**Interfaces:**
- Consumes: `PayrollWizardEntry`, `IRecoveryService.VerifyFingerprint/SelectRetryFailedNiks/SelectRecoveryResendNiks/PrepareForRecovery`, and `IBatchCoordinator.RunBatchAsync`.
- Produces: fingerprint-gated row filtering, original batch reuse, correct attempt type, and just-in-time recovery preparation.

- [ ] **Step 1: Write failing retry/recovery tests**

Extend the fake coordinator with `public BatchRunRequest? LastRequest { get; private set; }` and the fake recovery with lists for prepared IDs and selectable NIKs. Add:

```csharp
[Fact]
public async Task FailedRetry_ReusesBatchFiltersRowsAndUsesFailedRetryAttemptType()
{
    Guid batchId = Guid.NewGuid();
    FakeCoordinator coordinator = new();
    FakeRecovery recovery = new() { FailedNiks = ["NIK0002"] };
    PayrollWizardViewModel vm = Create(out FakeRepository repository,
        reader: ReaderWithRows(ValidInput(1), ValidInput(2)), coordinator: coordinator, recovery: recovery);
    repository.CreateBatch(BatchForRows(batchId, "JULY 2025", ValidInput(1), ValidInput(2)));
    SeedRecipients(repository, batchId, "NIK0001", "NIK0002");
    Assert.True(vm.Begin(PayrollWizardEntry.FailedRetry(batchId)));
    vm.SelectedFilePath = "payroll.xlsx";

    await vm.NextCommand.ExecuteAsync(null);
    vm.CurrentStep = WizardStep.Confirm;
    await vm.LoadedCommand.ExecuteAsync(null);
    await vm.ConfirmSendCommand.ExecuteAsync(null);

    Assert.Equal(batchId, coordinator.LastRequest!.BatchId);
    Assert.Equal(AttemptType.FailedRetry, coordinator.LastRequest.AttemptKind);
    Assert.Single(coordinator.LastRequest.Rows, row => row.Nik == "NIK0002");
    Assert.Empty(recovery.Prepared);
}

[Fact]
public async Task Recovery_PreparesOnlyAtConfirmedSendAndExcludesSentRecipients()
{
    Guid batchId = Guid.NewGuid();
    FakeCoordinator coordinator = new();
    FakeRecovery recovery = new() { RecoveryNiks = ["NIK0002"] };
    PayrollWizardViewModel vm = Create(out FakeRepository repository,
        reader: ReaderWithRows(ValidInput(1), ValidInput(2)), coordinator: coordinator, recovery: recovery);
    repository.CreateBatch(BatchForRows(batchId, "JULY 2025", ValidInput(1), ValidInput(2)) with { Status = BatchStatus.Interrupted });
    SeedRecipients(repository, batchId, "NIK0001", "NIK0002");
    Assert.True(vm.Begin(PayrollWizardEntry.RecoveryRetry(batchId)));
    vm.SelectedFilePath = "payroll.xlsx";

    await vm.NextCommand.ExecuteAsync(null);
    Assert.Empty(recovery.Prepared);
    vm.CurrentStep = WizardStep.Confirm;
    await vm.LoadedCommand.ExecuteAsync(null);
    await vm.ConfirmSendCommand.ExecuteAsync(null);

    Assert.Single(recovery.Prepared, batchId);
    Assert.Equal(AttemptType.RecoveryRetry, coordinator.LastRequest!.AttemptKind);
    Assert.Single(coordinator.LastRequest.Rows, row => row.Nik == "NIK0002");
}

[Fact]
public async Task Resume_FingerprintMismatchBlocksBeforeRecipientFiltering()
{
    FakeRecovery recovery = new() { FingerprintMatches = false };
    PayrollWizardViewModel vm = Create(out FakeRepository repository, recovery: recovery);
    Guid batchId = repository.CreateBatch(BatchForRows(Guid.NewGuid(), "JULY 2025", ValidInput(1)));
    Assert.True(vm.Begin(PayrollWizardEntry.FailedRetry(batchId)));
    vm.SelectedFilePath = "different.xlsx";

    await vm.NextCommand.ExecuteAsync(null);

    Assert.Equal(WizardStep.Select, vm.CurrentStep);
    Assert.Contains("fingerprint", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    Assert.Empty(repository.Batches.Where(batch => batch.Id != batchId));
}
```

The helper methods in this test file must compute the stored fingerprint from the same `BatchContext` and converted valid rows; do not hard-code a fake matching fingerprint.

Use these exact helpers and fake extensions:

```csharp
private static FakeReader ReaderWithRows(params PayrollRowInput[] rows) =>
    new(_ => new WorkbookReadResult(PayrollContract.Headers, rows, []));

private static PayrollBatchRecord BatchForRows(Guid id, string period, params PayrollRowInput[] rows)
{
    DateOnly paymentDate = new(2026, 5, 11);
    ValidationResult validation = PayrollValidator.Validate(PayrollContract.Headers, rows, null);
    string fingerprint = PayrollFingerprint.Compute(new BatchContext(period, paymentDate), validation.ValidRows);
    return new PayrollBatchRecord(id, period, paymentDate, fingerprint, BatchStatus.Completed,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true,
        validation.ValidRows.Count, validation.ValidRows.Count - 1, 1);
}

private static void SeedRecipients(FakeRepository repository, Guid batchId, params string[] niks)
{
    foreach (string nik in niks)
    {
        repository.AddRecipient(new BatchRecipientRecord(Guid.NewGuid(), batchId, nik,
            $"{nik}@example.com", NikHint.LastFour(nik), RecipientStatus.Failed, DateTimeOffset.UtcNow));
    }
}

private sealed class FakeCoordinator : IBatchCoordinator
{
    public BatchRunRequest? LastRequest { get; private set; }

    public Task<BatchRunResult> RunBatchAsync(BatchRunRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        IReadOnlyList<RecipientResult> results = [.. request.Rows.Select(row =>
            new RecipientResult(row.Nik, row.Nama, row.Email, true, 1, null, null))];
        return Task.FromResult(new BatchRunResult(request.BatchId, results));
    }
}

```

The Task 4 factory already accepts coordinator/recovery/service overrides. Keep both overloads and use `ReaderWithRows` only in tests that explicitly need multiple rows.

- [ ] **Step 2: Run the three tests and verify resume behavior is missing**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~FailedRetry_Reuses|FullyQualifiedName~Recovery_Prepares|FullyQualifiedName~FingerprintMismatch"
```

Expected: FAIL because validation always creates a new batch and Confirm always uses `AttemptType.Normal`.

- [ ] **Step 3: Add resume fingerprint verification and eligible-row filtering**

After normal workbook validation succeeds and before assigning `_validRows`, use the original complete valid rows for fingerprint verification:

```csharp
IReadOnlyList<PayrollRow> validatedRows = validation.ValidRows;
if (_entry.IsResume)
{
    BatchContext context = new(Period, DateOnly.FromDateTime(PaymentDate!.Value));
    RecoveryGate gate = _recovery.VerifyFingerprint(_batchId, context, validatedRows);
    if (!gate.CanProceed)
    {
        ErrorMessage = Strings.Get("RecoveryFingerprintMismatch");
        return;
    }

    IReadOnlyList<string> eligibleNiks = _entry.Mode == PayrollRunMode.FailedRetry
        ? _recovery.SelectRetryFailedNiks(_batchId)
        : _recovery.SelectRecoveryResendNiks(_batchId);
    HashSet<string> eligible = new(eligibleNiks, StringComparer.OrdinalIgnoreCase);
    _validRows = [.. validatedRows.Where(row => eligible.Contains(row.Nik))];
    if (_validRows.Count == 0)
    {
        ErrorMessage = Strings.Get("RecoveryNoEligibleRecipients");
        return;
    }
}
else
{
    _validRows = validatedRows;
}
```

Only call `FindPreviouslySentNiks(Period)` for Normal mode. Only call `CreateBatchRecord()` for Normal mode. Resume mode retains the batch ID established by `Begin`.

- [ ] **Step 4: Send with the correct attempt type and just-in-time preparation**

At the start of the Confirm try block, before constructing `BatchRunRequest`:

```csharp
if (_entry.Mode == PayrollRunMode.RecoveryRetry)
{
    _recovery.PrepareForRecovery(_batchId);
}
```

Change the request's attempt argument:

```csharp
_entry.AttemptType,
```

Keep the filtered `_validRows`, original `_batchId`, and progress reporter unchanged.

- [ ] **Step 5: Add localized blocking copy**

Add these resource keys to both resource files:

```xml
<data name="RecoveryFingerprintMismatch" xml:space="preserve"><value>Fingerprint file tidak cocok dengan batch asli. Pilih file payroll yang sama.</value></data>
<data name="RecoveryNoEligibleRecipients" xml:space="preserve"><value>Tidak ada penerima yang memenuhi syarat untuk dikirim ulang.</value></data>
```

```xml
<data name="RecoveryFingerprintMismatch" xml:space="preserve"><value>The file fingerprint does not match the original batch. Select the same payroll file.</value></data>
<data name="RecoveryNoEligibleRecipients" xml:space="preserve"><value>No recipients are eligible for resend.</value></data>
```

- [ ] **Step 6: Run wizard, recovery, and coordinator tests**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~PayrollWizardViewModelTests
dotnet test tests/EunSlip.Core.Tests/EunSlip.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~RecoveryServiceTests|FullyQualifiedName~BatchCoordinatorTests"
```

Expected: tests PASS; no new batch is created for retry/recovery.

- [ ] **Step 7: Commit executable retry/recovery**

```powershell
git add src/EunSlip.Desktop/ViewModels/PayrollWizardViewModel.cs src/EunSlip.Desktop/Localization tests/EunSlip.Desktop.Tests/PayrollWizardViewModelTests.cs
git commit -m "feat: execute fingerprinted payroll recovery"
```

---

### Task 6: Separate Preview Generation from Shell Launch and Redesign Six Steps

**Files:**
- Modify: `src/EunSlip.Desktop/ViewModels/PayrollWizardViewModel.cs`
- Modify: `src/EunSlip.Desktop/Views/WizardView.xaml.cs`
- Modify: `src/EunSlip.Desktop/Views/WizardView.xaml`
- Modify: `tests/EunSlip.Desktop.Tests/PayrollWizardViewModelTests.cs`
- Modify: `tests/EunSlip.Desktop.Tests/WorkflowViewContractTests.cs`

**Interfaces:**
- Consumes: Task 4/5 wizard state and shared theme resources.
- Produces: `string? GeneratePreviewPdf()`, `bool CanGeneratePreview`, `ReportPreviewOpenFailure()`, a testable preview path, a six-step progress header, and non-clipping redesigned step content.

- [ ] **Step 1: Replace the environment-dependent preview test**

Replace `OpenPreview_GeneratesPdf_WhenValidRowsExist` with:

```csharp
[Fact]
public async Task GeneratePreviewPdf_ReturnsGeneratedPathWithoutLaunchingExternalViewer()
{
    PayrollWizardViewModel vm = Create(out _, hasStamp: true);
    FillSelectStep(vm);
    await vm.NextCommand.ExecuteAsync(null);
    vm.CurrentStep = WizardStep.Preview;

    string? path = vm.GeneratePreviewPdf();

    Assert.NotNull(path);
    Assert.True(File.Exists(path));
    Assert.Null(vm.ErrorMessage);
}
```

Extend the view contract test:

```csharp
[Fact]
public void WizardView_OpensOnlyPathGeneratedByViewModel()
{
    string code = ReadFile("src", "EunSlip.Desktop", "Views", "WizardView.xaml.cs");
    Assert.Contains("vm.GeneratePreviewPdf()", code, StringComparison.Ordinal);
    Assert.Contains("UseShellExecute = true", code, StringComparison.Ordinal);
    string viewModel = ReadFile("src", "EunSlip.Desktop", "ViewModels", "PayrollWizardViewModel.cs");
    Assert.DoesNotContain("Process.Start", viewModel, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run preview tests and verify direct shell launch remains**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~GeneratePreviewPdf|FullyQualifiedName~WizardView_OpensOnly"
```

Expected: FAIL because preview generation and shell launching are still combined in the view model.

- [ ] **Step 3: Implement the generation-only view-model method**

```csharp
public string? GeneratePreviewPdf()
{
    if (_validRows.Count == 0)
    {
        return null;
    }

    string? stampPath = _stampStore.GetActiveStampPath();
    if (string.IsNullOrEmpty(stampPath))
    {
        ErrorMessage = Strings.Get("ValidationBlockingMessage");
        return null;
    }

    PayrollRow first = _validRows[0];
    string tempDir = _tempFiles.CreateBatchTempDirectory(_batchId);
    string fileName = PayrollFormatting.BuildPayslipFileName(Period, first.Nik);
    string pdfPath = Path.Combine(tempDir, fileName);
    try
    {
        _pdfGenerator.Generate(new PayslipRequest(
            new BatchContext(Period, DateOnly.FromDateTime(PaymentDate!.Value)), first, stampPath), pdfPath);
        ErrorMessage = null;
        return pdfPath;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Preview generation failed");
        ErrorMessage = Strings.Get("PreviewGenerationFailed");
        return null;
    }
}

public bool CanGeneratePreview => CurrentStep == WizardStep.Preview && _validRows.Count > 0;

public void ReportPreviewOpenFailure()
{
    ErrorMessage = Strings.Get("PreviewOpenFailed");
}
```

Remove the old `[RelayCommand] OpenPreview` method and `CanOpenPreview` predicate. Raise `OnPropertyChanged(nameof(CanGeneratePreview))` from `OnCurrentStepChanged` and immediately after `_validRows` changes. Bind the button's `IsEnabled` to `CanGeneratePreview`; external launch belongs to the view boundary.

- [ ] **Step 4: Launch the generated path from `WizardView`**

```csharp
private void OpenPreview_Click(object sender, System.Windows.RoutedEventArgs e)
{
    if (DataContext is not PayrollWizardViewModel vm)
    {
        return;
    }

    string? path = vm.GeneratePreviewPdf();
    if (path is null)
    {
        return;
    }

    try
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
        {
            UseShellExecute = true,
        });
    }
    catch (Exception)
    {
        vm.ReportPreviewOpenFailure();
    }
}
```

Add localized `PreviewGenerationFailed` and `PreviewOpenFailed` keys.

- [ ] **Step 5: Recompose the Wizard XAML**

Replace the single title header with a six-item progress strip bound to `StepIndex`. Use style/DataTriggers to distinguish completed/current/upcoming states. Every step uses the page frame and a stable action footer. Required control changes:

- Select: bordered file action, two-column period/date, and visible retry/recovery context when `IsResumeMode` is true.
- Validate: summary badges, DataGrid row height from Theme, warning confirmation, and an eligible-recipient indicator in resume mode.
- Preview: `Click="OpenPreview_Click" IsEnabled="{Binding CanGeneratePreview}"`, real outlined border, standard subject/body fields, readiness text properties rather than raw Booleans.
- Confirm: summary card plus `GmailStatusText` and `StampStatusText`; no raw Boolean TextBlocks.
- Send: progress bar, current/total, sent/failed, current NIK hint/name, attempt count, and explicit no-close copy.
- Results: summary band plus result table with non-clipping rows.

The Confirm readiness rows use this exact form:

```xml
<Border Style="{StaticResource Card}">
    <StackPanel>
        <TextBlock Text="PRASYARAT KIRIM" Style="{StaticResource MicroLabel}" />
        <Grid Margin="0,16,0,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <TextBlock Text="Gmail terhubung" Style="{StaticResource BodyText}" />
            <Border Grid.Column="1" Style="{StaticResource StatusBadge}">
                <TextBlock Text="{Binding GmailStatusText}" Style="{StaticResource LabelText}" />
            </Border>
        </Grid>
        <Grid Margin="0,12,0,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <TextBlock Text="Stamp perusahaan" Style="{StaticResource BodyText}" />
            <Border Grid.Column="1" Style="{StaticResource StatusBadge}">
                <TextBlock Text="{Binding StampStatusText}" Style="{StaticResource LabelText}" />
            </Border>
        </Grid>
    </StackPanel>
</Border>
```

- [ ] **Step 6: Extend XAML contract assertions**

```csharp
[Fact]
public void WizardView_HasSixStepLabelsAndNoRawBooleanBindings()
{
    string xaml = ReadFile("src", "EunSlip.Desktop", "Views", "WizardView.xaml");
    foreach (string label in new[] { "PILIH", "VALIDASI", "EMAIL", "KONFIRMASI", "KIRIM", "HASIL" })
    {
        Assert.Contains(label, xaml, StringComparison.Ordinal);
    }
    Assert.DoesNotContain("Text=\"{Binding HasGmailConnection", xaml, StringComparison.Ordinal);
    Assert.DoesNotContain("Text=\"{Binding HasStamp", xaml, StringComparison.Ordinal);
    Assert.DoesNotContain("Text=\"{Binding GmailReady", xaml, StringComparison.Ordinal);
    Assert.DoesNotContain("Text=\"{Binding StampReady", xaml, StringComparison.Ordinal);
}
```

- [ ] **Step 7: Run all wizard tests and build**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~PayrollWizardViewModelTests|FullyQualifiedName~WorkflowViewContractTests"
dotnet build src/EunSlip.Desktop/EunSlip.Desktop.csproj --no-restore
```

Expected: tests PASS; the former external-viewer failure is removed; build succeeds with 0 warnings and 0 errors.

- [ ] **Step 8: Commit Wizard presentation and preview boundary**

```powershell
git add src/EunSlip.Desktop/ViewModels/PayrollWizardViewModel.cs src/EunSlip.Desktop/Views/WizardView.xaml src/EunSlip.Desktop/Views/WizardView.xaml.cs src/EunSlip.Desktop/Localization tests/EunSlip.Desktop.Tests
git commit -m "ui: redesign six-step payroll wizard"
```

---

### Task 7: Coordinate History Navigation and Lock Sending

**Files:**
- Modify: `src/EunSlip.Desktop/ViewModels/MainViewModel.cs`
- Modify: `src/EunSlip.Desktop/MainWindow.xaml.cs`
- Modify: `tests/EunSlip.Desktop.Tests/MainViewModelTests.cs`
- Create: `tests/EunSlip.Desktop.Tests/ShellFixture.cs`

**Interfaces:**
- Consumes: `HistoryViewModel.ResumeRequested`, `PayrollWizardViewModel.Begin`, and `PayrollWizardViewModel.IsSending`.
- Produces: History-to-wizard navigation, disabled navigation while sending, `CanClose`, and window close interception.

- [ ] **Step 1: Write failing shell coordination tests**

Create real child view models with the existing test fakes rather than passing `null`. First replace both Plan 1 constructions in `NavigationCommands_UpdatePersistentActiveSection` and `SettingsAndAboutCommands_UpdateTheirActiveFlags`:

```csharp
ShellFixture fixture = ShellFixture.Create();
MainViewModel vm = fixture.Main;
```

Then add:

```csharp
[Fact]
public void HistoryResumeRequest_OpensWizardWithOriginalModeAndBatch()
{
    ShellFixture fixture = ShellFixture.Create();
    Guid batchId = fixture.Repository.CreateBatch(fixture.InterruptedBatch());

    fixture.History.RecoverCommand.Execute(fixture.Repository.GetBatch(batchId));

    Assert.Equal(NavigationSection.Payroll, fixture.Main.CurrentSection);
    Assert.Equal(PayrollRunMode.RecoveryRetry, fixture.Wizard.RunMode);
}

[Fact]
public void SendingState_DisablesNavigationAndClose()
{
    ShellFixture fixture = ShellFixture.Create();
    fixture.Wizard.CurrentStep = WizardStep.Send;
    fixture.Wizard.IsBusy = true;

    Assert.False(fixture.Main.GoHomeCommand.CanExecute(null));
    Assert.False(fixture.Main.GoHistoryCommand.CanExecute(null));
    Assert.False(fixture.Main.CanClose);
}
```

`ShellFixture` owns one repository, recovery fake, Gmail fake, stamp fake, wizard, history, settings, home, about, and main view model. Its constructor uses the same fake behaviors already present in `PayrollWizardViewModelTests`; it never calls Gmail send.

Create the fixture with complete no-network dependencies:

```csharp
using EunSlip.Core.Batches;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Recovery;
using EunSlip.Core.Security;
using EunSlip.Core.Sending;
using EunSlip.Desktop.ViewModels;
using EunSlip.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace EunSlip.Desktop.Tests;

internal sealed class ShellFixture
{
    private ShellFixture()
    {
        Repository = new RepositoryFake();
        Recovery = new RecoveryFake(Repository);
        GmailFake gmail = new();
        StampFake stamp = new();
        Home = new HomeViewModel(gmail, stamp, Repository);
        Wizard = new PayrollWizardViewModel(
            new ReaderFake(), new CoordinatorFake(), new PdfFake(), gmail, stamp,
            Repository, new SecretFake(), new TempFake(), Recovery,
            NullLogger<PayrollWizardViewModel>.Instance);
        History = new HistoryViewModel(Repository, Recovery, NullLogger<HistoryViewModel>.Instance);
        Settings = new SettingsViewModel(gmail, stamp, Repository,
            NullLogger<SettingsViewModel>.Instance);
        About = new AboutViewModel(new AppPaths(
            Path.Combine(Path.GetTempPath(), "eunslip-shell-fixture")));
        Main = new MainViewModel(Home, Wizard, History, Settings, About);
    }

    public static ShellFixture Create() => new();

    public RepositoryFake Repository { get; }
    public RecoveryFake Recovery { get; }
    public HomeViewModel Home { get; }
    public PayrollWizardViewModel Wizard { get; }
    public HistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }
    public AboutViewModel About { get; }
    public MainViewModel Main { get; }

    public PayrollBatchRecord InterruptedBatch() => new(
        Guid.NewGuid(), "JULY 2025", new DateOnly(2026, 5, 11), "fp", BatchStatus.Interrupted,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, true, 1, 0, 0);

    internal sealed class RepositoryFake : IAppRepository
    {
        public List<PayrollBatchRecord> Batches { get; } = [];
        public List<BatchRecipientRecord> Recipients { get; } = [];
        public void Initialize() { }
        public bool CheckIntegrity() => true;
        public void ResetDatabase() { }
        public string? GetSetting(string key) => null;
        public void SetSetting(string key, string value) { }
        public Guid CreateBatch(PayrollBatchRecord batch) { Batches.Add(batch); return batch.Id; }
        public PayrollBatchRecord? GetBatch(Guid id) => Batches.FirstOrDefault(batch => batch.Id == id);
        public IReadOnlyList<PayrollBatchRecord> ListBatches() => Batches;
        public void UpdateBatchStatus(Guid id, BatchStatus status, DateTimeOffset? startedAtUtc, DateTimeOffset? completedAtUtc)
        {
            int index = Batches.FindIndex(batch => batch.Id == id);
            if (index >= 0)
            {
                PayrollBatchRecord batch = Batches[index];
                Batches[index] = batch with { Status = status, StartedAtUtc = startedAtUtc ?? batch.StartedAtUtc, CompletedAtUtc = completedAtUtc };
            }
        }
        public Guid AddRecipient(BatchRecipientRecord recipient) { Recipients.Add(recipient); return recipient.Id; }
        public IReadOnlyList<BatchRecipientRecord> ListRecipients(Guid batchId) =>
            [.. Recipients.Where(recipient => recipient.BatchId == batchId)];
        public IReadOnlyList<SendAttemptRecord> ListAttempts(Guid batchId) => [];
        public void UpdateRecipientStatus(Guid recipientId, RecipientStatus status, DateTimeOffset updatedAtUtc) { }
        public void AddAttempt(SendAttemptRecord attempt) { }
        public void CompleteAttempt(Guid attemptId, AttemptStatus status, DateTimeOffset completedAtUtc, string? errorCategory, string? errorMessage, string? gmailMessageId) { }
        public AttemptStatus? GetLatestAttemptStatus(Guid recipientId) => null;
        public IReadOnlyList<Guid> FindInterruptedBatches() => [];
        public void ResetSendingRecipientsToPending(Guid batchId) { }
        public IReadOnlyList<string> FindPreviouslySentNiks(string period) => [];
        public void DeleteBatch(Guid id) { }
    }

    internal sealed class RecoveryFake(RepositoryFake repository) : IRecoveryService
    {
        public List<Guid> Prepared { get; } = [];
        public IReadOnlyList<Guid> DetectInterruptedBatches() => [];
        public IReadOnlyList<Guid> MarkDetectedBatchesInterrupted() => [];
        public void PrepareForRecovery(Guid batchId) => Prepared.Add(batchId);
        public RecoveryGate VerifyFingerprint(Guid batchId, BatchContext context, IReadOnlyList<PayrollRow> rows) =>
            new(RecoveryGateResult.Match, "fp", "fp");
        public IReadOnlyList<string> SelectRetryFailedNiks(Guid batchId) => [];
        public IReadOnlyList<string> SelectRecoveryResendNiks(Guid batchId) =>
            [.. repository.ListRecipients(batchId).Where(recipient => recipient.Status != RecipientStatus.Sent)
                .Select(recipient => recipient.EncryptedNik)];
    }

    private sealed class ReaderFake : IPayrollWorkbookReader
    {
        public WorkbookReadResult Read(string filePath) => throw new NotSupportedException();
    }

    private sealed class CoordinatorFake : IBatchCoordinator
    {
        public Task<BatchRunResult> RunBatchAsync(BatchRunRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new BatchRunResult(request.BatchId, []));
    }

    private sealed class PdfFake : IPayslipPdfGenerator
    {
        public void Generate(PayslipRequest request, string outputPath) { }
    }

    private sealed class GmailFake : IGmailAuthorization
    {
        public Task<GoogleAccount?> ConnectAsync(string clientSecretJson, CancellationToken cancellationToken) =>
            Task.FromResult<GoogleAccount?>(new GoogleAccount("payroll@example.com"));
        public Task<GoogleAccount?> RestoreAsync(CancellationToken cancellationToken) =>
            Task.FromResult<GoogleAccount?>(new GoogleAccount("payroll@example.com"));
        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> IsConnectedAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class StampFake : ISharedFileStore
    {
        public string? GetActiveStampPath() => "stamp.png";
        public string ImportStamp(string sourcePath) => "stamp.png";
        public void RemoveStamp() { }
    }

    private sealed class SecretFake : ISecretStore
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string envelope) => envelope;
    }

    private sealed class TempFake : ITempFileService
    {
        public string CreateBatchTempDirectory(Guid batchId) => Path.GetTempPath();
        public void DeleteFile(string path) { }
        public void CleanupLeftovers() { }
    }
}
```

- [ ] **Step 2: Run shell tests and verify coordination/locking is absent**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~MainViewModelTests
```

Expected: FAIL because History is not wired to Main and navigation ignores sending state.

- [ ] **Step 3: Wire resume navigation and command predicates**

In `MainViewModel` constructor:

```csharp
_history.ResumeRequested += OpenWizard;
_wizard.PropertyChanged += (_, args) =>
{
    if (args.PropertyName == nameof(PayrollWizardViewModel.IsSending))
    {
        OnPropertyChanged(nameof(CanClose));
        GoHomeCommand.NotifyCanExecuteChanged();
        GoPayrollCommand.NotifyCanExecuteChanged();
        GoHistoryCommand.NotifyCanExecuteChanged();
        GoSettingsCommand.NotifyCanExecuteChanged();
        GoAboutCommand.NotifyCanExecuteChanged();
    }
};
```

Use these members:

```csharp
public bool CanClose => !_wizard.IsSending;

private bool CanNavigate() => !_wizard.IsSending;

private void OpenWizard(PayrollWizardEntry entry)
{
    if (_wizard.Begin(entry))
    {
        Navigate(_wizard, NavigationSection.Payroll);
    }
}
```

Add `CanExecute = nameof(CanNavigate)` to every navigation command. Normal payroll calls `OpenWizard(PayrollWizardEntry.Normal())` rather than `Reset()` directly.

- [ ] **Step 4: Intercept window close while sending**

```csharp
using System.ComponentModel;
using System.Windows;
using EunSlip.Desktop.ViewModels;

namespace EunSlip.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not MainViewModel { CanClose: false })
        {
            return;
        }

        e.Cancel = true;
        _ = MessageBox.Show(
            "Pengiriman sedang berlangsung. Tunggu sampai proses selesai.",
            "EunSlip",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
```

- [ ] **Step 5: Run Main, History, and Wizard tests**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~MainViewModelTests|FullyQualifiedName~HistoryViewModelTests|FullyQualifiedName~PayrollWizardViewModelTests"
```

Expected: tests PASS; navigation is enabled outside Send and disabled during Send.

- [ ] **Step 6: Commit navigation coordination and sending lock**

```powershell
git add src/EunSlip.Desktop/ViewModels/MainViewModel.cs src/EunSlip.Desktop/MainWindow.xaml.cs tests/EunSlip.Desktop.Tests/MainViewModelTests.cs tests/EunSlip.Desktop.Tests/ShellFixture.cs
git commit -m "fix: lock navigation during payroll send"
```

---

### Task 8: Workflow and History Checkpoint

**Files:**
- Verify only; add regression fixes only when a failing check demonstrates a concrete defect.

**Interfaces:**
- Consumes: Tasks 1–7.
- Produces: a green and E2E-confirmed TASK 13/14 workflow ready for PDF redesign.

- [ ] **Step 1: Run all Core, Infrastructure, and Desktop tests**

```powershell
dotnet test EunSlip.slnx --no-restore
```

Expected: all tests PASS; 0 failed. The prior `OpenPreview_GeneratesPdf_WhenValidRowsExist` environment failure no longer exists.

- [ ] **Step 2: Build the complete solution**

```powershell
dotnet build EunSlip.slnx --no-restore
```

Expected: build succeeds with 0 warnings and 0 errors.

- [ ] **Step 3: Reproduce normal flow through Langkah 4**

Launch:

```powershell
src\EunSlip.Desktop\bin\Debug\net10.0-windows\EunSlip.Desktop.exe
```

Using Windows Computer Use and the dummy workbook, navigate Select → Validate → Preview → Confirm. Verify Gmail/stamp display `Siap` when Settings shows them ready and `KONFIRMASI & KIRIM` is enabled. Do not click the send button.

- [ ] **Step 4: Verify History UI state without external side effects**

Select a batch and confirm that the detail table remains visible, placeholder disappears, latest attempt metadata appears, and Retry/Recover enablement matches batch state. Navigate into retry/recovery only far enough to verify mode/context and fingerprint gating. Do not delete batches and do not confirm send.

- [ ] **Step 5: Review database mutation evidence**

Before and after merely opening recovery mode, compare batch/recipient state through repository tests or a disposable test database. Expected: no recipient reset occurs until `ConfirmSendCommand` is executed for RecoveryRetry in a fake-coordinator test.

- [ ] **Step 6: Commit only demonstrated checkpoint fixes**

If Steps 1–5 expose a reproducible defect, first add a focused regression test, apply the smallest fix, rerun all steps, and commit the touched files:

```powershell
git add src tests
git commit -m "fix: close payroll workflow regressions"
```

If every check passes, do not create an empty commit.
