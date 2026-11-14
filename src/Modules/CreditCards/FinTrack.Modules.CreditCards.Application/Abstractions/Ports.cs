using FinTrack.Modules.CreditCards.Application.Cards;
using FinTrack.Modules.CreditCards.Domain;

namespace FinTrack.Modules.CreditCards.Application.Abstractions;

/// <summary>Kart yazma tarafı. Filtre uygulanmış olsa da sahiplik her zaman açıkça kontrol edilir.</summary>
public interface ICardRepository
{
    Task<CreditCard?> FindAsync(Guid userId, Guid cardId, CancellationToken cancellationToken);

    Task AddAsync(CreditCard card, CancellationToken cancellationToken);
}

public interface ICardQueries
{
    Task<IReadOnlyList<CardDto>> ListAsync(
        Guid userId,
        CardFilter filter,
        CancellationToken cancellationToken);

    Task<CardDto?> FindAsync(Guid userId, Guid cardId, CancellationToken cancellationToken);
}

public enum SaveChangesOutcome
{
    Success = 0,
    ConcurrencyConflict = 1,
    DuplicateKey = 2,
}

/// <summary>
/// EF Core istisnalarını Application sınırında sonuçlara çevirir; handler'lar
/// altyapı tipi görmez (mimari testi bunu zorunlu kılar).
/// </summary>
public interface IUnitOfWork
{
    Task<SaveChangesOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
