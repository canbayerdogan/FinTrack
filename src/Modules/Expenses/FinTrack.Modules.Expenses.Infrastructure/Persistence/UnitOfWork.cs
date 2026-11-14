using System.Data;
using FinTrack.Modules.Expenses.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Expenses.Infrastructure.Persistence;

internal sealed class UnitOfWork(ExpensesDbContext db) : IUnitOfWork
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

internal sealed class TransactionScopeFactory(ExpensesDbContext db) : ITransactionScopeFactory
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
