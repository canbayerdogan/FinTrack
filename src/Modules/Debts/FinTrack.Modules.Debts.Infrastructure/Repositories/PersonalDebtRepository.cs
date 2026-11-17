using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.Modules.Debts.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Debts.Infrastructure.Repositories;

internal sealed class PersonalDebtRepository(DebtsDbContext db) : IPersonalDebtRepository
{
    public Task<PersonalDebt?> FindAsync(Guid userId, Guid debtId, CancellationToken cancellationToken) =>
        db.PersonalDebts
            .Include(debt => debt.Repayments)
            .FirstOrDefaultAsync(
                debt => debt.Id == debtId && debt.UserId == userId,
                cancellationToken);

    public async Task AddAsync(PersonalDebt debt, CancellationToken cancellationToken) =>
        await db.PersonalDebts.AddAsync(debt, cancellationToken);

    public async Task AddRepaymentAsync(
        PersonalDebtRepayment repayment,
        CancellationToken cancellationToken) =>
        await db.Repayments.AddAsync(repayment, cancellationToken);
}
