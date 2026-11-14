namespace FinTrack.SharedKernel;

/// <summary>
/// Domain olayı. Modül içinde veya modüller arasında in-process yayılır.
/// Kalıcılık garantisi (outbox) v1 kapsamında değildir.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}
