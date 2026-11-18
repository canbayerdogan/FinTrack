namespace FinTrack.Mobile.Core.PendingOperations;

/// <summary> Bekleyen işlemin yerel durum makinesi. </summary>
public enum PendingOperationState
{
    Created,
    SentUnconfirmed,
    Uncertain,
}

/// <summary>
/// Yerel bekleyen işlem kaydı. Gövde yalnızca şifreli olarak tutulur.
/// </summary>
public sealed record PendingOperation(
    string Key,
    string UserIdKey,
    string Method,
    string Route,
    byte[] BodyCipher,
    byte[] Nonce,
    byte[] Tag,
    DateTimeOffset CreatedAtUtc,
    PendingOperationState State,
    int AttemptCount,
    string? LastError,
    DateTimeOffset? TombstoneCheckedAtUtc);

/// <summary> En eski kayıt kapasite nedeniyle silindiğinde yayınlanır. </summary>
public sealed class PendingOperationEvictedEventArgs : EventArgs
{
    public PendingOperationEvictedEventArgs(string key, DateTimeOffset createdAtUtc)
    {
        Key = key;
        CreatedAtUtc = createdAtUtc;
    }

    public string Key { get; }

    public DateTimeOffset CreatedAtUtc { get; }
}

/// <summary> 64 KB gövde sınırı aşıldığında oluşturulur. </summary>
public sealed class PendingOperationBodyTooLargeException : InvalidOperationException
{
    public PendingOperationBodyTooLargeException(int length, int limit)
        : base($"İstek gövdesi {length} bayt; bekleyen işlem sınırı {limit} bayt.")
    {
        Length = length;
        Limit = limit;
    }

    public int Length { get; }

    public int Limit { get; }
}

/// <summary> Kurtarma sonucunun kullanıcıya gösterilebilir karşılığı. </summary>
public enum RecoveryStatus
{
    Completed,
    DefiniteFailure,
    Pending,
    Uncertain,
    Skipped,
}

public sealed record RecoveryResult(RecoveryStatus Status, string Key, string? Message);

public sealed record RecoveryReport(IReadOnlyList<RecoveryResult> Results)
{
    public string? Message => Results.LastOrDefault(result => result.Message is not null)?.Message;
}
