using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.CashAdvances;
using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure.Queries;

internal sealed class CashAdvanceQueries(CreditCardsDbContext db) : ICashAdvanceQueries
{
    public async Task<PagedResult<CashAdvanceDto>> ListAsync(
        Guid userId,
        Guid cardId,
        CashAdvanceFilter filter,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var query = db.CashAdvances
            .AsNoTracking()
            .Where(advance => advance.UserId == userId && advance.CardId == cardId);

        if (filter.Status is not null)
        {
            var status = filter.Status.Value;
            query = status == CashAdvanceStatus.Cancelled
                ? query.Where(advance => advance.CancelledAtUtc != null)
                : query.Where(advance => advance.CancelledAtUtc == null);
        }
        else if (!filter.IncludeCancelled)
        {
            query = query.Where(advance => advance.CancelledAtUtc == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var advances = await query
            .OrderByDescending(advance => advance.TakenOn)
            .ThenByDescending(advance => advance.RecordedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = advances.Select(CashAdvanceMapper.ToDto).ToList();

        return new PagedResult<CashAdvanceDto>(items, page, pageSize, totalCount);
    }

    public async Task<decimal> SumActiveAdvancesAsync(
        Guid userId,
        Guid cardId,
        DateOnly? takenAfterExclusive,
        DateOnly asOfDate,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        // İptal aktifliği kesitte değerlendirilir: yerel iptal tarihi kesitten
        // sonraysa avans kesitte aktif sayılır. Sınır UTC'ye bir kez çevrilir.
        var cutoffUtc = UserTime.EndOfDayUtc(asOfDate, timeZoneId);

        var query = db.CashAdvances
            .AsNoTracking()
            .Where(advance => advance.UserId == userId
                && advance.CardId == cardId
                && advance.TakenOn <= asOfDate
                && (advance.CancelledAtUtc == null || advance.CancelledAtUtc > cutoffUtc));

        if (takenAfterExclusive is not null)
        {
            var lowerBound = takenAfterExclusive.Value;
            query = query.Where(advance => advance.TakenOn > lowerBound);
        }

        return await query.SumAsync(advance => (decimal?)advance.Amount, cancellationToken) ?? 0m;
    }
}
