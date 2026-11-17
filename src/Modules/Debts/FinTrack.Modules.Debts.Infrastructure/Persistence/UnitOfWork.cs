using FinTrack.ModuleContracts;
using FinTrack.Modules.Debts.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Debts.Infrastructure.Persistence;

internal sealed class UnitOfWork(DebtsDbContext db) : IUnitOfWork
{
    public void Enlist(IWriteTransactionScope? scope) => db.Database.UseTransaction(scope?.Transaction);

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
