using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.Modules.CreditCards.Application.Payments;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure.Queries;

internal sealed class CardPaymentQueries(CreditCardsDbContext db) : ICardPaymentQueries
{
    public async Task<PagedResult<CardPaymentDto>> ListAsync(
        Guid userId,
        Guid cardId,
        CardPaymentFilter filter,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var query = db.CardPayments
            .AsNoTracking()
            .Where(payment => payment.UserId == userId && payment.CardId == cardId);

        if (filter.Status is not null)
        {
            var status = filter.Status.Value;
            query = status == PaymentStatus.Cancelled
                ? query.Where(payment => payment.CancelledAtUtc != null)
                : query.Where(payment => payment.CancelledAtUtc == null);
        }
        else if (!filter.IncludeCancelled)
        {
            query = query.Where(payment => payment.CancelledAtUtc == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var payments = await query
            .OrderByDescending(payment => payment.PaidOn)
            .ThenByDescending(payment => payment.RecordedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = payments.Select(CardPaymentMapper.ToDto).ToList();

        return new PagedResult<CardPaymentDto>(items, page, pageSize, totalCount);
    }

    public async Task<decimal> SumActivePaymentsAsync(
        Guid userId,
        Guid cardId,
        DateOnly? paidAfterExclusive,
        DateOnly asOfDate,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        // İptal aktifliği kesitte değerlendirilir: yerel iptal tarihi kesitten
        // sonraysa ödeme kesitte aktif sayılır. Sınır UTC'ye bir kez çevrilir.
        var cutoffUtc = UserTime.EndOfDayUtc(asOfDate, timeZoneId);

        var query = db.CardPayments
            .AsNoTracking()
            .Where(payment => payment.UserId == userId
                && payment.CardId == cardId
                && payment.PaidOn <= asOfDate
                && (payment.CancelledAtUtc == null || payment.CancelledAtUtc > cutoffUtc));

        if (paidAfterExclusive is not null)
        {
            var lowerBound = paidAfterExclusive.Value;
            query = query.Where(payment => payment.PaidOn > lowerBound);
        }

        return await query.SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;
    }
}
