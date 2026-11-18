namespace FinTrack.Mobile.Core.PendingOperations;

/// <summary>
/// Uygulama özel SQLite deposu. Gövde yalnızca AES-256-GCM ile şifrelenmiş
/// olarak saklanır; SecureStorage yalnızca kullanıcı bazlı anahtar içindir.
/// </summary>
public interface IPendingOperationStore
{
    int MaxOperations { get; }

    int MaxBodyBytes { get; }

    event EventHandler<PendingOperationEvictedEventArgs>? OperationEvicted;

    Task<PendingOperation> AddAsync(
        string userIdKey,
        string method,
        string route,
        byte[] body,
        CancellationToken cancellationToken = default);

    Task<PendingOperation?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingOperation>> ListForUserAsync(
        string userIdKey,
        PendingOperationState? state = null,
        CancellationToken cancellationToken = default);

    Task<byte[]?> TryReadBodyAsync(
        PendingOperation operation,
        CancellationToken cancellationToken = default);

    Task<PendingOperation> RewriteBodyAsync(
        string key,
        byte[] body,
        CancellationToken cancellationToken = default);

    Task SetStateAsync(
        string key,
        PendingOperationState state,
        string? lastError = null,
        CancellationToken cancellationToken = default);

    Task IncrementAttemptAsync(
        string key,
        string? lastError = null,
        CancellationToken cancellationToken = default);

    Task MarkTombstoneCheckedAsync(
        string key,
        DateTimeOffset checkedAtUtc,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
