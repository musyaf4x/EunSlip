using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Sending;
using EunSlip.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace EunSlip.Desktop.Tests;

public sealed class SettingsViewModelTests
{
    private sealed class FakeRepository : IAppRepository
    {
        public Dictionary<string, string> Settings { get; } = [];
        public void Initialize() { }
        public bool CheckIntegrity() => true;
        public void ResetDatabase() { }
        public string? GetSetting(string key) => Settings.GetValueOrDefault(key);
        public void SetSetting(string key, string value) => Settings[key] = value;
        public Guid CreateBatch(PayrollBatchRecord batch) => Guid.Empty;
        public PayrollBatchRecord? GetBatch(Guid id) => null;
        public IReadOnlyList<PayrollBatchRecord> ListBatches() => [];
        public void UpdateBatchStatus(Guid id, BatchStatus status, DateTimeOffset? startedAt, DateTimeOffset? completedAt) { }
        public Guid AddRecipient(BatchRecipientRecord recipient) => recipient.Id;
        public IReadOnlyList<BatchRecipientRecord> ListRecipients(Guid batchId) => [];
        public void UpdateRecipientStatus(Guid recipientId, RecipientStatus status, DateTimeOffset updatedAt) { }
        public void AddAttempt(SendAttemptRecord attempt) { }
        public void CompleteAttempt(Guid attemptId, AttemptStatus status, DateTimeOffset completedAt, string? errorCategory, string? errorMessage, string? gmailMessageId) { }
        public AttemptStatus? GetLatestAttemptStatus(Guid recipientId) => null;
        public IReadOnlyList<Guid> FindInterruptedBatches() => [];
        public void ResetSendingRecipientsToPending(Guid batchId) { }
        public IReadOnlyList<string> FindPreviouslySentNiks(string period) => [];
        public void DeleteBatch(Guid id) { }
    }

    private sealed class FakeGmail(bool connected) : IGmailAuthorization
    {
        public string? ConnectedEmail { get; private set; } = connected ? "g@e.co" : null;
        public Task<GoogleAccount?> ConnectAsync(string clientSecretJson, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(clientSecretJson))
            {
                return Task.FromResult<GoogleAccount?>(null);
            }
            ConnectedEmail = "g@e.co";
            return Task.FromResult<GoogleAccount?>(new GoogleAccount("g@e.co"));
        }
        public Task<GoogleAccount?> RestoreAsync(CancellationToken cancellationToken)
            => Task.FromResult<GoogleAccount?>(ConnectedEmail is null ? null : new GoogleAccount(ConnectedEmail));
        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            ConnectedEmail = null;
            return Task.CompletedTask;
        }
        public Task<bool> IsConnectedAsync(CancellationToken cancellationToken) => Task.FromResult(ConnectedEmail is not null);
    }

    private sealed class FakeStampStore(bool hasStamp) : ISharedFileStore
    {
        public bool HasStamp { get; private set; } = hasStamp;
        public string? GetActiveStampPath() => HasStamp ? "stamp.png" : null;
        public string ImportStamp(string sourcePath)
        {
            if (!System.IO.File.Exists(sourcePath))
            {
                throw new StampValidationException("Stamp file does not exist.");
            }
            HasStamp = true;
            return "stamp.png";
        }
        public void RemoveStamp() => HasStamp = false;
    }

    private static SettingsViewModel Create(
        out FakeRepository repo, bool connected = false, bool hasStamp = false)
    {
        repo = new FakeRepository();
        return new SettingsViewModel(
            new FakeGmail(connected), new FakeStampStore(hasStamp), repo,
            NullLogger<SettingsViewModel>.Instance);
    }

    [Fact]
    public async Task Loaded_RestoresGmailAndStampAndLanguage()
    {
        SettingsViewModel vm = Create(out FakeRepository repo, connected: true, hasStamp: true);
        repo.Settings["UiLanguage"] = "en-US";

        await vm.LoadedCommand.ExecuteAsync(null);

        Assert.True(vm.HasGmailConnection);
        Assert.True(vm.HasStamp);
        Assert.Equal("en-US", vm.SelectedLanguage);
    }

    [Fact]
    public async Task ConnectGmail_NoClientSecret_ShowsWarning()
    {
        SettingsViewModel vm = Create(out _);

        await vm.ConnectGmailCommand.ExecuteAsync(null);

        Assert.False(vm.HasGmailConnection);
        Assert.Contains("OAuth", vm.StatusMessage);
    }

    [Fact]
    public async Task ConnectGmail_WithClientSecret_Connects()
    {
        SettingsViewModel vm = Create(out FakeRepository repo);
        repo.Settings["OAuthClientSecret"] = "{ \"installed\": {} }";

        await vm.ConnectGmailCommand.ExecuteAsync(null);

        Assert.True(vm.HasGmailConnection);
        Assert.Equal("g@e.co", vm.ConnectedGmail);
    }

    [Fact]
    public async Task DisconnectGmail_ClearsConnection()
    {
        SettingsViewModel vm = Create(out _, connected: true);
        await vm.LoadedCommand.ExecuteAsync(null);

        await vm.DisconnectGmailCommand.ExecuteAsync(null);

        Assert.False(vm.HasGmailConnection);
        Assert.Null(vm.ConnectedGmail);
    }

    [Fact]
    public void PickStamp_InvalidImage_ShowsErrorMessage()
    {
        SettingsViewModel vm = Create(out _);

        vm.PickStampCommand.Execute("nonexistent.png");

        Assert.NotNull(vm.StatusMessage);
        Assert.False(vm.HasStamp);
    }

    [Fact]
    public void RemoveStamp_ClearsFlag()
    {
        SettingsViewModel vm = Create(out _, hasStamp: true);

        vm.RemoveStampCommand.Execute(null);

        Assert.False(vm.HasStamp);
    }

    [Fact]
    public void ChangingLanguage_PersistsAndFlagsRestart()
    {
        SettingsViewModel vm = Create(out FakeRepository repo);

        vm.SelectedLanguage = "en-US";

        Assert.Equal("en-US", repo.Settings["UiLanguage"]);
        Assert.True(vm.LanguageChangedRequiresRestart);
        Assert.NotNull(vm.StatusMessage);
    }
}
