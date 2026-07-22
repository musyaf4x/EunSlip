# EunSlip Design System, Shell, and Static Pages Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace EunSlip's weak visual shell and passive static pages with a cohesive, professional WPF design system and operational Home, Settings, and About experiences.

**Architecture:** Preserve the existing WPF MVVM composition and singleton view models. Centralize visual behavior in `Theme.xaml`, keep page state in the existing view models, and use `MainViewModel` as the sole owner of the active navigation section. This plan does not yet change retry/recovery or payroll sending behavior; those are handled by the workflow plan.

**Tech Stack:** .NET 10, WPF XAML, CommunityToolkit.Mvvm 8.4.2, xUnit 2.9.3.

## Global Constraints

- Baseline viewport: `1180 × 760`; minimum supported window: `1024 × 680`.
- UI font family: installed Windows `Segoe UI`; no bundled font dependency.
- Colors: Carbon Ink `#1A211E`, Paper White `#FFFFFF`, Obsidian `#0C0C0C`, Fog `#EEF1F0`, Mist `#E0E0E0`, Graphite `#606562`, Ash Border `#CCCFCD`, Slate `#363537`, Ember Red `#CC2E39`.
- Inputs, buttons, and navigation use 4 DIP radius; cards use 8 DIP radius; badges use pill radius.
- No gradients, shadows, faux elevation, decorative animation, or third-party UI framework.
- Body/form type is 14–16 DIPs; page titles are 28–32 DIPs; short UI labels are bold and uppercase.
- Never expose raw `True` or `False` strings in user-facing UI.
- All implementation uses the existing Core/Infrastructure boundaries and existing installed dependencies.
- During TDD cycles use scoped tests and `dotnet build ... --no-restore`; run the full suite only at plan checkpoints.
- Do not transmit payroll email during implementation or E2E review.

## File Map

### Create

- `tests/EunSlip.Desktop.Tests/ThemeContractTests.cs` — structural contract for required theme resources and control-template bindings.
- `tests/EunSlip.Desktop.Tests/MainViewModelTests.cs` — active navigation state regression tests.
- `tests/EunSlip.Desktop.Tests/HomeViewModelTests.cs` — Home loading, recent batch, interrupted batch, and status presentation tests.

### Modify

- `src/EunSlip.Desktop/Theme.xaml` — authoritative tokens, typography, focus, button, card, field, badge, and table styles.
- `src/EunSlip.Desktop/MainWindow.xaml` — minimum dimensions, redesigned navigation rail, active states, and shell metadata.
- `src/EunSlip.Desktop/ViewModels/MainViewModel.cs` — explicit `NavigationSection` and derived active-state properties.
- `src/EunSlip.Desktop/ViewModels/HomeViewModel.cs` — real repository-backed operational summary and localized status text.
- `src/EunSlip.Desktop/Views/HomeView.xaml` — primary payroll CTA, readiness cards, interruption callout, and recent batch panel.
- `src/EunSlip.Desktop/ViewModels/SettingsViewModel.cs` — loading/status presentation and stamp-removal confirmation state.
- `src/EunSlip.Desktop/Views/SettingsView.xaml` — compact two-column settings layout and disclosures.
- `src/EunSlip.Desktop/Views/AboutView.xaml` — compact identity, attribution, legal, and diagnostics composition.
- `src/EunSlip.Desktop/Localization/Strings.resx` — Indonesian copy introduced by this plan.
- `src/EunSlip.Desktop/Localization/Strings.en.resx` — English equivalents for every new resource key.
- `tests/EunSlip.Desktop.Tests/SettingsViewModelTests.cs` — loading labels and stamp-removal confirmation tests.

---

### Task 1: Establish the Visual Resource Contract

**Files:**
- Create: `tests/EunSlip.Desktop.Tests/ThemeContractTests.cs`
- Modify: `src/EunSlip.Desktop/Theme.xaml`

**Interfaces:**
- Consumes: WPF `ResourceDictionary` conventions already loaded by `App.xaml`.
- Produces: `PageFrame`, `PageTitle`, `PageSubtitle`, `StatusBadge`, `DangerButton`, `NavButton`, `PrimaryButton`, `OutlinedButton`, `FieldInput`, `FieldPicker`, `FieldCombo`, `Card`, and `EditorialDataGrid` resources used by all later page tasks.

- [ ] **Step 1: Write the failing theme contract test**

```csharp
using System.Text;

namespace EunSlip.Desktop.Tests;

public sealed class ThemeContractTests
{
    private static string ReadRepositoryFile(params string[] parts)
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
    public void Theme_DefinesRedesignResourcesAndBindsButtonBorders()
    {
        string xaml = ReadRepositoryFile("src", "EunSlip.Desktop", "Theme.xaml");

        foreach (string key in new[]
        {
            "PageFrame", "PageTitle", "PageSubtitle", "StatusBadge", "DangerButton",
            "NavButton", "PrimaryButton", "OutlinedButton", "FieldInput", "FieldPicker",
            "FieldCombo", "Card", "EditorialDataGrid",
        })
        {
            Assert.Contains($"x:Key=\"{key}\"", xaml, StringComparison.Ordinal);
        }

        Assert.Contains("BorderBrush=\"{TemplateBinding BorderBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BorderThickness=\"{TemplateBinding BorderThickness}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight\" Value=\"40\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DropShadowEffect", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("LinearGradientBrush", xaml, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the scoped test and verify the contract fails**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~ThemeContractTests
```

