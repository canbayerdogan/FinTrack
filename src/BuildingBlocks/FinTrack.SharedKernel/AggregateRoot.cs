namespace FinTrack.SharedKernel;

/// <summary>
/// Tutarlılık sınırının kökü. Yalnızca aggregate kökleri repository'ye eklenir.
/// Domain olayları buradan yayılır.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
