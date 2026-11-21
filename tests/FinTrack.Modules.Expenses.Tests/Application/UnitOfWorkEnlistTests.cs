using System.Data.Common;
using System.Reflection;
using FinTrack.ModuleContracts;
using FinTrack.Modules.Expenses.Infrastructure.Persistence;
using FinTrack.Modules.Expenses.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace FinTrack.Modules.Expenses.Tests.Application;

/// <summary>
/// Expenses UnitOfWork için ADR-0010 Enlist guard'ı: aynı transaction tekrar
/// kaydedilmez, null accessor'a düşer, host temizlenince ayrılır. Diğer üç modül
/// kendi test projelerinde doğrulanır.
/// </summary>
public sealed class UnitOfWorkEnlistTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Ayni_transaction_icin_ikinci_enlist_baglantiyi_bozmaz()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var uow = new UnitOfWork(db);
        await using var scope = await BeginScopeAsync(db);

        uow.Enlist(scope);
        var first = db.Database.CurrentTransaction;
        Assert.NotNull(first);
        Assert.Same(scope.Transaction, first!.GetDbTransaction());
        Assert.Same(scope.Transaction, EnlistedTransaction(uow));

        // Aynı transaction ikinci kez kaydediliyor: referans değişmemeli.
        uow.Enlist(scope);
        Assert.Same(first, db.Database.CurrentTransaction);
        Assert.Same(scope.Transaction, EnlistedTransaction(uow));

        // Transaction hâlâ kullanılabilir; bağlantı bozulmadı.
        await scope.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Enlist_null_host_scope_yoksa_transaction_dan_ayrir()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var uow = new UnitOfWork(db);
        await using var scope = await BeginScopeAsync(db);

        uow.Enlist(scope);
        Assert.NotNull(db.Database.CurrentTransaction);

        uow.Enlist(null);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Null(EnlistedTransaction(uow));
    }

    [Fact]
    public async Task Enlist_null_host_scope_varsa_hosta_duser_sonra_temizlenince_ayrilir()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var accessor = new TestScopeAccessor();
        var uow = new UnitOfWork(db, accessor);
        await using var scope = await BeginScopeAsync(db);

        accessor.Current = scope;

        // Null scope, host accessor'a geri düşer.
        uow.Enlist(null);
        Assert.Same(scope.Transaction, db.Database.CurrentTransaction?.GetDbTransaction());
        Assert.Same(scope.Transaction, EnlistedTransaction(uow));

        // Host accessor temizlendikten sonra null, transaction'dan ayırır.
        accessor.Current = null;
        uow.Enlist(null);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Null(EnlistedTransaction(uow));
    }

    private static async Task<SqliteWriteTransactionScope> BeginScopeAsync(DbContext db)
    {
        var transaction = await db.Database.GetDbConnection().BeginTransactionAsync();
        return new SqliteWriteTransactionScope(transaction);
    }

    private static DbTransaction? EnlistedTransaction(UnitOfWork uow) =>
        (DbTransaction?)typeof(UnitOfWork)
            .GetField("_enlisted", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(uow);

    private sealed class SqliteWriteTransactionScope(DbTransaction transaction) : IWriteTransactionScope
    {
        public DbTransaction Transaction => transaction;

        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    private sealed class TestScopeAccessor : IWriteTransactionScopeAccessor
    {
        public IWriteTransactionScope? Current { get; set; }
    }
}
