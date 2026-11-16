using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure.Repositories;

internal sealed class CardPaymentRepository(CreditCardsDbContext db) : ICardPaymentRepository
{
    public Task<CardPayment?> FindAsync(
        Guid userId,
        Guid cardId,
        Guid paymentId,
        CancellationToken cancellationToken) =>
        db.CardPayments.FirstOrDefaultAsync(
            payment => payment.Id == paymentId
                && payment.CardId == cardId
                && payment.UserId == userId,
            cancellationToken);

    public async Task AddAsync(CardPayment payment, CancellationToken cancellationToken) =>
        await db.CardPayments.AddAsync(payment, cancellationToken);
}
