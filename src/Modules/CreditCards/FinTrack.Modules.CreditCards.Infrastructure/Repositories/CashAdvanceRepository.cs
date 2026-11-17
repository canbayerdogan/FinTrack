using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure.Repositories;

internal sealed class CashAdvanceRepository(CreditCardsDbContext db) : ICashAdvanceRepository
{
    public Task<CashAdvance?> FindAsync(
        Guid userId,
        Guid cardId,
        Guid cashAdvanceId,
        CancellationToken cancellationToken) =>
        db.CashAdvances.FirstOrDefaultAsync(
            advance => advance.Id == cashAdvanceId
                && advance.CardId == cardId
                && advance.UserId == userId,
            cancellationToken);

    public async Task AddAsync(CashAdvance advance, CancellationToken cancellationToken) =>
        await db.CashAdvances.AddAsync(advance, cancellationToken);
}
