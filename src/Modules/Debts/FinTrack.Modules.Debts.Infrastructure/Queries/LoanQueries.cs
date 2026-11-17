using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.Modules.Debts.Application.Common;
using FinTrack.Modules.Debts.Application.Loans;
using FinTrack.Modules.Debts.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Debts.Infrastructure.Queries;

internal sealed class LoanQueries(DebtsDbContext db) : ILoanQueries
{
    public async Task<PagedResult<LoanDto>> ListAsync(
        Guid userId,
        LoanFilter filter,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var query = db.Loans
            .AsNoTracking()
            .Where(loan => loan.UserId == userId);

        if (filter.Status is not null)
        {
            query = query.Where(loan => loan.Status == filter.Status.Value);
        }
        else if (!filter.IncludeCancelled)
        {
            query = query.Where(loan => loan.Status != LoanStatus.Cancelled);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .Include(loan => loan.Installments)
            .ThenInclude(installment => installment.Payments)
            .OrderByDescending(loan => loan.RecordedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(LoanMapper.ToDto).ToList();
        return new PagedResult<LoanDto>(items, page, pageSize, totalCount);
    }

    public async Task<LoanDto?> FindAsync(Guid userId, Guid loanId, CancellationToken cancellationToken)
    {
        var loan = await db.Loans
            .AsNoTracking()
            .Include(loan => loan.Installments)
            .ThenInclude(installment => installment.Payments)
            .FirstOrDefaultAsync(
                candidate => candidate.Id == loanId && candidate.UserId == userId,
                cancellationToken);

        return loan is null ? null : LoanMapper.ToDto(loan);
    }
}