Expected: FAIL because `PageFrame`, `PageTitle`, `StatusBadge`, `DangerButton`, and the button border template bindings are absent.

- [ ] **Step 3: Replace the typography and shared control styles**

Keep the existing color resources, then replace the typography/control portion of `Theme.xaml` with resources matching this contract. The button template must include the declared border properties:

```xml
<FontFamily x:Key="BodyFont">Segoe UI</FontFamily>
<FontFamily x:Key="TechnicalFont">Consolas</FontFamily>

<Thickness x:Key="PagePadding">40,32</Thickness>
<CornerRadius x:Key="RadiusSm">4</CornerRadius>
<CornerRadius x:Key="RadiusMd">8</CornerRadius>
<CornerRadius x:Key="RadiusPill">999</CornerRadius>

<Style x:Key="PageFrame" TargetType="Grid">
    <Setter Property="Margin" Value="40,32" />
</Style>
<Style x:Key="PageTitle" TargetType="TextBlock">
    <Setter Property="FontFamily" Value="{StaticResource BodyFont}" />
    <Setter Property="FontSize" Value="30" />
    <Setter Property="FontWeight" Value="Bold" />
    <Setter Property="Foreground" Value="{StaticResource CarbonInkBrush}" />
</Style>
<Style x:Key="PageSubtitle" TargetType="TextBlock">
    <Setter Property="FontFamily" Value="{StaticResource BodyFont}" />
    <Setter Property="FontSize" Value="15" />
    <Setter Property="LineHeight" Value="22" />
    <Setter Property="Foreground" Value="{StaticResource GraphiteBrush}" />
</Style>

<Style x:Key="GhostButton" TargetType="Button">
    <Setter Property="FontFamily" Value="{StaticResource BodyFont}" />
    <Setter Property="FontSize" Value="14" />
    <Setter Property="FontWeight" Value="Bold" />
    <Setter Property="Foreground" Value="{StaticResource CarbonInkBrush}" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderBrush" Value="Transparent" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="MinHeight" Value="40" />
    <Setter Property="Padding" Value="16,9" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="{StaticResource RadiusSm}"
                        Padding="{TemplateBinding Padding}">
                    <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                      VerticalAlignment="Center" />
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<Style x:Key="StatusBadge" TargetType="Border">
    <Setter Property="Background" Value="{StaticResource FogBrush}" />
    <Setter Property="BorderBrush" Value="{StaticResource AshBorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="CornerRadius" Value="{StaticResource RadiusPill}" />
    <Setter Property="Padding" Value="10,5" />
</Style>

<Style x:Key="DangerButton" TargetType="Button" BasedOn="{StaticResource OutlinedButton}">
    <Setter Property="Foreground" Value="{StaticResource EmberRedBrush}" />
    <Setter Property="BorderBrush" Value="{StaticResource EmberRedBrush}" />
</Style>
```

Also raise body text to 15 DIPs, micro labels to 12 DIPs bold uppercase, fields/buttons to a minimum height of 40 DIPs, DataGrid rows to 40 DIPs, and column headers to 42 DIPs. Preserve the existing 4/8 radius policy and absence of shadows.

- [ ] **Step 4: Run the theme test and build the desktop project**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~ThemeContractTests
dotnet build src/EunSlip.Desktop/EunSlip.Desktop.csproj --no-restore
```

Expected: test PASS; build succeeds with 0 warnings and 0 errors.

- [ ] **Step 5: Commit the visual resource contract**

```powershell
git add src/EunSlip.Desktop/Theme.xaml tests/EunSlip.Desktop.Tests/ThemeContractTests.cs
git commit -m "ui: establish EunSlip redesign tokens"
```

---

### Task 2: Redesign the Shell and Persist Active Navigation

**Files:**
- Create: `tests/EunSlip.Desktop.Tests/MainViewModelTests.cs`
- Modify: `src/EunSlip.Desktop/ViewModels/MainViewModel.cs`
- Modify: `src/EunSlip.Desktop/MainWindow.xaml`

**Interfaces:**
- Consumes: theme resources from Task 1 and the existing child view-model singletons.
- Produces: `NavigationSection CurrentSection`, `IsHomeActive`, `IsPayrollActive`, `IsHistoryActive`, `IsSettingsActive`, `IsAboutActive`, `ActiveLanguage`, and `VersionText`.

- [ ] **Step 1: Write failing active-navigation tests**

```csharp
using EunSlip.Desktop.ViewModels;

namespace EunSlip.Desktop.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void NavigationCommands_UpdatePersistentActiveSection()
    {
        MainViewModel vm = new(null!, null!, null!, null!, null!);

        Assert.Equal(NavigationSection.Home, vm.CurrentSection);
        Assert.True(vm.IsHomeActive);

        vm.GoHistoryCommand.Execute(null);

        Assert.Equal(NavigationSection.History, vm.CurrentSection);
        Assert.True(vm.IsHistoryActive);
        Assert.False(vm.IsHomeActive);
    }

    [Fact]
    public void SettingsAndAboutCommands_UpdateTheirActiveFlags()
    {
        MainViewModel vm = new(null!, null!, null!, null!, null!);

        vm.GoSettingsCommand.Execute(null);
        Assert.True(vm.IsSettingsActive);

        vm.GoAboutCommand.Execute(null);
        Assert.True(vm.IsAboutActive);
        Assert.False(vm.IsSettingsActive);
    }
}
```

- [ ] **Step 2: Run the tests and verify missing navigation state**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~MainViewModelTests
```

