using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.Modules.Debts.Application.Common;
using FinTrack.Modules.Debts.Application.PersonalDebts;
using FinTrack.Modules.Debts.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Debts.Infrastructure.Queries;

internal sealed class PersonalDebtQueries(DebtsDbContext db) : IPersonalDebtQueries
{
    public async Task<PagedResult<PersonalDebtDto>> ListAsync(
        Guid userId,
        PersonalDebtFilter filter,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var query = db.PersonalDebts
            .AsNoTracking()
            .Where(debt => debt.UserId == userId);

        if (filter.Status is not null)
        {
            query = query.Where(debt => debt.Status == filter.Status.Value);
        }
        else if (!filter.IncludeCancelled)
        {
            query = query.Where(debt => debt.Status != PersonalDebtStatus.Cancelled);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .Include(debt => debt.Repayments)
            .OrderByDescending(debt => debt.RecordedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(PersonalDebtMapper.ToDto).ToList();
        return new PagedResult<PersonalDebtDto>(items, page, pageSize, totalCount);
    }

    public async Task<PersonalDebtDto?> FindAsync(Guid userId, Guid debtId, CancellationToken cancellationToken)
    {
        var debt = await db.PersonalDebts
            .AsNoTracking()
            .Include(debt => debt.Repayments)
            .FirstOrDefaultAsync(
                candidate => candidate.Id == debtId && candidate.UserId == userId,
                cancellationToken);

        return debt is null ? null : PersonalDebtMapper.ToDto(debt);
    }

    public async Task<IReadOnlyList<RepaymentDto>> ListRepaymentsAsync(
        Guid userId,
        Guid debtId,
        CancellationToken cancellationToken)
    {
        var rows = await db.Repayments
            .AsNoTracking()
            .Where(repayment => repayment.UserId == userId && repayment.PersonalDebtId == debtId)
            .OrderByDescending(repayment => repayment.PaidOn)
            .ThenByDescending(repayment => repayment.RecordedAtUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(PersonalDebtMapper.ToRepaymentDto).ToList();
    }
}
