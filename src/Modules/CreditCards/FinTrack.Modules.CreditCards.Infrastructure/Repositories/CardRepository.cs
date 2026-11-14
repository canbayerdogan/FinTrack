using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure.Repositories;

internal sealed class CardRepository(CreditCardsDbContext db) : ICardRepository
{
    public Task<CreditCard?> FindAsync(Guid userId, Guid cardId, CancellationToken cancellationToken) =>
        db.Cards.FirstOrDefaultAsync(
            card => card.Id == cardId && card.UserId == userId,
            cancellationToken);

    public async Task AddAsync(CreditCard card, CancellationToken cancellationToken) =>
        await db.Cards.AddAsync(card, cancellationToken);
}
