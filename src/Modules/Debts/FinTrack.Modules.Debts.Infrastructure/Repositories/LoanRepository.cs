using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.Modules.Debts.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Debts.Infrastructure.Repositories;

internal sealed class LoanRepository(DebtsDbContext db) : ILoanRepository
{
    public Task<Loan?> FindAsync(Guid userId, Guid loanId, CancellationToken cancellationToken) =>
        db.Loans
            .Include(loan => loan.Installments)
            .ThenInclude(installment => installment.Payments)
            .FirstOrDefaultAsync(
                loan => loan.Id == loanId && loan.UserId == userId,
                cancellationToken);

    public async Task AddAsync(Loan loan, CancellationToken cancellationToken) =>
        await db.Loans.AddAsync(loan, cancellationToken);

    public async Task AddPaymentAsync(
        InstallmentPayment payment,
        CancellationToken cancellationToken) =>
        await db.InstallmentPayments.AddAsync(payment, cancellationToken);
}