Expected: FAIL because `NavigationSection` and the active-state properties do not exist.

- [ ] **Step 3: Add explicit navigation state to `MainViewModel`**

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Desktop.Localization;

namespace EunSlip.Desktop.ViewModels;

public enum NavigationSection { Home, Payroll, History, Settings, About }

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly HomeViewModel _home;
    private readonly PayrollWizardViewModel _wizard;
    private readonly HistoryViewModel _history;
    private readonly SettingsViewModel _settings;
    private readonly AboutViewModel _about;

    [ObservableProperty]
    private ViewModelBase _current;

    [ObservableProperty]
    private NavigationSection _currentSection = NavigationSection.Home;

    public MainViewModel(HomeViewModel home, PayrollWizardViewModel wizard, HistoryViewModel history,
        SettingsViewModel settings, AboutViewModel about)
    {
        _home = home;
        _wizard = wizard;
        _history = history;
        _settings = settings;
        _about = about;
        _current = home;
    }

    public string NavHome => Strings.Get("Nav_Home");
    public string NavPayrollProcess => Strings.Get("Nav_PayrollProcess");
    public string NavHistory => Strings.Get("Nav_History");
    public string NavSettings => Strings.Get("Nav_Settings");
    public string NavAbout => Strings.Get("Nav_About");
    public string ActiveLanguage => CultureInfo.CurrentUICulture.Name;
    public string VersionText => "v1.0.0";
    public bool IsHomeActive => CurrentSection == NavigationSection.Home;
    public bool IsPayrollActive => CurrentSection == NavigationSection.Payroll;
    public bool IsHistoryActive => CurrentSection == NavigationSection.History;
    public bool IsSettingsActive => CurrentSection == NavigationSection.Settings;
    public bool IsAboutActive => CurrentSection == NavigationSection.About;

    [RelayCommand]
    private void GoHome() => Navigate(_home, NavigationSection.Home);

    [RelayCommand]
    private void GoPayroll()
    {
        _wizard.Reset();
        Navigate(_wizard, NavigationSection.Payroll);
    }

    [RelayCommand]
    private void GoHistory() => Navigate(_history, NavigationSection.History);

    [RelayCommand]
    private void GoSettings() => Navigate(_settings, NavigationSection.Settings);

    [RelayCommand]
    private void GoAbout() => Navigate(_about, NavigationSection.About);

    private void Navigate(ViewModelBase target, NavigationSection section)
    {
        Current = target;
        CurrentSection = section;
    }

    partial void OnCurrentSectionChanged(NavigationSection value)
    {
        OnPropertyChanged(nameof(IsHomeActive));
        OnPropertyChanged(nameof(IsPayrollActive));
        OnPropertyChanged(nameof(IsHistoryActive));
        OnPropertyChanged(nameof(IsSettingsActive));
        OnPropertyChanged(nameof(IsAboutActive));
    }
}
```

- [ ] **Step 4: Recompose the navigation rail in `MainWindow.xaml`**

Set `MinWidth="1024"` and `MinHeight="680"`. Use a 240-DIP rail, Carbon Ink active items, persistent DataTriggers, and shell metadata. Each navigation button follows this exact pattern with its matching active property:

```xml
<Button Content="{Binding NavHome}" Command="{Binding GoHomeCommand}" Margin="0,2">
    <Button.Style>
        <Style TargetType="Button" BasedOn="{StaticResource NavButton}">
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsHomeActive}" Value="True">
                    <Setter Property="Background" Value="{StaticResource CarbonInkBrush}" />
                    <Setter Property="Foreground" Value="{StaticResource PaperWhiteBrush}" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Button.Style>
</Button>
```

The rail footer must use these bindings rather than hard-coded environment state:

```xml
<StackPanel Grid.Row="2" Margin="24,16,24,24">
    <Border BorderBrush="{StaticResource MistBrush}" BorderThickness="0,1,0,0" Margin="0,0,0,16" />
    <TextBlock Text="{Binding ActiveLanguage}" Style="{StaticResource MicroLabel}" />
    <TextBlock Text="{Binding VersionText}" Style="{StaticResource MutedText}" Margin="0,4,0,12" />
    <Image Source="/Assets/vierth.png" Height="22" HorizontalAlignment="Left"
           RenderOptions.BitmapScalingMode="HighQuality" />
    <TextBlock Text="DEVELOPED BY VIERTH LABS" Style="{StaticResource MicroLabel}" Margin="0,8,0,0" />
</StackPanel>
```

- [ ] **Step 5: Run the tests and desktop build**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~MainViewModelTests
dotnet build src/EunSlip.Desktop/EunSlip.Desktop.csproj --no-restore
```

Expected: tests PASS; build succeeds with 0 warnings and 0 errors.

- [ ] **Step 6: Commit the shell redesign**

```powershell
git add src/EunSlip.Desktop/MainWindow.xaml src/EunSlip.Desktop/ViewModels/MainViewModel.cs tests/EunSlip.Desktop.Tests/MainViewModelTests.cs
git commit -m "ui: redesign application shell"
```

---

