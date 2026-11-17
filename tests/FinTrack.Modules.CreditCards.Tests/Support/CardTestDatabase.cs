using System.Security.Cryptography;
using FinTrack.Modules.CashInflows.Infrastructure;
using FinTrack.Modules.CreditCards.Infrastructure;
using FinTrack.SharedKernel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinTrack.Modules.CreditCards.Tests.Support;

/// <summary>
/// SQLite tabanlı test veritabanı. SQL Server <c>rowversion</c> üretmediği için
/// sürüm alanını interceptor atar; asıl eşzamanlılık davranışı API/MSSQL
/// testlerinde doğrulanır.
/// </summary>
public sealed class CardTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CreditCardsDbContext> _options;

    private CardTestDatabase(SqliteConnection connection, DbContextOptions<CreditCardsDbContext> options)
    {
        _connection = connection;
        _options = options;
    }

    public static async Task<CardTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CreditCardsDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new SqliteRowVersionInterceptor())
            .Options;

        var database = new CardTestDatabase(connection, options);

        await using var db = database.CreateContext(Guid.NewGuid());
        await db.Database.EnsureCreatedAsync();

        return database;
    }

    /// <summary>ADR-0009 testlerinde iki DbContext'in paylaştığı fiziksel bağlantı.</summary>
    internal SqliteConnection Connection => _connection;

    public CreditCardsDbContext CreateContext(
        Guid userId,
        string preferredTimeZone = UserTime.DefaultTimeZoneId) =>
        new(_options, new TestCurrentUser(userId, preferredTimeZone));

    public CashInflowsDbContext CreateInflowContext(
        Guid userId,
        string preferredTimeZone = UserTime.DefaultTimeZoneId)
    {
        var options = new DbContextOptionsBuilder<CashInflowsDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new SqliteRowVersionInterceptor())
            .Options;

        return new CashInflowsDbContext(options, new TestCurrentUser(userId, preferredTimeZone));
    }

    /// <summary>
    /// Aynı bağlantıdahem <c>cards</c> hem <c>inflows</c> tablolarını kurar.
    /// İkinci şema, <c>EnsureCreated</c> mevcut tabloları görüp atladığı için
    /// üretilen CREATE script'iyle oluşturulur.
    /// </summary>
    public static async Task<CardTestDatabase> CreateWithCashInflowsAsync()
    {
        var database = await CreateAsync();
        await using var inflows = database.CreateInflowContext(Guid.NewGuid());
        await inflows.Database.ExecuteSqlRawAsync(inflows.Database.GenerateCreateScript());
        return database;
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}

/// <summary>SQLite'ta üretilmeyen 8 baytlık sürüm değerini uygulama tarafında atar.</summary>
public sealed class SqliteRowVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AssignRowVersions(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AssignRowVersions(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void AssignRowVersions(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var property = entry.Properties
                .FirstOrDefault(candidate => candidate.Metadata.Name == "RowVersion");

            if (property is null)
            {
                continue;
            }

            property.CurrentValue = RandomNumberGenerator.GetBytes(8);

            if (entry.State == EntityState.Modified)
            {
                property.IsModified = true;
            }
        }
    }
}

public sealed class TestCurrentUser(Guid? userId, string preferredTimeZone = UserTime.DefaultTimeZoneId)
    : ICurrentUser
{
    public Guid? UserId => userId;

    public bool IsAuthenticated => userId is not null;

    public string PreferredTimeZone => preferredTimeZone;

    public Guid RequireUserId() =>
        userId ?? throw new UnauthorizedAccessException("Kullanıcı kimliği yok.");
}

public sealed class TestClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; } = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
}
