using System.Data.Common;
using System.Text;
using FinTrack.Api.Idempotency;
using FinTrack.Api.WriteTransactions;
using FinTrack.ModuleContracts;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinTrack.Api.UnitTests;

/// <summary>
/// Audit F-06/F-07: host middleware'ının participant kaydı ve gerçek transaction
/// commit sınırındaki hata davranışı. Host içi tipler <c>InternalsVisibleTo</c> ile
/// doğrudan erişilebilir; Docker/Testcontainers gerekmez.
/// </summary>
public sealed class IdempotencyFailureBoundaryTests
{
    [Fact]
    public async Task Enlist_istisnasi_mevcut_politikada_yayilir_ve_hicbir_kayit_yazilmaz()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var idempotencyDb = CreateIdempotencyDb(connection);
        var context = CreateContext();
        var scope = new RecordingScope(transaction);
        var failing = new RecordingParticipant(throwOnEnlist: true);
        var second = new RecordingParticipant();
        var store = new FakeStore();
        var setter = new FakeScopeSetter();
        var middleware = CreateMiddleware(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeAsync(
            middleware, context, store, scope, setter, idempotencyDb, [failing, second]));

        // F-06: kayıt döngüsü try/catch içinde olmadığı için istisna yayılır (500).
        // Politika değiştirilirse bu test ve audit bulgusu birlikte güncellenir.
        Assert.Equal(0, store.InsertCount);
        Assert.Equal(0, store.CompleteCount);
        Assert.Equal(0, scope.CommitCount);
        Assert.Equal(1, scope.DisposeCount);
        Assert.Null(setter.Current);
        Assert.Equal(1, failing.EnlistCount);
        Assert.Equal(0, second.EnlistCount);
        Assert.Equal(1, failing.DetachCount);
        Assert.Equal(1, second.DetachCount);
    }

    [Fact]
    public async Task Gercek_commit_istisnasi_409_InProgress_doner_ve_scope_kapanir()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var idempotencyDb = CreateIdempotencyDb(connection);
        var context = CreateContext();
        var scope = new RecordingScope(transaction, throwOnCommit: true);
        var store = new FakeStore();
        var setter = new FakeScopeSetter();
        var middleware = CreateMiddleware(context);

        await InvokeAsync(middleware, context, store, scope, setter, idempotencyDb, [new RecordingParticipant()]);

        // F-07: commit sonucu belirsizse 409 + "aynı anahtarla tekrar dene" yanıtı.
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal(1, scope.CommitCount);
        Assert.Equal(1, scope.DisposeCount);
        Assert.Null(setter.Current);
        Assert.Equal(1, store.CompleteCount);
        Assert.Equal(0, store.TombstoneCount);
    }

    private static IdempotencyDbContext CreateIdempotencyDb(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<IdempotencyDbContext>().UseSqlite(connection).Options);

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/expenses";
        context.Request.Headers[IdempotencyScope.HeaderName] = Guid.NewGuid().ToString("D");
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection()
            .AddProblemDetails()
            .AddLogging()
            .BuildServiceProvider();
        return context;
    }

    private static IdempotencyMiddleware CreateMiddleware(HttpContext context) =>
        new(_ =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }, NullLogger<IdempotencyMiddleware>.Instance);

    private static Task InvokeAsync(
        IdempotencyMiddleware middleware,
        HttpContext context,
        IIdempotencyStore store,
        IWriteTransactionScope scope,
        IWriteTransactionScopeSetter setter,
        IdempotencyDbContext idempotencyDb,
        IEnumerable<IWriteScopeParticipant> participants) =>
        middleware.InvokeAsync(
            context,
            new FakeCurrentUser(Guid.NewGuid()),
            store,
            new FakeScopeFactory(scope),
            setter,
            idempotencyDb,
            participants,
            new FakeCleanup(),
            new FakeHooks());

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;

        public bool IsAuthenticated => true;

        public string PreferredTimeZone => "UTC";

        public Guid RequireUserId() => userId;
    }

    private sealed class FakeScopeFactory(IWriteTransactionScope scope) : IWriteTransactionScopeFactory
    {
        public Task<IWriteTransactionScope> BeginAsync(CancellationToken cancellationToken) =>
            Task.FromResult(scope);
    }

    private sealed class FakeScopeSetter : IWriteTransactionScopeSetter
    {
        public IWriteTransactionScope? Current { get; private set; }

        public void Set(IWriteTransactionScope? scope) => Current = scope;
    }

    private sealed class RecordingScope(DbTransaction transaction, bool throwOnCommit = false)
        : IWriteTransactionScope
    {
        public DbTransaction Transaction => transaction;

        public int CommitCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            CommitCount++;
            return throwOnCommit
                ? Task.FromException(new InvalidOperationException("Commit sırasında bağlantı koptu."))
                : Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingParticipant(bool throwOnEnlist = false) : IWriteScopeParticipant
    {
        public int EnlistCount { get; private set; }

        public int DetachCount { get; private set; }

        public void Enlist(IWriteTransactionScope? scope)
        {
            if (scope is null)
            {
                DetachCount++;
                return;
            }

            EnlistCount++;
            if (throwOnEnlist)
            {
                throw new InvalidOperationException("Enlist sırasında participant kaydı başarısız.");
            }
        }
    }

    private sealed class FakeStore : IIdempotencyStore
    {
        public int InsertCount { get; private set; }

        public int CompleteCount { get; private set; }

        public int TombstoneCount { get; private set; }

        public Task<IdempotencyKeyRecord?> FindAsync(
            string scopeHash, string key, CancellationToken cancellationToken) =>
            Task.FromResult<IdempotencyKeyRecord?>(null);

        public Task<IdempotencyInsertOutcome> InsertAsync(
            IdempotencyKeyRecord record, CancellationToken cancellationToken)
        {
            InsertCount++;
            return Task.FromResult(IdempotencyInsertOutcome.Created);
        }

        public Task CompleteAsync(
            string scopeHash,
            string key,
            IdempotencyResponseMetadata response,
            DateTime completedAtUtc,
            CancellationToken cancellationToken)
        {
            CompleteCount++;
            return Task.CompletedTask;
        }

        public Task TombstoneAsync(string scopeHash, string key, CancellationToken cancellationToken)
        {
            TombstoneCount++;
            return Task.CompletedTask;
        }

        public Task<IdempotencyStatusResult?> GetStatusAsync(
            string userId, string key, CancellationToken cancellationToken) =>
            Task.FromResult<IdempotencyStatusResult?>(null);
    }

    private sealed class FakeCleanup : IIdempotencyCleanup
    {
        public Task RunIfDueAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void ResetForTests()
        {
        }
    }

    private sealed class FakeHooks : IIdempotencyTestHooks
    {
        public IdempotencyTestHook? Get(string key) => null;

        public void Set(string key, IdempotencyTestHook hook)
        {
        }

        public void Clear(string key)
        {
        }
    }
}
