using FinTrack.Modules.CashInflows.Application.Abstractions;
using FinTrack.Modules.CashInflows.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CashInflows.Infrastructure.Repositories;

internal sealed class CashInflowRepository(CashInflowsDbContext db) : ICashInflowRepository
{
    public Task<CashInflow?> FindAsync(
        Guid userId,
        Guid cashInflowId,
        CancellationToken cancellationToken) =>
        db.CashInflows.FirstOrDefaultAsync(
            inflow => inflow.Id == cashInflowId && inflow.UserId == userId,
            cancellationToken);

    public async Task AddAsync(CashInflow cashInflow, CancellationToken cancellationToken) =>
        await db.CashInflows.AddAsync(cashInflow, cancellationToken);
}
