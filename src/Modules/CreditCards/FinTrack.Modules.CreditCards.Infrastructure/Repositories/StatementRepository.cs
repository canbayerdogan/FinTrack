using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure.Repositories;

internal sealed class StatementRepository(CreditCardsDbContext db) : IStatementRepository
{
    public Task<Statement?> FindAsync(
        Guid userId,
        Guid cardId,
        Guid statementId,
        CancellationToken cancellationToken) =>
        db.Statements.FirstOrDefaultAsync(
            statement => statement.Id == statementId
                && statement.CardId == cardId
                && statement.UserId == userId,
            cancellationToken);

    public Task<bool> HasOverlappingActiveStatementAsync(
        Guid userId,
        Guid cardId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken) =>
        // Yalnızca aktif ekstreler engel oluşturur; gün kümeleri kesişiyorsa çakışır
        // (başlangıç ve bitiş dahil). İptal edilmiş ekstre engel değildir.
        db.Statements.AnyAsync(
            statement => statement.UserId == userId
                && statement.CardId == cardId
                && statement.CancelledAtUtc == null
                && statement.PeriodStart <= periodEnd
                && statement.PeriodEnd >= periodStart,
            cancellationToken);

    public async Task AddAsync(Statement statement, CancellationToken cancellationToken) =>
        await db.Statements.AddAsync(statement, cancellationToken);
}
