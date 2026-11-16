using FinTrack.Modules.CashInflows.Application.Abstractions;
using FinTrack.Modules.CashInflows.Application.CashInflows;
using FinTrack.Modules.CashInflows.Application.Common;
using FinTrack.Modules.CashInflows.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CashInflows.Infrastructure.Queries;

internal sealed class CashInflowQueries(CashInflowsDbContext db) : ICashInflowQueries
{
    public async Task<PagedResult<CashInflowDto>> ListAsync(
        Guid userId,
        CashInflowFilter filter,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var query = db.CashInflows
            .AsNoTracking()
            .Where(inflow => inflow.UserId == userId);

        if (filter.From is not null)
        {
            query = query.Where(inflow => inflow.OccurredOn >= filter.From.Value);
        }

        if (filter.To is not null)
        {
            query = query.Where(inflow => inflow.OccurredOn <= filter.To.Value);
        }

        if (filter.Source is not null)
        {
            query = query.Where(inflow => inflow.Source == filter.Source.Value);
        }

        if (filter.Status is not null)
        {
            query = filter.Status.Value == CashInflowStatus.Cancelled
                ? query.Where(inflow => inflow.CancelledAtUtc != null)
                : query.Where(inflow => inflow.CancelledAtUtc == null);
        }
        else if (!filter.IncludeCancelled)
        {
            query = query.Where(inflow => inflow.CancelledAtUtc == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(inflow => inflow.OccurredOn)
            .ThenByDescending(inflow => inflow.RecordedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(CashInflowMapper.ToDto).ToList();

        return new PagedResult<CashInflowDto>(items, page, pageSize, totalCount);
    }

    public async Task<CashInflowDto?> FindAsync(
        Guid userId,
        Guid cashInflowId,
        CancellationToken cancellationToken)
    {
        var inflow = await db.CashInflows
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == cashInflowId && candidate.UserId == userId,
                cancellationToken);

        return inflow is null ? null : CashInflowMapper.ToDto(inflow);
    }
}
