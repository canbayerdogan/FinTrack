using System.Data.Common;
using FinTrack.ModuleContracts;
using FinTrack.Modules.CashInflows.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CashInflows.Infrastructure.Persistence;

internal sealed class UnitOfWork(
    CashInflowsDbContext db,
    IWriteTransactionScopeAccessor? scopeAccessor = null) : IUnitOfWork, IWriteScopeParticipant
{
    private DbTransaction? _enlisted;

    public void Enlist(IWriteTransactionScope? scope)
    {
        // ADR-0010: null çağrısı host transaction'ına geri düşer; host yoksa
        // mevcut ADR-0009 davranışı korunur (bağlantı temizlenir).
        var transaction = (scope ?? scopeAccessor?.Current)?.Transaction;
        if (ReferenceEquals(_enlisted, transaction))
        {
            return;
        }

        db.Database.UseTransaction(transaction);
        _enlisted = transaction;
    }

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