### Task 3: Turn Home into the Operational Entry Page

**Files:**
- Create: `tests/EunSlip.Desktop.Tests/HomeViewModelTests.cs`
- Modify: `src/EunSlip.Desktop/ViewModels/HomeViewModel.cs`
- Modify: `src/EunSlip.Desktop/Views/HomeView.xaml`
- Modify: `src/EunSlip.Desktop/Localization/Strings.resx`
- Modify: `src/EunSlip.Desktop/Localization/Strings.en.resx`

**Interfaces:**
- Consumes: `IGmailAuthorization`, `ISharedFileStore`, and `IAppRepository.ListBatches/GetSetting`.
- Produces: `IsLoading`, `GmailStatusText`, `StampStatusText`, `ActiveLanguage`, `RecentBatchSummary`, `InterruptedBatchNotice`, and `HasInterruptedBatch`.

- [ ] **Step 1: Write failing Home state tests**

```csharp
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Sending;
using EunSlip.Desktop.ViewModels;

namespace EunSlip.Desktop.Tests;

public sealed class HomeViewModelTests
{
    private sealed class Repository : IAppRepository
    {
        public List<PayrollBatchRecord> Batches { get; } = [];
        public Dictionary<string, string> Settings { get; } = [];
        public void Initialize() { }
        public bool CheckIntegrity() => true;
        public void ResetDatabase() { }
        public string? GetSetting(string key) => Settings.GetValueOrDefault(key);
        public void SetSetting(string key, string value) => Settings[key] = value;
        public Guid CreateBatch(PayrollBatchRecord batch) { Batches.Add(batch); return batch.Id; }
        public PayrollBatchRecord? GetBatch(Guid id) => Batches.FirstOrDefault(x => x.Id == id);
        public IReadOnlyList<PayrollBatchRecord> ListBatches() => [.. Batches.OrderByDescending(x => x.CreatedAtUtc)];
        public void UpdateBatchStatus(Guid id, BatchStatus status, DateTimeOffset? startedAtUtc, DateTimeOffset? completedAtUtc) { }
        public Guid AddRecipient(BatchRecipientRecord recipient) => recipient.Id;
        public IReadOnlyList<BatchRecipientRecord> ListRecipients(Guid batchId) => [];
        public void UpdateRecipientStatus(Guid recipientId, RecipientStatus status, DateTimeOffset updatedAtUtc) { }
        public void AddAttempt(SendAttemptRecord attempt) { }
        public void CompleteAttempt(Guid attemptId, AttemptStatus status, DateTimeOffset completedAtUtc, string? errorCategory, string? errorMessage, string? gmailMessageId) { }
        public AttemptStatus? GetLatestAttemptStatus(Guid recipientId) => null;
        public IReadOnlyList<Guid> FindInterruptedBatches() => [];
        public void ResetSendingRecipientsToPending(Guid batchId) { }
        public IReadOnlyList<string> FindPreviouslySentNiks(string period) => [];
        public void DeleteBatch(Guid id) { }
    }

    private sealed class Gmail : IGmailAuthorization
    {
        public Task<GoogleAccount?> ConnectAsync(string clientSecretJson, CancellationToken cancellationToken) =>
            Task.FromResult<GoogleAccount?>(new GoogleAccount("payroll@example.com"));
        public Task<GoogleAccount?> RestoreAsync(CancellationToken cancellationToken) =>
            Task.FromResult<GoogleAccount?>(new GoogleAccount("payroll@example.com"));
        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> IsConnectedAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class Stamp : ISharedFileStore
    {
        public string? GetActiveStampPath() => "stamp.png";
        public string ImportStamp(string sourcePath) => "stamp.png";
        public void RemoveStamp() { }
    }

    [Fact]
    public async Task Loaded_UsesLocalizedStatusesAndLatestBatch()
    {
        Repository repository = new();
        repository.Settings["UiLanguage"] = "id-ID";
        repository.Batches.Add(new PayrollBatchRecord(Guid.NewGuid(), "JULI 2026", new DateOnly(2026, 7, 22),
            "fp", BatchStatus.Completed, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, true, 2, 2, 0));
        HomeViewModel vm = new(new Gmail(), new Stamp(), repository);

        await vm.LoadedCommand.ExecuteAsync(null);

        Assert.Equal(EunSlip.Desktop.Localization.Strings.Get("StatusReady"), vm.GmailStatusText);
        Assert.Equal(EunSlip.Desktop.Localization.Strings.Get("StatusReady"), vm.StampStatusText);
        Assert.Equal("id-ID", vm.ActiveLanguage);
        Assert.Contains("JULI 2026", vm.RecentBatchSummary);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Loaded_ExposesInterruptedBatchNotice()
    {
        Repository repository = new();
        repository.Batches.Add(new PayrollBatchRecord(Guid.NewGuid(), "JUNI 2026", new DateOnly(2026, 6, 22),
            "fp", BatchStatus.Interrupted, DateTimeOffset.UtcNow, null, null, true, 2, 1, 0));
        HomeViewModel vm = new(new Gmail(), new Stamp(), repository);

        await vm.LoadedCommand.ExecuteAsync(null);

        Assert.True(vm.HasInterruptedBatch);
        Assert.Contains("JUNI 2026", vm.InterruptedBatchNotice);
    }
}
```

