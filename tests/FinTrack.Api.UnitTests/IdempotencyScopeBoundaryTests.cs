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
/// Audit F-11: kapsam dışı yolların (PUT, auth POST, kimlik hatası) mevcut
/// davranışı. Testler middleware pipeline'ını <see cref="IdempotencyMiddleware.InvokeAsync"/>
/// üzerinden çalıştırır; yansıma veya kaynak metni araması kullanmaz.
/// Kapsam dışı isteklerde geçersiz anahtar bilerek gönderilir: middleware kapsam
/// içinde olsaydı 400 üretir ve <c>next</c>'e geçmezdi.
/// </summary>
public sealed class IdempotencyScopeBoundaryTests
{
    [Theory]
    [InlineData("PUT", "/api/v1/expenses/abc")]
    [InlineData("PUT", "/api/v1/categories/abc/archive")]
    [InlineData("POST", "/api/v1/auth/login")]
    [InlineData("POST", "/api/v1/auth/register")]
    public async Task Kapsam_disindaki_yollar_idempotency_kontrolunu_atlayip_next_e_gecer(string method, string path)
    {
        await using var harness = await BoundaryHarness.CreateAsync(method, path);
        harness.Context.Request.Headers[IdempotencyScope.HeaderName] = "gecersiz-anahtar";

        await harness.InvokeAsync();

        Assert.Equal(1, harness.Next.Value);
        Assert.Equal(StatusCodes.Status200OK, harness.Context.Response.StatusCode);
        Assert.Null(harness.Context.Response.ContentLength);
        Assert.Equal("next", harness.ResponseText);
        AssertNoHostScope(harness);
    }

    [Fact]
    public async Task Kimlik_hatasinda_host_scope_acilmadan_next_cagirilir()
    {
        await using var harness = await BoundaryHarness.CreateAsync(
            "POST", "/api/v1/expenses", new ThrowingCurrentUser());
        harness.Context.Request.Headers[IdempotencyScope.HeaderName] = Guid.NewGuid().ToString("D");

        await harness.InvokeAsync();

        // docs/11 §1.1: RequireUserId UnauthorizedAccessException fırlatırsa
        // middleware doğrulamaya/kayda/transaction'a girmeden zinciri sürdürür.
        Assert.Equal(1, harness.Next.Value);
        Assert.Equal(StatusCodes.Status200OK, harness.Context.Response.StatusCode);
        Assert.Equal("next", harness.ResponseText);
        AssertNoHostScope(harness);
    }

    [Fact]
    public async Task Kapsam_dahilindeki_POST_tek_scope_acip_tek_commit_yapar()
    {
        await using var harness = await BoundaryHarness.CreateAsync("POST", "/api/v1/expenses");
        harness.Context.Request.Headers[IdempotencyScope.HeaderName] = Guid.NewGuid().ToString("D");

        await harness.InvokeAsync();

        // Pozitif kontrol: yukarıdaki sayaçlar gerçekten scope açılışını gözlemliyor.
        Assert.Equal(1, harness.Next.Value);
        Assert.Equal(1, harness.Factory.BeginCount);
        Assert.Same(harness.Scope, harness.Setter.LastSetScope);
        Assert.Null(harness.Setter.Current); // finally bloğu scope'u temizler
        Assert.Equal(1, harness.Participant.EnlistCount);
        Assert.Equal(1, harness.Participant.DetachCount);
        Assert.Equal(1, harness.Store.InsertCount);
        Assert.Equal(1, harness.Store.CompleteCount);
        Assert.Equal(1, harness.Scope.CommitCount);
        Assert.Equal(1, harness.Scope.DisposeCount);
        Assert.Equal(1, harness.Cleanup.RunCount);
        Assert.Equal("next", harness.ResponseText);
    }

    private static void AssertNoHostScope(BoundaryHarness harness)
    {
        Assert.Equal(0, harness.Factory.BeginCount);
        Assert.Equal(0, harness.Setter.SetCount);
        Assert.Null(harness.Setter.Current);
        Assert.Equal(0, harness.Participant.EnlistCount);
        Assert.Equal(0, harness.Participant.DetachCount);
        Assert.Equal(0, harness.Store.FindCount);
        Assert.Equal(0, harness.Store.InsertCount);
        Assert.Equal(0, harness.Store.CompleteCount);
        Assert.Equal(0, harness.Store.TombstoneCount);
        Assert.Equal(0, harness.Scope.CommitCount);
        Assert.Equal(0, harness.Cleanup.RunCount);
    }

    private sealed class BoundaryHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbTransaction _transaction;
        private readonly IdempotencyDbContext _idempotencyDb;
        private readonly IdempotencyMiddleware _middleware;

