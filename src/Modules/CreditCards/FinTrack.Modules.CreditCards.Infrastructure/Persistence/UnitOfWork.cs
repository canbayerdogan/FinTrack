using System.Data;
using System.Data.Common;
using FinTrack.ModuleContracts;
using FinTrack.Modules.CreditCards.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure.Persistence;

internal sealed class UnitOfWork(
    CreditCardsDbContext db,
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

internal sealed class TransactionScopeFactory(
    CreditCardsDbContext db,
    IWriteTransactionScopeAccessor? scopeAccessor = null) : ITransactionScopeFactory
{
    public Task<ITransactionScope> BeginSerializableAsync(CancellationToken cancellationToken)
    {
        // ADR-0010 kural 3: host transaction varken modül ikinci transaction açmaz.
        if (scopeAccessor?.Current is { } hostScope)
        {
            return Task.FromResult<ITransactionScope>(new HostTransactionScope());
        }

        return BeginCoreAsync(cancellationToken);
    }

    private async Task<ITransactionScope> BeginCoreAsync(CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        return new EfTransactionScope(transaction);
    }
}

/// <summary>Host scope varken handler'ın <c>await using</c> bloğunu bozan no-op sarmalayıcı.</summary>
internal sealed class HostTransactionScope : ITransactionScope
{
    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class EfTransactionScope(
    Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) : ITransactionScope
{
    public Task CommitAsync(CancellationToken cancellationToken) =>
        transaction.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