- [ ] **Step 2: Run the Home tests and verify constructor/state failures**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~HomeViewModelTests
```

Expected: FAIL because the repository dependency and status properties do not exist.

- [ ] **Step 3: Implement repository-backed Home state**

Update the constructor to accept `IAppRepository repository`. Implement the state calculation with this shape:

```csharp
[ObservableProperty]
private bool _isLoading;

[ObservableProperty]
private string _gmailStatusText = Strings.Get("StatusChecking");

[ObservableProperty]
private string _stampStatusText = Strings.Get("StatusChecking");

[ObservableProperty]
private string _activeLanguage = "id-ID";

public bool HasInterruptedBatch => !string.IsNullOrEmpty(InterruptedBatchNotice);

public async Task RefreshAsync(CancellationToken cancellationToken)
{
    IsLoading = true;
    GmailStatusText = Strings.Get("StatusChecking");
    StampStatusText = Strings.Get("StatusChecking");
    try
    {
        GoogleAccount? account = await _gmail.RestoreAsync(cancellationToken);
        ConnectedGmail = account?.Email;
        HasGmailConnection = account is not null;
        HasStamp = _stampStore.GetActiveStampPath() is not null;
        GmailStatusText = HasGmailConnection ? Strings.Get("StatusReady") : Strings.Get("StatusNotReady");
        StampStatusText = HasStamp ? Strings.Get("StatusReady") : Strings.Get("StatusNotReady");
        ActiveLanguage = _repository.GetSetting("UiLanguage") ?? "id-ID";

        IReadOnlyList<PayrollBatchRecord> batches = _repository.ListBatches();
        PayrollBatchRecord? latest = batches.FirstOrDefault();
        RecentBatchSummary = latest is null
            ? Strings.Get("HomeNoRecentBatch")
            : $"{latest.Period} · {latest.Status} · {latest.SentCount}/{latest.RecipientCount}";

        PayrollBatchRecord? interrupted = batches.FirstOrDefault(x => x.Status == BatchStatus.Interrupted);
        InterruptedBatchNotice = interrupted is null
            ? null
            : string.Format(Strings.Get("HomeInterruptedBatch"), interrupted.Period);
        OnPropertyChanged(nameof(HasInterruptedBatch));
    }
    finally
    {
        IsLoading = false;
    }
}
```

Add Indonesian and English values for `StatusChecking`, `StatusReady`, `StatusNotReady`, `HomeNoRecentBatch`, and `HomeInterruptedBatch` in both resource files.

- [ ] **Step 4: Replace Home with a strong operational composition**

Use an Obsidian hero panel with a white primary action bound to the shell command, followed by readiness cards and recent/interrupted state:

```xml
<Border Grid.Row="0" Background="{StaticResource ObsidianBrush}" CornerRadius="8" Padding="32">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="Auto" />
        </Grid.ColumnDefinitions>
        <StackPanel>
            <TextBlock Text="EUNSLIP PAYROLL DELIVERY" Foreground="{StaticResource PaperWhiteBrush}"
                       FontFamily="{StaticResource BodyFont}" FontSize="12" FontWeight="Bold" />
            <TextBlock Text="Proses slip gaji dengan alur yang terverifikasi."
                       Foreground="{StaticResource PaperWhiteBrush}" FontFamily="{StaticResource BodyFont}"
                       FontSize="30" FontWeight="Bold" TextWrapping="Wrap" Margin="0,12,24,0" />
        </StackPanel>
        <Button Grid.Column="1" Content="PROSES PAYROLL"
                Command="{Binding DataContext.GoPayrollCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                Background="{StaticResource PaperWhiteBrush}" Foreground="{StaticResource CarbonInkBrush}"
                Style="{StaticResource PrimaryButton}" Padding="20,12" VerticalAlignment="Center" />
    </Grid>
</Border>
```

Readiness cards bind to `GmailStatusText`, `ConnectedGmail`, `StampStatusText`, and `ActiveLanguage`. The recent panel binds to `RecentBatchSummary`. The interruption panel binds to `InterruptedBatchNotice` and uses `HasInterruptedBatch` with `BooleanToVisibilityConverter`; do not bind a string to a Boolean converter.

- [ ] **Step 5: Run Home and Settings regression tests plus desktop build**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~HomeViewModelTests|FullyQualifiedName~SettingsViewModelTests"
dotnet build src/EunSlip.Desktop/EunSlip.Desktop.csproj --no-restore
```

Expected: tests PASS; build succeeds with 0 warnings and 0 errors.

- [ ] **Step 6: Commit the Home redesign**

```powershell
git add src/EunSlip.Desktop/ViewModels/HomeViewModel.cs src/EunSlip.Desktop/Views/HomeView.xaml src/EunSlip.Desktop/Localization/Strings.resx src/EunSlip.Desktop/Localization/Strings.en.resx tests/EunSlip.Desktop.Tests/HomeViewModelTests.cs
git commit -m "ui: make Home an operational dashboard"
```

---

### Task 4: Compact and Clarify Settings

