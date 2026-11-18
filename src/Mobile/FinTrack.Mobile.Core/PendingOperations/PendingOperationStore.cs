using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinTrack.Mobile.Core.Abstractions;
using Microsoft.Data.Sqlite;

namespace FinTrack.Mobile.Core.PendingOperations;

/// <summary>
/// App-private SQLite deposu (docs/11 §7.1–7.3). Gövde AES-256-GCM ile
/// şifrelenir; nonce her yazmada yenilenir, AAD anahtar/method/route'u kapsar.
/// </summary>
public sealed class PendingOperationStore : IPendingOperationStore, IDisposable
{
    private const int KeyBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    private readonly string _connectionString;
    private readonly ISecureKeyValueStore _secureStore;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private bool _disposed;

    public PendingOperationStore(
        string databasePath,
        ISecureKeyValueStore secureStore,
        TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _secureStore = secureStore ?? throw new ArgumentNullException(nameof(secureStore));
        _clock = clock ?? TimeProvider.System;

        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        CreateSchema();
    }

    public int MaxOperations => 50;

    public int MaxBodyBytes => 64 * 1024;

    public event EventHandler<PendingOperationEvictedEventArgs>? OperationEvicted;

    public async Task<PendingOperation> AddAsync(
        string userIdKey,
        string method,
        string route,
        byte[] body,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userIdKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentNullException.ThrowIfNull(body);
        EnsureBodySize(body.Length);

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            while (await CountCoreAsync(cancellationToken) >= MaxOperations)
            {
                var oldest = await FindOldestCoreAsync(cancellationToken);
                if (oldest is null)
                {
                    break;
                }

                await DeleteCoreAsync(oldest.Key, cancellationToken);
                RaiseEvicted(oldest);
            }

            var key = Guid.NewGuid().ToString("D");
            while (await GetCoreAsync(key, cancellationToken) is not null)
            {
                key = Guid.NewGuid().ToString("D");
            }

            var encryptionKey = await GetOrCreateEncryptionKeyAsync(userIdKey, cancellationToken);
            var record = new PendingOperation(
                key,
                userIdKey,
                method.ToUpperInvariant(),
                route,
                [],
                [],
                [],
                _clock.GetUtcNow(),
                PendingOperationState.Created,
                0,
                null,
                null);
            var encrypted = Encrypt(encryptionKey, record, body);
            record = record with { BodyCipher = encrypted.Cipher, Nonce = encrypted.Nonce, Tag = encrypted.Tag };
            await InsertCoreAsync(record, cancellationToken);
            return record;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<PendingOperation?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return await GetCoreAsync(key, cancellationToken);
    }

