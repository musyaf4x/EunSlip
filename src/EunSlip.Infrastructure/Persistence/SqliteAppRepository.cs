using System.Globalization;
using EunSlip.Core.Persistence;
using Microsoft.Data.Sqlite;

namespace EunSlip.Infrastructure.Persistence;

public sealed class SqliteAppRepository(string connectionString) : IAppRepository
{
    private const int CurrentUserVersion = 1;

    public void Initialize()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        Migrate(connection, transaction);
        transaction.Commit();
    }

    public bool CheckIntegrity()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        object result = command.ExecuteScalar()!;
        return Convert.ToString(result, CultureInfo.InvariantCulture) == "ok";
    }

    public void ResetDatabase()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "DROP TABLE IF EXISTS SendAttempts; " +
            "DROP TABLE IF EXISTS BatchRecipients; " +
            "DROP TABLE IF EXISTS PayrollBatches; " +
            "DROP TABLE IF EXISTS ApplicationSettings;";
        _ = command.ExecuteNonQuery();

        using SqliteCommand resetVersion = connection.CreateCommand();
        resetVersion.Transaction = transaction;
        resetVersion.CommandText = "PRAGMA user_version = 0;";
        _ = resetVersion.ExecuteNonQuery();

        Migrate(connection, transaction);
        transaction.Commit();
    }

    public string? GetSetting(string key)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM ApplicationSettings WHERE Key = @key;";
        command.Parameters.AddWithValue("@key", key);
        object? result = command.ExecuteScalar();
        return result is null ? null : Convert.ToString(result, CultureInfo.InvariantCulture);
    }

    public void SetSetting(string key, string value)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO ApplicationSettings (Key, Value) VALUES (@key, @value) " +
            "ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        _ = command.ExecuteNonQuery();
        transaction.Commit();
    }

    public Guid CreateBatch(PayrollBatchRecord batch)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO PayrollBatches (Id, Period, PaymentDate, Fingerprint, Status, CreatedAtUtc, " +
            "StartedAtUtc, CompletedAtUtc, WarningConfirmed, RecipientCount, SentCount, FailedCount) " +
            "VALUES (@id, @period, @paymentDate, @fingerprint, @status, @createdAt, NULL, NULL, " +
            "@warningConfirmed, @recipientCount, 0, 0);";
        command.Parameters.AddWithValue("@id", batch.Id.ToString());
        command.Parameters.AddWithValue("@period", batch.Period);
        command.Parameters.AddWithValue("@paymentDate", batch.PaymentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@fingerprint", batch.Fingerprint);
        command.Parameters.AddWithValue("@status", batch.Status.ToString());
        command.Parameters.AddWithValue("@createdAt", batch.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@warningConfirmed", batch.WarningConfirmed ? 1 : 0);
        command.Parameters.AddWithValue("@recipientCount", batch.RecipientCount);
        _ = command.ExecuteNonQuery();
        transaction.Commit();
        return batch.Id;
    }

    public PayrollBatchRecord? GetBatch(Guid id)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = BatchSelectClause + "WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", id.ToString());
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadBatch(reader) : null;
    }

    public IReadOnlyList<PayrollBatchRecord> ListBatches()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = BatchSelectClause + "ORDER BY CreatedAtUtc DESC;";
        using SqliteDataReader reader = command.ExecuteReader();
        List<PayrollBatchRecord> batches = [];
        while (reader.Read())
        {
            batches.Add(ReadBatch(reader));
        }
        return batches;
    }

    public void UpdateBatchStatus(Guid id, BatchStatus status, DateTimeOffset? startedAtUtc, DateTimeOffset? completedAtUtc)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "UPDATE PayrollBatches SET Status = @status, " +
            "StartedAtUtc = COALESCE(@startedAt, StartedAtUtc), " +
            "CompletedAtUtc = @completedAt WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", id.ToString());
        command.Parameters.AddWithValue("@status", status.ToString());
        command.Parameters.AddWithValue("@startedAt",
            (object?)startedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("@completedAt",
            (object?)completedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
        _ = command.ExecuteNonQuery();
        transaction.Commit();
    }

    public Guid AddRecipient(BatchRecipientRecord recipient)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO BatchRecipients (Id, BatchId, EncryptedNik, EncryptedEmail, NikHint, Status, " +
            "LastUpdatedAtUtc) VALUES (@id, @batchId, @nik, @email, @nikHint, @status, @updatedAt);";
        command.Parameters.AddWithValue("@id", recipient.Id.ToString());
        command.Parameters.AddWithValue("@batchId", recipient.BatchId.ToString());
        command.Parameters.AddWithValue("@nik", recipient.EncryptedNik);
        command.Parameters.AddWithValue("@email", recipient.EncryptedEmail);
        command.Parameters.AddWithValue("@nikHint", (object?)recipient.NikHint ?? DBNull.Value);
        command.Parameters.AddWithValue("@status", recipient.Status.ToString());
        command.Parameters.AddWithValue("@updatedAt", recipient.LastUpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();
        transaction.Commit();
        return recipient.Id;
    }

    public IReadOnlyList<BatchRecipientRecord> ListRecipients(Guid batchId)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = RecipientSelectClause + "WHERE BatchId = @batchId ORDER BY Id;";
        command.Parameters.AddWithValue("@batchId", batchId.ToString());
        using SqliteDataReader reader = command.ExecuteReader();
        List<BatchRecipientRecord> recipients = [];
        while (reader.Read())
        {
            recipients.Add(ReadRecipient(reader));
        }
        return recipients;
    }

    public void UpdateRecipientStatus(Guid recipientId, RecipientStatus status, DateTimeOffset updatedAtUtc)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "UPDATE BatchRecipients SET Status = @status, LastUpdatedAtUtc = @updatedAt WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", recipientId.ToString());
        command.Parameters.AddWithValue("@status", status.ToString());
        command.Parameters.AddWithValue("@updatedAt", updatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();

        if (status is RecipientStatus.Sent or RecipientStatus.Failed)
        {
            UpdateBatchTally(connection, transaction, recipientId);
        }

        transaction.Commit();
    }

    public void AddAttempt(SendAttemptRecord attempt)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO SendAttempts (Id, RecipientId, AttemptNumber, AttemptType, StartedAtUtc, " +
            "CompletedAtUtc, Status, ErrorCategory, ErrorMessage, GmailMessageId) VALUES (@id, @recipientId, " +
            "@attemptNumber, @attemptType, @startedAt, @completedAt, @status, @errorCategory, @errorMessage, @gmailMessageId);";
        command.Parameters.AddWithValue("@id", attempt.Id.ToString());
        command.Parameters.AddWithValue("@recipientId", attempt.RecipientId.ToString());
        command.Parameters.AddWithValue("@attemptNumber", attempt.AttemptNumber);
        command.Parameters.AddWithValue("@attemptType", attempt.AttemptType.ToString());
        command.Parameters.AddWithValue("@startedAt", attempt.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@completedAt",
            (object?)attempt.CompletedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("@status", attempt.Status.ToString());
        command.Parameters.AddWithValue("@errorCategory", (object?)attempt.ErrorCategory ?? DBNull.Value);
        command.Parameters.AddWithValue("@errorMessage", (object?)attempt.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("@gmailMessageId", (object?)attempt.GmailMessageId ?? DBNull.Value);
        _ = command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void CompleteAttempt(Guid attemptId, AttemptStatus status, DateTimeOffset completedAtUtc, string? errorCategory, string? errorMessage, string? gmailMessageId)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "UPDATE SendAttempts SET Status = @status, CompletedAtUtc = @completedAt, " +
            "ErrorCategory = @errorCategory, ErrorMessage = @errorMessage, GmailMessageId = @gmailMessageId " +
            "WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", attemptId.ToString());
        command.Parameters.AddWithValue("@status", status.ToString());
        command.Parameters.AddWithValue("@completedAt", completedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@errorCategory", (object?)errorCategory ?? DBNull.Value);
        command.Parameters.AddWithValue("@errorMessage", (object?)errorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("@gmailMessageId", (object?)gmailMessageId ?? DBNull.Value);
        _ = command.ExecuteNonQuery();
        transaction.Commit();
    }

    public IReadOnlyList<Guid> FindInterruptedBatches()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM PayrollBatches WHERE Status = @status;";
        command.Parameters.AddWithValue("@status", BatchStatus.Sending.ToString());
        using SqliteDataReader reader = command.ExecuteReader();
        List<Guid> ids = [];
        while (reader.Read())
        {
            ids.Add(Guid.Parse(reader.GetString(0)));
        }
        return ids;
    }

    public void ResetSendingRecipientsToPending(Guid batchId)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "UPDATE BatchRecipients SET Status = @status WHERE BatchId = @batchId AND Status = @sending;";
        command.Parameters.AddWithValue("@batchId", batchId.ToString());
        command.Parameters.AddWithValue("@status", RecipientStatus.Pending.ToString());
        command.Parameters.AddWithValue("@sending", RecipientStatus.Sending.ToString());
        _ = command.ExecuteNonQuery();
        transaction.Commit();
    }

    public IReadOnlyList<string> FindPreviouslySentNiks(string period)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT NikHint FROM BatchRecipients WHERE Status = @sent AND BatchId IN " +
            "(SELECT Id FROM PayrollBatches WHERE Period = @period AND Status = @completed);";
        command.Parameters.AddWithValue("@sent", RecipientStatus.Sent.ToString());
        command.Parameters.AddWithValue("@completed", BatchStatus.Completed.ToString());
        command.Parameters.AddWithValue("@period", period);
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> niks = [];
        while (reader.Read() && !reader.IsDBNull(0))
        {
            niks.Add(reader.GetString(0));
        }
        return niks;
    }

    public void DeleteBatch(Guid id)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "DELETE FROM SendAttempts WHERE RecipientId IN (SELECT Id FROM BatchRecipients WHERE BatchId = @id); " +
            "DELETE FROM BatchRecipients WHERE BatchId = @id; " +
            "DELETE FROM PayrollBatches WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", id.ToString());
        _ = command.ExecuteNonQuery();
        transaction.Commit();
    }

    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new(connectionString);
        connection.Open();
        using SqliteCommand pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        _ = pragma.ExecuteNonQuery();
        return connection;
    }

    private static void Migrate(SqliteConnection connection, SqliteTransaction transaction)
    {
        int version = ReadUserVersion(connection, transaction);
        if (version >= CurrentUserVersion)
        {
            return;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS ApplicationSettings (Key TEXT PRIMARY KEY NOT NULL, Value TEXT NOT NULL); " +
            "CREATE TABLE IF NOT EXISTS PayrollBatches (Id TEXT PRIMARY KEY NOT NULL, Period TEXT NOT NULL, " +
            "PaymentDate TEXT NOT NULL, Fingerprint TEXT NOT NULL, Status TEXT NOT NULL, CreatedAtUtc TEXT NOT NULL, " +
            "StartedAtUtc TEXT, CompletedAtUtc TEXT, WarningConfirmed INTEGER NOT NULL DEFAULT 0, " +
            "RecipientCount INTEGER NOT NULL DEFAULT 0, SentCount INTEGER NOT NULL DEFAULT 0, FailedCount INTEGER NOT NULL DEFAULT 0); " +
            "CREATE TABLE IF NOT EXISTS BatchRecipients (Id TEXT PRIMARY KEY NOT NULL, BatchId TEXT NOT NULL, " +
            "EncryptedNik TEXT NOT NULL, EncryptedEmail TEXT NOT NULL, NikHint TEXT, Status TEXT NOT NULL, " +
            "LastUpdatedAtUtc TEXT NOT NULL, FOREIGN KEY (BatchId) REFERENCES PayrollBatches(Id) ON DELETE CASCADE); " +
            "CREATE TABLE IF NOT EXISTS SendAttempts (Id TEXT PRIMARY KEY NOT NULL, RecipientId TEXT NOT NULL, " +
            "AttemptNumber INTEGER NOT NULL, AttemptType TEXT NOT NULL, StartedAtUtc TEXT NOT NULL, CompletedAtUtc TEXT, " +
            "Status TEXT NOT NULL, ErrorCategory TEXT, ErrorMessage TEXT, GmailMessageId TEXT, " +
            "FOREIGN KEY (RecipientId) REFERENCES BatchRecipients(Id) ON DELETE CASCADE); " +
            "CREATE INDEX IF NOT EXISTS IX_BatchRecipients_BatchId ON BatchRecipients(BatchId); " +
            "CREATE INDEX IF NOT EXISTS IX_SendAttempts_RecipientId ON SendAttempts(RecipientId);";
        _ = command.ExecuteNonQuery();

        using SqliteCommand setVersion = connection.CreateCommand();
        setVersion.Transaction = transaction;
        setVersion.CommandText = $"PRAGMA user_version = {CurrentUserVersion};";
        _ = setVersion.ExecuteNonQuery();
    }

    private static int ReadUserVersion(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version;";
        object result = command.ExecuteScalar()!;
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static void UpdateBatchTally(
        SqliteConnection connection, SqliteTransaction transaction,
        Guid recipientId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "UPDATE PayrollBatches SET SentCount = (SELECT COUNT(*) FROM BatchRecipients WHERE " +
            "BatchId = PayrollBatches.Id AND Status = @sent), FailedCount = (SELECT COUNT(*) FROM " +
            "BatchRecipients WHERE BatchId = PayrollBatches.Id AND Status = @failed) WHERE Id = " +
            "(SELECT BatchId FROM BatchRecipients WHERE Id = @recipientId);";
        command.Parameters.AddWithValue("@recipientId", recipientId.ToString());
        command.Parameters.AddWithValue("@sent", RecipientStatus.Sent.ToString());
        command.Parameters.AddWithValue("@failed", RecipientStatus.Failed.ToString());
        _ = command.ExecuteNonQuery();
    }

    private const string BatchSelectClause =
        "SELECT Id, Period, PaymentDate, Fingerprint, Status, CreatedAtUtc, " +
        "StartedAtUtc, CompletedAtUtc, WarningConfirmed, RecipientCount, SentCount, FailedCount " +
        "FROM PayrollBatches ";

    private static PayrollBatchRecord ReadBatch(SqliteDataReader reader)
    {
        return new PayrollBatchRecord(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            DateOnly.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
            reader.GetString(3),
            Enum.Parse<BatchStatus>(reader.GetString(4)),
            DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
            reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture),
            reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
            reader.GetInt32(8) != 0,
            reader.GetInt32(9),
            reader.GetInt32(10),
            reader.GetInt32(11));
    }

    private const string RecipientSelectClause =
        "SELECT Id, BatchId, EncryptedNik, EncryptedEmail, NikHint, Status, LastUpdatedAtUtc " +
        "FROM BatchRecipients ";

    private static BatchRecipientRecord ReadRecipient(SqliteDataReader reader)
    {
        return new BatchRecipientRecord(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            Enum.Parse<RecipientStatus>(reader.GetString(5)),
            DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture));
    }
}