**Files:**
- Modify: `src/EunSlip.Desktop/ViewModels/SettingsViewModel.cs`
- Modify: `src/EunSlip.Desktop/Views/SettingsView.xaml`
- Modify: `src/EunSlip.Desktop/Localization/Strings.resx`
- Modify: `src/EunSlip.Desktop/Localization/Strings.en.resx`
- Modify: `tests/EunSlip.Desktop.Tests/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: current Gmail, stamp, repository, DPAPI, and language behavior.
- Produces: `IsLoading`, `GmailStatusText`, `StampStatusText`, `OAuthStatusText`, `IsRemoveStampConfirmationVisible`, `RequestRemoveStampCommand`, `ConfirmRemoveStampCommand`, and `CancelRemoveStampCommand`.

- [ ] **Step 1: Add failing loading and removal-confirmation tests**

```csharp
[Fact]
public async Task Loaded_UsesProfessionalStatusLabels()
{
    SettingsViewModel vm = Create(out _, connected: true, hasStamp: true);

    await vm.LoadedCommand.ExecuteAsync(null);

    Assert.Equal(EunSlip.Desktop.Localization.Strings.Get("StatusReady"), vm.GmailStatusText);
    Assert.Equal(EunSlip.Desktop.Localization.Strings.Get("StatusReady"), vm.StampStatusText);
    Assert.False(vm.IsLoading);
}

[Fact]
public void RemoveStamp_RequiresExplicitConfirmation()
{
    SettingsViewModel vm = Create(out _, hasStamp: true);

    vm.RequestRemoveStampCommand.Execute(null);
    Assert.True(vm.IsRemoveStampConfirmationVisible);
    Assert.True(vm.HasStamp);

    vm.ConfirmRemoveStampCommand.Execute(null);
    Assert.False(vm.IsRemoveStampConfirmationVisible);
    Assert.False(vm.HasStamp);
}
```

- [ ] **Step 2: Run the tests and verify missing presentation state**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~SettingsViewModelTests
```

Expected: FAIL because the new loading/status/confirmation members do not exist.

- [ ] **Step 3: Implement loading labels and two-stage stamp deletion**

Add `using EunSlip.Desktop.Localization;` to the view-model imports, then implement:

```csharp
[ObservableProperty]
private bool _isLoading;

[ObservableProperty]
private string _gmailStatusText = Strings.Get("StatusChecking");

[ObservableProperty]
private string _stampStatusText = Strings.Get("StatusChecking");

[ObservableProperty]
private string _oauthStatusText = Strings.Get("StatusChecking");

[ObservableProperty]
private bool _isRemoveStampConfirmationVisible;

[RelayCommand]
private void RequestRemoveStamp() => IsRemoveStampConfirmationVisible = true;

[RelayCommand]
private void CancelRemoveStamp() => IsRemoveStampConfirmationVisible = false;

[RelayCommand]
private void ConfirmRemoveStamp()
{
    _stampStore.RemoveStamp();
    HasStamp = false;
    StampStatusText = Strings.Get("StatusNotReady");
    IsRemoveStampConfirmationVisible = false;
    StatusMessage = Strings.Get("SettingsStampRemoved");
}
```

Wrap `LoadedAsync` with `IsLoading = true` and a `finally` that sets it to false. Set the three status labels only after Gmail, stamp, and OAuth checks complete. Add `SettingsStampRemoved` to both resource files (`Stamp dihapus.` / `Stamp removed.`). Preserve encrypted secret storage and never repopulate the secret TextBox.

- [ ] **Step 4: Recompose Settings into a two-column grid**

Use a page header followed by a two-column grid at baseline width:

