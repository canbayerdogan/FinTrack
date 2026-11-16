using FinTrack.ModuleContracts;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Expenses.Infrastructure.Queries;

/// <summary>
/// Kart borcu için Expenses'ın sahip olduğu veriyi <see cref="ICardExpenseQuery"/>
/// üzerinden okur (ADR-0008). Yalnızca aktif harcama ve iadeler sayılır;
/// iptal edilmiş harcamanın iadesi de sayılmaz.
/// </summary>
internal sealed class CardExpenseQuery(ExpensesDbContext db) : ICardExpenseQuery
{
    public async Task<CardExpenseTotals> GetCardTotalsAsync(
        Guid userId,
        Guid cardId,
        CancellationToken cancellationToken)
    {
        var expenses = await db.Expenses
            .AsNoTracking()
            .Where(expense => expense.UserId == userId
                && expense.CardId == cardId
                && expense.CancelledAtUtc == null)
            .SumAsync(expense => (decimal?)expense.Amount, cancellationToken) ?? 0m;

        var refunds = await db.Refunds
            .AsNoTracking()
            .Where(refund => refund.UserId == userId
                && refund.CancelledAtUtc == null
                && db.Expenses.Any(expense => expense.Id == refund.ExpenseId
                    && expense.UserId == userId
                    && expense.CardId == cardId
                    && expense.CancelledAtUtc == null))
            .SumAsync(refund => (decimal?)refund.Amount, cancellationToken) ?? 0m;

        return new CardExpenseTotals(expenses, refunds);
    }

    public async Task<CardExpenseTotals> GetCardTotalsAsOfAsync(
        Guid userId,
        Guid cardId,
        DateOnly asOfDate,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        // İptal aktifliği kesitte değerlendirilir: yerel iptal tarihi kesitten
        // sonraysa kayıt kesitte aktif sayılır. Sınır UTC'ye bir kez çevrilir;
        // böylece EF tarafında saat dilimi çevirisi gerekmez.
        var cutoffUtc = UserTime.EndOfDayUtc(asOfDate, timeZoneId);

        var expenses = await db.Expenses
            .AsNoTracking()
            .Where(expense => expense.UserId == userId
                && expense.CardId == cardId
                && expense.OccurredOn <= asOfDate
                && (expense.CancelledAtUtc == null || expense.CancelledAtUtc > cutoffUtc))
            .SumAsync(expense => (decimal?)expense.Amount, cancellationToken) ?? 0m;

        var refunds = await db.Refunds
            .AsNoTracking()
            .Where(refund => refund.UserId == userId
                && refund.RefundedOn <= asOfDate
                && (refund.CancelledAtUtc == null || refund.CancelledAtUtc > cutoffUtc)
                && db.Expenses.Any(expense => expense.Id == refund.ExpenseId
                    && expense.UserId == userId
                    && expense.CardId == cardId
                    && (expense.CancelledAtUtc == null || expense.CancelledAtUtc > cutoffUtc)))
            .SumAsync(refund => (decimal?)refund.Amount, cancellationToken) ?? 0m;

        return new CardExpenseTotals(expenses, refunds);
    }
}
