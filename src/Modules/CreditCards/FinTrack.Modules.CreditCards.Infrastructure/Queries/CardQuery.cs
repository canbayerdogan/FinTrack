using FinTrack.ModuleContracts;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure.Queries;

/// <summary>
/// Expenses'ın kart alışverişini doğrulaması için sahiplik + durum sorgusu.
/// Yalnızca <see cref="CardReference"/> görünür; entity sızmaz.
/// </summary>
internal sealed class CardQuery(CreditCardsDbContext db) : ICardQuery
{
    public Task<CardReference?> GetCardAsync(
        Guid userId,
        Guid cardId,
        CancellationToken cancellationToken) =>
        db.Cards
            .AsNoTracking()
            .Where(card => card.Id == cardId && card.UserId == userId)
            .Select(card => new CardReference(card.Id, card.DisplayName, !card.IsArchived))
            .FirstOrDefaultAsync(cancellationToken);
}
