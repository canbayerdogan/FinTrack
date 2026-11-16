using System.Linq.Expressions;
using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.Modules.CreditCards.Application.Statements;
using FinTrack.Modules.CreditCards.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure.Queries;

internal sealed class StatementQueries(CreditCardsDbContext db) : IStatementQueries
{
    public async Task<PagedResult<StatementRecord>> ListAsync(
        Guid userId,
        Guid cardId,
        StatementFilter filter,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var query = db.Statements
            .AsNoTracking()
            .Where(statement => statement.UserId == userId && statement.CardId == cardId);

        if (filter.Status is not null)
        {
            var status = filter.Status.Value;
            query = status == StatementStatus.Cancelled
                ? query.Where(statement => statement.CancelledAtUtc != null)
                : query.Where(statement => statement.CancelledAtUtc == null);
        }
        else if (!filter.IncludeCancelled)
        {
            query = query.Where(statement => statement.CancelledAtUtc == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var records = await query
            .OrderByDescending(statement => statement.PeriodEnd)
            .ThenByDescending(statement => statement.RecordedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<StatementRecord>(records, page, pageSize, totalCount);
    }

    public Task<StatementRecord?> FindAsync(
        Guid userId,
        Guid cardId,
        Guid statementId,
        CancellationToken cancellationToken) =>
        db.Statements
            .AsNoTracking()
            .Where(statement => statement.UserId == userId
                && statement.CardId == cardId
                && statement.Id == statementId)
            .Select(Projection)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<StatementRecord?> FindPreviousActiveAsync(
        Guid userId,
        Guid cardId,
        DateOnly beforePeriodEnd,
        CancellationToken cancellationToken) =>
        db.Statements
            .AsNoTracking()
            .Where(statement => statement.UserId == userId
                && statement.CardId == cardId
                && statement.CancelledAtUtc == null
                && statement.PeriodEnd < beforePeriodEnd)
            .OrderByDescending(statement => statement.PeriodEnd)
            .Select(Projection)
            .FirstOrDefaultAsync(cancellationToken);

    private static readonly Expression<Func<Statement, StatementRecord>> Projection = statement => new StatementRecord(
            statement.Id,
            statement.CardId,
            statement.PeriodStart,
            statement.PeriodEnd,
            statement.DueDate,
            statement.StatementAmount,
            statement.Currency,
            statement.RecordedAtUtc,
            statement.CancelledAtUtc,
            statement.CancellationReason,
            statement.RowVersion);
}