    public async Task<IReadOnlyList<PendingOperation>> ListForUserAsync(
        string userIdKey,
        PendingOperationState? state = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userIdKey);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT "Key", "UserIdKey", "Method", "Route", "BodyCipher", "Nonce", "Tag",
                   "CreatedAtUtc", "State", "AttemptCount", "LastError", "TombstoneCheckedAtUtc"
            FROM "PendingOperations"
            WHERE "UserIdKey" = $userId AND ($state IS NULL OR "State" = $state)
            ORDER BY "CreatedAtUtc" ASC, "Key" ASC;
            """;
        command.Parameters.AddWithValue("$userId", userIdKey);
        command.Parameters.AddWithValue("$state", state?.ToString() ?? (object)DBNull.Value);

        var result = new List<PendingOperation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Map(reader));
        }

        return result;
    }

    public async Task<byte[]?> TryReadBodyAsync(
        PendingOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Nonce.Length != NonceBytes || operation.Tag.Length != TagBytes)
        {
            return null;
        }

        byte[]? encryptionKey;
        try
        {
            var encoded = await _secureStore.GetAsync(
                PendingOperationKeyNames.ForUser(operation.UserIdKey),
                cancellationToken);
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return null;
            }

            encryptionKey = Convert.FromBase64String(encoded);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Anahtar kaybı/bozulması: kayıt silinmez, Uncertain işaretlenir.
            return null;
        }

        if (encryptionKey.Length != KeyBytes)
        {
            return null;
        }

        try
        {
            var plaintext = new byte[operation.BodyCipher.Length];
            using var aes = new AesGcm(encryptionKey, TagBytes);
            aes.Decrypt(operation.Nonce, operation.BodyCipher, operation.Tag, plaintext, BuildAad(operation));
            return plaintext;
        }
        catch (CryptographicException)
        {
            // AAD değişikliği, tag uyumsuzluğu veya şifreli gövde bozulması.
            return null;
        }
    }

    public async Task<PendingOperation> RewriteBodyAsync(
        string key,
        byte[] body,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(body);
        EnsureBodySize(body.Length);

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            var record = await GetCoreAsync(key, cancellationToken)
                         ?? throw new InvalidOperationException("Bekleyen işlem kaydı bulunamadı.");
            var encryptionKey = await GetOrCreateEncryptionKeyAsync(record.UserIdKey, cancellationToken);
            var encrypted = Encrypt(encryptionKey, record, body);
            record = record with { BodyCipher = encrypted.Cipher, Nonce = encrypted.Nonce, Tag = encrypted.Tag };
            await UpdateBodyCoreAsync(record, cancellationToken);
            return record;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task SetStateAsync(
        string key,
        PendingOperationState state,
        string? lastError = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE "PendingOperations" SET "State" = $state, "LastError" = $error WHERE "Key" = $key;
                """;
            command.Parameters.AddWithValue("$state", state.ToString());
            command.Parameters.AddWithValue("$error", lastError ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$key", key);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task IncrementAttemptAsync(
        string key,
        string? lastError = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE "PendingOperations"
                SET "AttemptCount" = "AttemptCount" + 1, "LastError" = $error
                WHERE "Key" = $key;
                """;
            command.Parameters.AddWithValue("$error", lastError ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$key", key);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task MarkTombstoneCheckedAsync(
        string key,
        DateTimeOffset checkedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE "PendingOperations" SET "TombstoneCheckedAtUtc" = $checked WHERE "Key" = $key;
                """;
            command.Parameters.AddWithValue("$checked", FormatTime(checkedAtUtc));
            command.Parameters.AddWithValue("$key", key);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await DeleteCoreAsync(key, cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writeGate.Dispose();
    }

    private void CreateSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS "PendingOperations" (
                "Key" TEXT NOT NULL PRIMARY KEY,
                "UserIdKey" TEXT NOT NULL,
                "Method" TEXT NOT NULL,
                "Route" TEXT NOT NULL,
                "BodyCipher" BLOB NOT NULL,
                "Nonce" BLOB NOT NULL,
                "Tag" BLOB NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "State" TEXT NOT NULL,
                "AttemptCount" INTEGER NOT NULL,
                "LastError" TEXT NULL,
                "TombstoneCheckedAtUtc" TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_PendingOperations_User_State"
                ON "PendingOperations" ("UserIdKey", "State", "CreatedAtUtc");
            """;
        command.ExecuteNonQuery();
    }

    private Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return Task.FromResult(connection);
    }

    private async Task<PendingOperation?> GetCoreAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT "Key", "UserIdKey", "Method", "Route", "BodyCipher", "Nonce", "Tag",
                   "CreatedAtUtc", "State", "AttemptCount", "LastError", "TombstoneCheckedAtUtc"
            FROM "PendingOperations" WHERE "Key" = $key LIMIT 1;
            """;
        command.Parameters.AddWithValue("$key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private async Task<long> CountCoreAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"PendingOperations\";";
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    private async Task<PendingOperation?> FindOldestCoreAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT "Key", "UserIdKey", "Method", "Route", "BodyCipher", "Nonce", "Tag",
                   "CreatedAtUtc", "State", "AttemptCount", "LastError", "TombstoneCheckedAtUtc"
            FROM "PendingOperations" ORDER BY "CreatedAtUtc" ASC, "Key" ASC LIMIT 1;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private async Task InsertCoreAsync(PendingOperation record, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            INSERT INTO "PendingOperations"
                ("Key", "UserIdKey", "Method", "Route", "BodyCipher", "Nonce", "Tag",
                 "CreatedAtUtc", "State", "AttemptCount", "LastError", "TombstoneCheckedAtUtc")
            VALUES
                ($key, $userId, $method, $route, $cipher, $nonce, $tag,
                 $created, $state, $attempts, $error, $checked);
            """;
        AddRecordParameters(command, record);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpdateBodyCoreAsync(PendingOperation record, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            UPDATE "PendingOperations"
            SET "BodyCipher" = $cipher, "Nonce" = $nonce, "Tag" = $tag
            WHERE "Key" = $key;
            """;
        command.Parameters.AddWithValue("$cipher", record.BodyCipher);
        command.Parameters.AddWithValue("$nonce", record.Nonce);
        command.Parameters.AddWithValue("$tag", record.Tag);
        command.Parameters.AddWithValue("$key", record.Key);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddRecordParameters(SqliteCommand command, PendingOperation record)
    {
        command.Parameters.AddWithValue("$key", record.Key);
        command.Parameters.AddWithValue("$userId", record.UserIdKey);
        command.Parameters.AddWithValue("$method", record.Method);
        command.Parameters.AddWithValue("$route", record.Route);
        command.Parameters.AddWithValue("$cipher", record.BodyCipher);
        command.Parameters.AddWithValue("$nonce", record.Nonce);
        command.Parameters.AddWithValue("$tag", record.Tag);
        command.Parameters.AddWithValue("$created", FormatTime(record.CreatedAtUtc));
        command.Parameters.AddWithValue("$state", record.State.ToString());
        command.Parameters.AddWithValue("$attempts", record.AttemptCount);
        command.Parameters.AddWithValue("$error", record.LastError ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$checked",
            record.TombstoneCheckedAtUtc.HasValue ? FormatTime(record.TombstoneCheckedAtUtc.Value) : DBNull.Value);
    }

    private async Task DeleteCoreAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM \"PendingOperations\" WHERE \"Key\" = $key;";
        command.Parameters.AddWithValue("$key", key);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<byte[]> GetOrCreateEncryptionKeyAsync(
        string userIdKey,
        CancellationToken cancellationToken)
    {
        var name = PendingOperationKeyNames.ForUser(userIdKey);
        var stored = await _secureStore.GetAsync(name, cancellationToken);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            try
            {
                var existing = Convert.FromBase64String(stored);
                if (existing.Length == KeyBytes)
                {
                    return existing;
                }
            }
            catch (FormatException)
            {
                // Bozuk anahtar yeni bir kayıt için yeniden oluşturulur.
            }
        }

        var created = RandomNumberGenerator.GetBytes(KeyBytes);
        await _secureStore.SetAsync(name, Convert.ToBase64String(created), cancellationToken);
        return created;
    }

    private static (byte[] Cipher, byte[] Nonce, byte[] Tag) Encrypt(
        byte[] encryptionKey,
        PendingOperation operation,
        byte[] body)
    {
        // PK çakışması dahil her yazmada yeni nonce (docs/11 §7.2 madde 2).
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var cipher = new byte[body.Length];
        var tag = new byte[TagBytes];
        using var aes = new AesGcm(encryptionKey, TagBytes);
        aes.Encrypt(nonce, body, cipher, tag, BuildAad(operation));
        return (cipher, nonce, tag);
    }

    private static byte[] BuildAad(PendingOperation operation) =>
        Encoding.UTF8.GetBytes($"{operation.Key}\n{operation.Method}\n{operation.Route}");

    private void EnsureBodySize(int length)
    {
        if (length > MaxBodyBytes)
        {
            throw new PendingOperationBodyTooLargeException(length, MaxBodyBytes);
        }
    }

    private void RaiseEvicted(PendingOperation operation)
    {
        var args = new PendingOperationEvictedEventArgs(operation.Key, operation.CreatedAtUtc);
        OperationEvicted?.Invoke(this, args);
        Trace.TraceWarning(
            "FinTrack pending-operation limit ({0}) reached; oldest operation {1} created {2:O} was removed.",
            MaxOperations,
            operation.Key,
            operation.CreatedAtUtc);
    }

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTime(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static PendingOperation Map(SqliteDataReader reader) => new(
        reader.GetString(reader.GetOrdinal("Key")),
        reader.GetString(reader.GetOrdinal("UserIdKey")),
        reader.GetString(reader.GetOrdinal("Method")),
        reader.GetString(reader.GetOrdinal("Route")),
        reader.GetFieldValue<byte[]>(reader.GetOrdinal("BodyCipher")),
        reader.GetFieldValue<byte[]>(reader.GetOrdinal("Nonce")),
        reader.GetFieldValue<byte[]>(reader.GetOrdinal("Tag")),
        ParseTime(reader.GetString(reader.GetOrdinal("CreatedAtUtc"))),
        Enum.Parse<PendingOperationState>(reader.GetString(reader.GetOrdinal("State"))),
        reader.GetInt32(reader.GetOrdinal("AttemptCount")),
        reader.IsDBNull(reader.GetOrdinal("LastError")) ? null : reader.GetString(reader.GetOrdinal("LastError")),
        reader.IsDBNull(reader.GetOrdinal("TombstoneCheckedAtUtc"))
            ? null
            : ParseTime(reader.GetString(reader.GetOrdinal("TombstoneCheckedAtUtc"))));
}
