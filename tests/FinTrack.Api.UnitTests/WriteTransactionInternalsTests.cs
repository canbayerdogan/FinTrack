using System.Data;
using System.Data.Common;
using System.Reflection;
using FinTrack.Api.Idempotency;
using FinTrack.ModuleContracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinTrack.Api.UnitTests;

/// <summary>
/// Host'un internal ADR-0010 yardımcıları için davranış testleri. Tipler internal
/// olduğundan ve host kaynağı değiştirilmeyeceğinden yansıma (reflection) ile
/// erişilir; InternalsVisibleTo eklenmez. Ayrıca IdempotencyStore'un internal
/// hata sınıflandırma yardımcılarının mesaj tabanlı dalları örtülür.
/// </summary>
public sealed class WriteTransactionInternalsTests
{
    private static readonly Assembly HostAssembly = typeof(IdempotencyScope).Assembly;

    [Fact]
    public void WriteTransactionScopeAccessor_baslangicta_bos_sonra_set_edilen_scope_u_tasir()
    {
        var accessor = (IWriteTransactionScopeAccessor)CreateInternal(
            "FinTrack.Api.WriteTransactions.WriteTransactionScopeAccessor");

        Assert.Null(accessor.Current);
        Assert.Contains(
            accessor.GetType().GetInterfaces(),
            iface => iface.Name == "IWriteTransactionScopeSetter");

        var set = accessor.GetType().GetMethod(
            "Set", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(set);

        var scope = new FakeWriteTransactionScope();
        set.Invoke(accessor, [scope]);
        Assert.Same(scope, accessor.Current);

        set.Invoke(accessor, [null]);
        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task NoOpWriteTransactionScope_host_transactionini_paylasir_ve_commit_dispose_etmez()
    {
        var host = new FakeWriteTransactionScope();
        var noOp = (IWriteTransactionScope)CreateInternal(
            "FinTrack.Api.WriteTransactions.NoOpWriteTransactionScope", host);

        Assert.Same(host.Transaction, noOp.Transaction);

        await noOp.CommitAsync(CancellationToken.None);
        await noOp.DisposeAsync();

        Assert.Equal(0, host.CommitCount);
        Assert.Equal(0, host.DisposeCount);
    }

    [Fact]
    public async Task Host_scope_varken_fabric_ikinci_transaction_acmaz()
    {
        var accessor = (IWriteTransactionScopeAccessor)CreateInternal(
            "FinTrack.Api.WriteTransactions.WriteTransactionScopeAccessor");
        var set = accessor.GetType().GetMethod(
            "Set", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

        var host = new FakeWriteTransactionScope();
        set.Invoke(accessor, [host]);

        var factory = (IWriteTransactionScopeFactory)CreateInternal(
            "FinTrack.Api.WriteTransactions.SqlWriteTransactionScopeFactory",
            new FakeConnectionProvider(),
            accessor);

        var scope = await factory.BeginAsync(CancellationToken.None);

        Assert.Same(host.Transaction, scope.Transaction);
        await scope.CommitAsync(CancellationToken.None);
        await scope.DisposeAsync();
        Assert.Equal(0, host.CommitCount);
        Assert.Equal(0, host.DisposeCount);
    }

    [Theory]
    [InlineData("UNIQUE constraint failed: shared.IdempotencyKeys")]
    [InlineData("duplicate key value violates unique constraint")]
    public void Store_UNIQUE_ihlalini_dogru_siniflandirir(string innerMessage)
    {
        Assert.True(Classify("IsUniqueViolation", innerMessage));
        Assert.False(Classify("IsLockTimeout", innerMessage));
    }

    [Theory]
    [InlineData("Lock timeout expired")]
    [InlineData("Command timeout expired")]
    public void Store_kilit_timeout_unu_dogru_siniflandirir(string innerMessage)
    {
        Assert.True(Classify("IsLockTimeout", innerMessage));
        Assert.False(Classify("IsUniqueViolation", innerMessage));
    }

    [Fact]
    public void Store_ilgisiz_veya_yok_ici_hata_hicbir_sinifa_girmez()
    {
        Assert.False(Classify("IsUniqueViolation", "some unrelated failure"));
        Assert.False(Classify("IsLockTimeout", "some unrelated failure"));
        Assert.False(Classify("IsUniqueViolation", null));
        Assert.False(Classify("IsLockTimeout", null));
    }

    private static object CreateInternal(string fullName, params object?[] args)
    {
        var type = HostAssembly.GetType(fullName, throwOnError: true)!;
        return Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: args,
            culture: null)!;
    }

    private static bool Classify(string methodName, string? innerMessage)
    {
        var type = HostAssembly.GetType("FinTrack.Api.Idempotency.IdempotencyStore", throwOnError: true)!;
        var method = type.GetMethod(
            methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);

        Exception? inner = innerMessage is null ? null : new InvalidOperationException(innerMessage);
        var exception = new DbUpdateException("kayıt yazılamadı", inner);

        return (bool)method.Invoke(null, [exception])!;
    }

    private sealed class FakeConnectionProvider : IWriteConnectionProvider
    {
        public DbConnection Connection =>
            throw new InvalidOperationException("Host scope varken bağlantı açılmamalı.");
    }

    private sealed class FakeWriteTransactionScope : IWriteTransactionScope
    {
        public FakeWriteTransactionScope(DbTransaction? transaction = null) =>
            Transaction = transaction ?? new FakeDbTransaction();

        public DbTransaction Transaction { get; }

        public int CommitCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeDbTransaction : DbTransaction
    {
        protected override DbConnection? DbConnection => null;

        public override IsolationLevel IsolationLevel => IsolationLevel.Serializable;

        public override void Commit()
        {
        }

        public override void Rollback()
        {
        }
    }
}