```xml
<Grid Style="{StaticResource PageFrame}" MaxWidth="920" HorizontalAlignment="Left">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="24" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>

    <StackPanel Grid.Row="0" Grid.ColumnSpan="3" Margin="0,0,0,24">
        <TextBlock Text="Pengaturan" Style="{StaticResource PageTitle}" />
        <TextBlock Text="Kelola koneksi pengiriman, stamp, dan preferensi aplikasi."
                   Style="{StaticResource PageSubtitle}" Margin="0,6,0,0" />
    </StackPanel>

    <Border Grid.Row="1" Grid.Column="0" Style="{StaticResource Card}">
        <StackPanel>
            <TextBlock Text="GMAIL" Style="{StaticResource MicroLabel}" />
            <TextBlock Text="{Binding GmailStatusText}" Style="{StaticResource SectionHeading}" Margin="0,12,0,4" />
            <TextBlock Text="{Binding ConnectedGmail}" Style="{StaticResource MutedText}" />
            <StackPanel Orientation="Horizontal" Margin="0,20,0,0">
                <Button Content="HUBUNGKAN" Command="{Binding ConnectGmailCommand}" Style="{StaticResource PrimaryButton}" Margin="0,0,8,0" />
                <Button Content="PUTUSKAN" Command="{Binding DisconnectGmailCommand}" Style="{StaticResource OutlinedButton}" />
            </StackPanel>
        </StackPanel>
    </Border>

    <Border Grid.Row="1" Grid.Column="2" Style="{StaticResource Card}">
        <StackPanel>
            <TextBlock Text="STAMP" Style="{StaticResource MicroLabel}" />
            <TextBlock Text="{Binding StampStatusText}" Style="{StaticResource SectionHeading}" Margin="0,12,0,20" />
             <StackPanel Orientation="Horizontal">
                 <Button Content="PILIH GAMBAR" Click="PickStamp_Click" Style="{StaticResource OutlinedButton}" Margin="0,0,8,0" />
                 <Button Content="HAPUS" Command="{Binding RequestRemoveStampCommand}" Style="{StaticResource DangerButton}" />
             </StackPanel>
             <Border Background="{StaticResource FogBrush}" Padding="12" Margin="0,12,0,0"
                     Visibility="{Binding IsRemoveStampConfirmationVisible, Converter={StaticResource BoolToVis}}">
                 <StackPanel>
                     <TextBlock Text="Hapus stamp aktif? Tindakan ini memblokir pengiriman sampai stamp baru dipilih."
                                Style="{StaticResource MutedText}" TextWrapping="Wrap" />
                     <StackPanel Orientation="Horizontal" Margin="0,12,0,0">
                         <Button Content="BATAL" Command="{Binding CancelRemoveStampCommand}"
                                 Style="{StaticResource OutlinedButton}" Margin="0,0,8,0" />
                         <Button Content="YA, HAPUS" Command="{Binding ConfirmRemoveStampCommand}"
                                 Style="{StaticResource DangerButton}" />
                     </StackPanel>
                 </StackPanel>
             </Border>
         </StackPanel>
     </Border>

     <Grid Grid.Row="2" Grid.ColumnSpan="3" Margin="0,24,0,0">
         <Grid.ColumnDefinitions>
             <ColumnDefinition Width="*" />
             <ColumnDefinition Width="24" />
             <ColumnDefinition Width="*" />
         </Grid.ColumnDefinitions>
         <Border Grid.Column="0" Style="{StaticResource Card}">
             <StackPanel>
                 <TextBlock Text="OAUTH CLIENT" Style="{StaticResource MicroLabel}" />
                 <TextBlock Text="{Binding OAuthStatusText}" Style="{StaticResource SectionHeading}" Margin="0,12,0,12" />
                 <TextBox Text="{Binding OauthClientSecretJson, UpdateSourceTrigger=PropertyChanged}"
                          Style="{StaticResource FieldInput}" AcceptsReturn="True" MinHeight="64" />
                 <Button Content="SIMPAN KREDENSIAL" Command="{Binding SaveOAuthSecretCommand}"
                         Style="{StaticResource PrimaryButton}" HorizontalAlignment="Left" Margin="0,12,0,0" />
                 <Expander Header="PANDUAN SETUP" Margin="0,12,0,0">
                     <TextBlock Text="Gunakan OAuth Desktop App, simpan client secret, lalu hubungkan akun Gmail payroll."
                                Style="{StaticResource MutedText}" TextWrapping="Wrap" Margin="0,8,0,0" />
                 </Expander>
             </StackPanel>
         </Border>
         <Border Grid.Column="2" Style="{StaticResource Card}">
             <StackPanel>
                 <TextBlock Text="BAHASA" Style="{StaticResource MicroLabel}" />
                 <TextBlock Text="Bahasa antarmuka" Style="{StaticResource SectionHeading}" Margin="0,12,0,12" />
                 <ComboBox ItemsSource="{StaticResource Languages}" SelectedItem="{Binding SelectedLanguage}"
                           Style="{StaticResource FieldCombo}" />
                 <TextBlock Text="Perubahan bahasa diterapkan setelah aplikasi dimulai ulang."
                            Style="{StaticResource MutedText}" TextWrapping="Wrap" Margin="0,12,0,0"
                            Visibility="{Binding LanguageChangedRequiresRestart, Converter={StaticResource BoolToVis}}" />
                 <TextBlock Text="{Binding StatusMessage}" Style="{StaticResource MutedText}"
                            TextWrapping="Wrap" Margin="0,16,0,0" />
             </StackPanel>
         </Border>
     </Grid>
 </Grid>
```

Keep the entire page inside its existing vertical `ScrollViewer` so the 760-DIP baseline remains usable and smaller windows scroll instead of clipping.

