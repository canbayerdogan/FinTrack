using FinTrack.Modules.CashInflows.Application.CashInflows;
using FinTrack.Modules.CashInflows.Domain;
using FinTrack.Modules.CashInflows.Infrastructure;
using FinTrack.Modules.CashInflows.Infrastructure.Persistence;
using FinTrack.Modules.CashInflows.Infrastructure.Queries;
using FinTrack.Modules.CashInflows.Infrastructure.Repositories;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CashInflows.Tests.Support;

/// <summary>
/// Handler'ları gerçek repository/sorgu/UnitOfWork ile, sabit kullanıcı ve saatle
/// kurar. Borç bağlantılı kayıtlar sahibi modülleri taklit eden domain fabrikasıyla
/// doğrudan veritabanına yazılır.
/// </summary>
public sealed class CashInflowHandlerFactory
{
    private readonly ICurrentUser _currentUser;
    private readonly TestClock _clock;

    public CashInflowHandlerFactory(
        CashInflowsDbContext db,
        Guid userId,
        DateTime nowUtc,
        string preferredTimeZone = UserTime.DefaultTimeZoneId)
    {
        Db = db;
        _currentUser = new TestCurrentUser(userId, preferredTimeZone);
        _clock = new TestClock(nowUtc);
        Inflows = new CashInflowRepository(db);
        Queries = new CashInflowQueries(db);
        UnitOfWork = new UnitOfWork(db);
    }

    internal CashInflowsDbContext Db { get; }

    internal CashInflowRepository Inflows { get; }

    internal CashInflowQueries Queries { get; }

    internal UnitOfWork UnitOfWork { get; }

    public CreateCashInflowHandler CreateCashInflow() =>
        new(_currentUser, Inflows, UnitOfWork, _clock);

    public UpdateCashInflowHandler UpdateCashInflow() =>
        new(_currentUser, Inflows, UnitOfWork, _clock);

    public CancelCashInflowHandler CancelCashInflow() =>
        new(_currentUser, Inflows, UnitOfWork, _clock);

    public ListCashInflowsHandler ListCashInflows() =>
        new(_currentUser, Queries);

    public GetCashInflowHandler GetCashInflow() =>
        new(_currentUser, Queries);

    /// <summary>Sahibi olan borç/kart modülünün yaptığı atomik yazımı taklit eder.</summary>
    public async Task<CashInflow> SeedDebtLinkedAsync(
        decimal amount,
        DateOnly occurredOn,
        CashInflowSource source,
        Guid debtLinkId,
        string? description = null)
    {
        var result = CashInflow.CreateFromDebtOwner(
            _currentUser.RequireUserId(),
            amount,
            Money.DefaultCurrency,
            occurredOn,
            source,
            debtLinkId,
            description,
            UserTime.Today(_clock, _currentUser.PreferredTimeZone),
            _clock.UtcNow);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test verisi oluşturulamadı: {result.Error.Code}");
        }

        await Db.CashInflows.AddAsync(result.Value);
        await Db.SaveChangesAsync();
        return result.Value;
    }
}
