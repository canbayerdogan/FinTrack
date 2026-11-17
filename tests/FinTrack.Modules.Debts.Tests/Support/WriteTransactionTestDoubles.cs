using System.Data.Common;
using FinTrack.ModuleContracts;
using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Tests.Support;

/// <summary>
/// ADR-0009 sözleşmelerinin SQLite karşılığı: gerçek açık transaction kullanır.
/// Ambient <c>TransactionScope</c> yerine açık <c>DbTransaction</c> tercih edilir.
/// </summary>
internal sealed class SqliteWriteConnectionProvider(DbConnection connection) : IWriteConnectionProvider
{
    public DbConnection Connection => connection;
}

internal sealed class SqliteWriteTransactionScopeFactory(DbConnection connection)
    : IWriteTransactionScopeFactory
{
    public SqliteWriteTransactionScope? LastScope { get; private set; }

    public async Task<IWriteTransactionScope> BeginAsync(CancellationToken cancellationToken)
    {
        var transaction = await connection.BeginTransactionAsync(cancellationToken);
        LastScope = new SqliteWriteTransactionScope(transaction);
        return LastScope;
    }
}

internal sealed class SqliteWriteTransactionScope(DbTransaction transaction) : IWriteTransactionScope
{
    public DbTransaction Transaction => transaction;

    public int CommitCount { get; private set; }

    public bool Disposed { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        CommitCount++;
        return transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return transaction.DisposeAsync();
    }
}

/// <summary>CashInflows writer'ını taklit eden, transaction'ı doğrulayan test çifti.</summary>
public sealed class FakeDebtCashInflowWriter : IDebtCashInflowWriter
{
    public Guid CreatedInflowId { get; set; } = Guid.NewGuid();

    public DebtInflowCreate? LastCreate { get; private set; }

    public DebtInflowCancel? LastCancel { get; private set; }

    public IWriteTransactionScope? LastCreateScope { get; private set; }

    public IWriteTransactionScope? LastCancelScope { get; private set; }

    public Result? NextCreateFailure { get; set; }

    public Result? NextCancelFailure { get; set; }

    public Task<Result<Guid>> CreateAsync(
        DebtInflowCreate request,
        IWriteTransactionScope scope,
        CancellationToken cancellationToken)
    {
        LastCreate = request;
        LastCreateScope = scope;

        if (NextCreateFailure is { } failure)
        {
            return Task.FromResult(Result.Failure<Guid>(failure.Error));
        }

        CreatedInflowId = Guid.NewGuid();
        return Task.FromResult(Result.Success(CreatedInflowId));
    }

    public Task<Result> CancelAsync(
        DebtInflowCancel request,
        IWriteTransactionScope scope,
        CancellationToken cancellationToken)
    {
        LastCancel = request;
        LastCancelScope = scope;

        return Task.FromResult(NextCancelFailure ?? Result.Success());
    }
}

/// <summary>Kayıt sayılır, commit çağrısını takip eder; rollback davranışını test eder.</summary>
public sealed class CountingUnitOfWork(IUnitOfWork inner) : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public SaveChangesOutcome? ForcedOutcome { get; set; }

    public void Enlist(IWriteTransactionScope? scope) => inner.Enlist(scope);

    public async Task<SaveChangesOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return ForcedOutcome ?? await inner.SaveChangesAsync(cancellationToken);
    }
}