- [ ] **Step 5: Run Settings tests and desktop build**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~SettingsViewModelTests
dotnet build src/EunSlip.Desktop/EunSlip.Desktop.csproj --no-restore
```

Expected: tests PASS; build succeeds with 0 warnings and 0 errors.

- [ ] **Step 6: Commit the Settings redesign**

```powershell
git add src/EunSlip.Desktop/ViewModels/SettingsViewModel.cs src/EunSlip.Desktop/Views/SettingsView.xaml src/EunSlip.Desktop/Localization tests/EunSlip.Desktop.Tests/SettingsViewModelTests.cs
git commit -m "ui: compact and clarify Settings"
```

---

### Task 5: Recompose About without Expanding Product Scope

**Files:**
- Modify: `src/EunSlip.Desktop/Views/AboutView.xaml`
- Modify: `tests/EunSlip.Desktop.Tests/ThemeContractTests.cs`

**Interfaces:**
- Consumes: unchanged `AboutViewModel` properties and Task 1 theme resources.
- Produces: compact, readable identity, developer, license, disclaimer, and diagnostics sections.

- [ ] **Step 1: Extend the structural XAML contract**

```csharp
[Fact]
public void StaticPages_UseSharedPageHierarchyAndNoRawBooleanFormatting()
{
    foreach (string relative in new[]
    {
        Path.Combine("src", "EunSlip.Desktop", "Views", "HomeView.xaml"),
        Path.Combine("src", "EunSlip.Desktop", "Views", "SettingsView.xaml"),
        Path.Combine("src", "EunSlip.Desktop", "Views", "AboutView.xaml"),
    })
    {
        string xaml = ReadRepositoryFile(relative.Split(Path.DirectorySeparatorChar));
        Assert.Contains("{StaticResource PageTitle}", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("StringFormat='Terkoneksi: {0}'", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("StringFormat='Aktif: {0}'", xaml, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the test and verify About lacks the shared hierarchy**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~ThemeContractTests
```

Expected: FAIL until all three static pages use `PageTitle` and raw Boolean formatting is removed.

- [ ] **Step 3: Replace About's stacked card wall with two clear bands**

```xml
<Grid Style="{StaticResource PageFrame}" MaxWidth="920" HorizontalAlignment="Left">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>

    <StackPanel Grid.Row="0" Margin="0,0,0,24">
        <TextBlock Text="Tentang Aplikasi" Style="{StaticResource PageTitle}" />
        <TextBlock Text="Identitas produk, lisensi penggunaan, dan lokasi diagnostik."
                   Style="{StaticResource PageSubtitle}" Margin="0,6,0,0" />
    </StackPanel>

    <Grid Grid.Row="1" Margin="0,0,0,24">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="24" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>
        <Border Grid.Column="0" Style="{StaticResource Card}">
            <StackPanel>
                <TextBlock Text="EUNSLIP" Style="{StaticResource MicroLabel}" />
                <TextBlock Text="{Binding AppName}" Style="{StaticResource SectionHeading}" Margin="0,12,0,8" />
                <TextBlock Text="{Binding Version, StringFormat='Versi {0}'}" Style="{StaticResource BodyText}" />
                <TextBlock Text="{Binding Company, StringFormat='Untuk {0}'}" Style="{StaticResource MutedText}" Margin="0,4,0,0" />
            </StackPanel>
        </Border>
        <Border Grid.Column="2" Style="{StaticResource Card}">
            <StackPanel>
                <TextBlock Text="DIKEMBANGKAN OLEH" Style="{StaticResource MicroLabel}" />
                <Image Source="/Assets/vierth.png" Height="28" HorizontalAlignment="Left" Margin="0,16,0,12" />
                <TextBlock Text="{Binding Developer}" Style="{StaticResource SectionHeading}" />
                <TextBlock Text="{Binding ContactEmail}" Style="{StaticResource MutedText}" Margin="0,4,0,0" />
            </StackPanel>
        </Border>
    </Grid>

    <Border Grid.Row="2" BorderBrush="{StaticResource MistBrush}" BorderThickness="1,0,0,0" Padding="0,24,0,0">
        <StackPanel>
            <TextBlock Text="LISENSI" Style="{StaticResource MicroLabel}" />
            <TextBlock Text="{Binding LicenseSummary}" Style="{StaticResource BodyText}" TextWrapping="Wrap" Margin="0,12,0,8" />
            <TextBlock Text="{Binding Disclaimer}" Style="{StaticResource MutedText}" TextWrapping="Wrap" />
            <TextBlock Text="{Binding LogFolder, StringFormat='Folder log: {0}'}"
                       FontFamily="{StaticResource TechnicalFont}" FontSize="13" Margin="0,20,0,0" />
        </StackPanel>
    </Border>
</Grid>
```

- [ ] **Step 4: Run structural tests and build**

Run:

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore --filter FullyQualifiedName~ThemeContractTests
dotnet build src/EunSlip.Desktop/EunSlip.Desktop.csproj --no-restore
```

Expected: tests PASS; build succeeds with 0 warnings and 0 errors.

- [ ] **Step 5: Commit the static-page finish**

```powershell
git add src/EunSlip.Desktop/Views/AboutView.xaml tests/EunSlip.Desktop.Tests/ThemeContractTests.cs
git commit -m "ui: refine About page hierarchy"
```

---

### Task 6: Shell and Static-Page Checkpoint

**Files:**
- Verify only; do not create production files in this task.

**Interfaces:**
- Consumes: Tasks 1–5.
- Produces: a green shell/static-page phase ready for the workflow redesign.

- [ ] **Step 1: Run the scoped Desktop test project**

```powershell
dotnet test tests/EunSlip.Desktop.Tests/EunSlip.Desktop.Tests.csproj --no-restore
```

Expected: all Desktop tests PASS, including the pre-existing suite; 0 failed.

- [ ] **Step 2: Build the complete solution**

```powershell
dotnet build EunSlip.slnx --no-restore
```

Expected: build succeeds with 0 warnings and 0 errors.

- [ ] **Step 3: Launch and inspect the shell and three non-workflow pages**

Run:

```powershell
src\EunSlip.Desktop\bin\Debug\net10.0-windows\EunSlip.Desktop.exe
```

Using Windows Computer Use, inspect the shell, Home, Settings, and About at 1180 × 760. Verify persistent active navigation, 40-DIP actions, firm type hierarchy, Settings' baseline fit, and absence of clipped text. History is intentionally deferred to the workflow plan. Do not connect/disconnect Gmail, remove a stamp, or send payroll during this inspection.

- [ ] **Step 4: Record and commit only necessary checkpoint fixes**

If the inspection reveals a concrete visual regression, add a regression assertion to `ThemeContractTests.cs` or the relevant view-model test, apply the smallest XAML fix, rerun Steps 1–3, then commit the exact touched files:

```powershell
git add src/EunSlip.Desktop tests/EunSlip.Desktop.Tests
git commit -m "fix: close shell redesign regressions"
```

If no fix is required, do not create an empty commit.
