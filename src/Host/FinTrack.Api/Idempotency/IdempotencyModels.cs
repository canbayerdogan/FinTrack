namespace FinTrack.Api.Idempotency;

/// <summary> Kayıt durumları; <see cref="InProgress"/> asla commit edilmez (docs/11 §5.1). </summary>
public enum IdempotencyStatus : byte
{
    InProgress = 1,
    Completed = 2,
    Tombstone = 3,
}

/// <summary> shared.IdempotencyKeys satırı. Host dışından erişilmez. </summary>
public sealed class IdempotencyKeyRecord
{
    public long Id { get; set; }
    public string ScopeHash { get; set; } = string.Empty;
    /// <summary>SHA-256(JWT sub) — durum sorgusunda kullanıcı izolasyonu için (docs/11 §7.5).</summary>
    public string UserHash { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public IdempotencyStatus Status { get; set; } = IdempotencyStatus.InProgress;
    public string RequestContentType { get; set; } = "application/json";
    public int? ResponseStatusCode { get; set; }
    public string? ResponseContentType { get; set; }
    public string? ResponseHeaders { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime TombstoneExpiresAtUtc { get; set; }
}

/// <summary> Replay için saklanan 2xx yanıt metadata'sı. Yalnızca Location başlığı tutulur. </summary>
public sealed record IdempotencyResponseMetadata(
    int StatusCode,
    string ContentType,
    string? Location,
    string Body);

/// <summary> Durum sorgusunun salt okunur sonucu (docs/11 §7.5). </summary>
public sealed record IdempotencyStatusResult(
    IdempotencyStatus Status,
    DateTime? CompletedAtUtc,
    bool HasBody,
    DateTime ExpiresAtUtc);

public enum IdempotencyInsertOutcome
{
    Created,
    Duplicate,
    /// <summary> INSERT kilit timeout'una takıldı (SQL 1222 / command timeout). </summary>
    InProgress,
}

/// <summary> Idempotency kaydı erişimi; yalnızca host middleware/endpoint kullanır. </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyKeyRecord?> FindAsync(string scopeHash, string key, CancellationToken cancellationToken);

    Task<IdempotencyInsertOutcome> InsertAsync(IdempotencyKeyRecord record, CancellationToken cancellationToken);

    Task CompleteAsync(
        string scopeHash,
        string key,
        IdempotencyResponseMetadata response,
        DateTime completedAtUtc,
        CancellationToken cancellationToken);

    Task TombstoneAsync(string scopeHash, string key, CancellationToken cancellationToken);

    Task<IdempotencyStatusResult?> GetStatusAsync(
        string userId,
        string key,
        CancellationToken cancellationToken);
}
