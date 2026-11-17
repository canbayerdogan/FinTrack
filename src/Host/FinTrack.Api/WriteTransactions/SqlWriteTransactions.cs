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

internal sealed class SqlWriteTransactionScopeFactory(IWriteConnectionProvider connectionProvider)
    : IWriteTransactionScopeFactory
{
    public async Task<IWriteTransactionScope> BeginAsync(CancellationToken cancellationToken)
    {
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
