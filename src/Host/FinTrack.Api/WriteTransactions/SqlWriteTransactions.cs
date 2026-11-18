using System.Data;
using System.Data.Common;
using FinTrack.ModuleContracts;
using Microsoft.Data.SqlClient;

namespace FinTrack.Api.WriteTransactions;

/// <summary>
/// İstek kapsamında tek SQL bağlantısı (ADR-0009). Bağlantı DI tarafından
/// kapatılır; transaction scope yalnızca transaction'ı kapatır.
/// </summary>
internal sealed class SqlWriteConnectionProvider(string connectionString) : IWriteConnectionProvider, IDisposable
{
    private readonly SqlConnection _connection = new(connectionString);

    public DbConnection Connection => _connection;

    public void Dispose() => _connection.Dispose();
}

/// <summary>Host middleware'inin aktif scope'u ayarlaması için host içi sözleşme.</summary>
internal interface IWriteTransactionScopeSetter : IWriteTransactionScopeAccessor
{
    void Set(IWriteTransactionScope? scope);
}

/// <summary>
/// ADR-0010: aktif host transaction'ını istek kapsamında taşır. Modüller yalnızca
/// <see cref="IWriteTransactionScopeAccessor"/> görür; setter host'a özeldir.
/// </summary>
internal sealed class WriteTransactionScopeAccessor : IWriteTransactionScopeSetter
{
    public IWriteTransactionScope? Current { get; private set; }

    public void Set(IWriteTransactionScope? scope) => Current = scope;
}

internal sealed class SqlWriteTransactionScopeFactory(
    IWriteConnectionProvider connectionProvider,
    IWriteTransactionScopeAccessor scopeAccessor)
    : IWriteTransactionScopeFactory
{
    public async Task<IWriteTransactionScope> BeginAsync(CancellationToken cancellationToken)
    {
        // ADR-0010 kural 5: host transaction varken modül ikinci transaction açmaz.
        if (scopeAccessor.Current is { } hostScope)
        {
            return new NoOpWriteTransactionScope(hostScope);
        }

        var connection = connectionProvider.Connection;
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        return new WriteTransactionScope(transaction);
    }
}

internal sealed class WriteTransactionScope(DbTransaction transaction) : IWriteTransactionScope
{
    public DbTransaction Transaction => transaction;

    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    /// <summary>Commit edilmemişse dispose otomatik rollback yapar.</summary>
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}

/// <summary>
/// Host transaction varken modül handler'larının <c>await using</c> bloklarının
/// bozulmadan çalışmasını sağlayan no-op sarmalayıcı (ADR-0010 kural 5).
/// Commit ve dispose host middleware'ine aittir.
/// </summary>
internal sealed class NoOpWriteTransactionScope(IWriteTransactionScope hostScope) : IWriteTransactionScope
{
    public DbTransaction Transaction => hostScope.Transaction;

    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
