using System.Data;
using FinTrack.ModuleContracts;
using FinTrack.Modules.CreditCards.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure.Persistence;

internal sealed class UnitOfWork(CreditCardsDbContext db) : IUnitOfWork
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

internal sealed class TransactionScopeFactory(CreditCardsDbContext db) : ITransactionScopeFactory
{
    public async Task<ITransactionScope> BeginSerializableAsync(CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        return new EfTransactionScope(transaction);
    }
}

internal sealed class EfTransactionScope(
    Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) : ITransactionScope
{
    public Task CommitAsync(CancellationToken cancellationToken) =>
        transaction.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
