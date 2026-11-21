using System.Data.Common;
using System.Reflection;
using FinTrack.ModuleContracts;
using FinTrack.Modules.CreditCards.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using CashInflowsUnitOfWork = FinTrack.Modules.CashInflows.Infrastructure.Persistence.UnitOfWork;
using CreditCardsUnitOfWork = FinTrack.Modules.CreditCards.Infrastructure.Persistence.UnitOfWork;

namespace FinTrack.Modules.CreditCards.Tests.Application;

/// <summary>
/// Dört UnitOfWork'ten ikisi (CreditCards, CashInflows) için ADR-0010 Enlist guard'ı:
/// aynı transaction tekrar kaydedilmez, null accessor'a düşer, host temizlenince ayrılır.
/// </summary>
public sealed class UnitOfWorkEnlistTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Theory]
    [InlineData("CreditCards")]
    [InlineData("CashInflows")]
    public async Task Ayni_transaction_icin_ikinci_enlist_baglantiyi_bozmaz(string module)
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using DbContext db = module == "CreditCards"
            ? database.CreateContext(UserId)
            : database.CreateInflowContext(UserId);

        var uow = CreateUow(module, db);
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

        // Transaction hâlâ kullanılabilir; yield bozulmadı.
        await scope.CommitAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("CreditCards")]
    [InlineData("CashInflows")]
    public async Task Enlist_null_host_scope_yoksa_transaction_dan_ayrir(string module)
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using DbContext db = module == "CreditCards"
            ? database.CreateContext(UserId)
            : database.CreateInflowContext(UserId);

        var uow = CreateUow(module, db);
        await using var scope = await BeginScopeAsync(db);

        uow.Enlist(scope);
        Assert.NotNull(db.Database.CurrentTransaction);

        uow.Enlist(null);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Null(EnlistedTransaction(uow));
    }

    [Theory]
    [InlineData("CreditCards")]
    [InlineData("CashInflows")]
    public async Task Enlist_null_host_scope_varsa_hosta_duser_sonra_temizlenince_ayrilir(string module)
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using DbContext db = module == "CreditCards"
            ? database.CreateContext(UserId)
            : database.CreateInflowContext(UserId);

        var accessor = new TestScopeAccessor();
        var uow = CreateUow(module, db, accessor);
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

    private static IWriteScopeParticipant CreateUow(string module, DbContext db, IWriteTransactionScopeAccessor? accessor = null) =>
        module == "CreditCards"
            ? new CreditCardsUnitOfWork((FinTrack.Modules.CreditCards.Infrastructure.CreditCardsDbContext)db, accessor)
            : new CashInflowsUnitOfWork((FinTrack.Modules.CashInflows.Infrastructure.CashInflowsDbContext)db, accessor);

    private static async Task<SqliteWriteTransactionScope> BeginScopeAsync(DbContext db)
    {
        var transaction = await db.Database.GetDbConnection().BeginTransactionAsync();
        return new SqliteWriteTransactionScope(transaction);
    }

    private static DbTransaction? EnlistedTransaction(object uow) =>
        (DbTransaction?)uow.GetType()
            .GetField("_enlisted", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(uow);

    private sealed class TestScopeAccessor : IWriteTransactionScopeAccessor
    {
        public IWriteTransactionScope? Current { get; set; }
    }
}
