using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.Modules.Debts.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Debts.Infrastructure.Queries;

/// <summary>
/// Reporting sözleşmesi için salt okunur borç toplamı. İptal edilen borçlar
/// hariç tutulur; kalan tutarlar aggregate'lerden türetilir, saklanmaz.
/// </summary>
internal sealed class DebtSummaryQueries(DebtsDbContext db) : IDebtSummaryQueries
{
    public async Task<decimal> GetTotalRemainingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var loans = await db.Loans
            .AsNoTracking()
            .Where(loan => loan.UserId == userId && loan.Status != LoanStatus.Cancelled)
            .Include(loan => loan.Installments)
            .ToListAsync(cancellationToken);

        var debts = await db.PersonalDebts
            .AsNoTracking()
            .Where(debt => debt.UserId == userId && debt.Status != PersonalDebtStatus.Cancelled)
            .Include(debt => debt.Repayments)
            .ToListAsync(cancellationToken);

        return loans.Sum(loan => loan.RemainingAmount)
            + debts.Sum(debt => debt.RemainingAmount);
    }
}
