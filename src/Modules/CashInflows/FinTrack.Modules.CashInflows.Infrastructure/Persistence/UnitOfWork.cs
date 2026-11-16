using FinTrack.Modules.CashInflows.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CashInflows.Infrastructure.Persistence;

internal sealed class UnitOfWork(CashInflowsDbContext db) : IUnitOfWork
{
    public async Task<SaveChangesOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return SaveChangesOutcome.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            return SaveChangesOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return SaveChangesOutcome.DuplicateKey;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