        private BoundaryHarness(
            SqliteConnection connection,
            DbTransaction transaction,
            IdempotencyDbContext idempotencyDb,
            IdempotencyMiddleware middleware,
            DefaultHttpContext context,
            TrackingScopeFactory factory,
            TrackingScope scope,
            TrackingSetter setter,
            TrackingStore store,
            TrackingParticipant participant,
            TrackingCleanup cleanup,
            Counter next)
        {
            _connection = connection;
            _transaction = transaction;
            _idempotencyDb = idempotencyDb;
            _middleware = middleware;
            Context = context;
            Factory = factory;
            Scope = scope;
            Setter = setter;
            Store = store;
            Participant = participant;
            Cleanup = cleanup;
            Next = next;
        }

        public DefaultHttpContext Context { get; }

        public TrackingScopeFactory Factory { get; }

        public TrackingScope Scope { get; }

        public TrackingSetter Setter { get; }

        public TrackingStore Store { get; }

        public TrackingParticipant Participant { get; }

        public TrackingCleanup Cleanup { get; }

        public Counter Next { get; }

        public string ResponseText => Encoding.UTF8.GetString(((MemoryStream)Context.Response.Body).ToArray());

        public static async Task<BoundaryHarness> CreateAsync(
            string method, string path, ICurrentUser? currentUser = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var transaction = await connection.BeginTransactionAsync();
            var idempotencyDb = new IdempotencyDbContext(
                new DbContextOptionsBuilder<IdempotencyDbContext>().UseSqlite(connection).Options);

            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = path;
            context.Request.ContentType = "application/json";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
            context.Response.Body = new MemoryStream();
            context.RequestServices = new ServiceCollection()
                .AddProblemDetails()
                .AddLogging()
                .BuildServiceProvider();

            var scope = new TrackingScope(transaction);
            var factory = new TrackingScopeFactory(scope);
            var setter = new TrackingSetter();
            var store = new TrackingStore();
            var participant = new TrackingParticipant();
            var cleanup = new TrackingCleanup();
            var next = new Counter();

            var middleware = new IdempotencyMiddleware(
                async nextContext =>
                {
                    next.Value++;
                    nextContext.Response.StatusCode = StatusCodes.Status200OK;
                    await nextContext.Response.WriteAsync("next");
                },
                NullLogger<IdempotencyMiddleware>.Instance);

            return new BoundaryHarness(
                connection,
                transaction,
                idempotencyDb,
                middleware,
                context,
                factory,
                scope,
                setter,
                store,
                participant,
                cleanup,
                next)
            {
                CurrentUser = currentUser ?? new FakeCurrentUser(Guid.NewGuid()),
            };
        }

        public ICurrentUser CurrentUser { get; init; } = null!;

        public Task InvokeAsync() => _middleware.InvokeAsync(
            Context,
            CurrentUser,
            Store,
            Factory,
            Setter,
            _idempotencyDb,
            [Participant],
            Cleanup,
            new FakeHooks());

        public async ValueTask DisposeAsync()
        {
            await _idempotencyDb.DisposeAsync();
            await _transaction.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class Counter
    {
        public int Value { get; set; }
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;

        public bool IsAuthenticated => true;

        public string PreferredTimeZone => "UTC";

        public Guid RequireUserId() => userId;
    }

    private sealed class ThrowingCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;

        public bool IsAuthenticated => false;

        public string PreferredTimeZone => "UTC";

        public Guid RequireUserId() => throw new UnauthorizedAccessException("Kullanıcı kimliği yok.");
    }

    private sealed class TrackingScopeFactory(IWriteTransactionScope scope) : IWriteTransactionScopeFactory
    {
        public int BeginCount { get; private set; }

        public Task<IWriteTransactionScope> BeginAsync(CancellationToken cancellationToken)
        {
            BeginCount++;
            return Task.FromResult(scope);
        }
    }

    private sealed class TrackingScope(DbTransaction transaction) : IWriteTransactionScope
    {
        public DbTransaction Transaction => transaction;

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

    private sealed class TrackingSetter : IWriteTransactionScopeSetter
    {
        public IWriteTransactionScope? Current { get; private set; }

        public int SetCount { get; private set; }

        public IWriteTransactionScope? LastSetScope { get; private set; }

        public void Set(IWriteTransactionScope? scope)
        {
            Current = scope;
            SetCount++;
            if (scope is not null)
            {
                LastSetScope = scope;
            }
        }
    }

    private sealed class TrackingStore : IIdempotencyStore
    {
        public int FindCount { get; private set; }

        public int InsertCount { get; private set; }

        public int CompleteCount { get; private set; }

        public int TombstoneCount { get; private set; }

        public Task<IdempotencyKeyRecord?> FindAsync(
            string scopeHash, string key, CancellationToken cancellationToken)
        {
            FindCount++;
            return Task.FromResult<IdempotencyKeyRecord?>(null);
        }

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

    private sealed class TrackingParticipant : IWriteScopeParticipant
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
        }
    }

    private sealed class TrackingCleanup : IIdempotencyCleanup
    {
        public int RunCount { get; private set; }

        public Task RunIfDueAsync(CancellationToken cancellationToken)
        {
            RunCount++;
            return Task.CompletedTask;
        }

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
