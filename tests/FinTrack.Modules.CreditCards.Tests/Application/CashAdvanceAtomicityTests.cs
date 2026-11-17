using FinTrack.Modules.CashInflows.Application.CashInflows;
using FinTrack.Modules.CashInflows.Domain;
using FinTrack.Modules.CashInflows.Infrastructure;
using FinTrack.Modules.CashInflows.Infrastructure.Persistence;
using FinTrack.Modules.CashInflows.Infrastructure.Repositories;
using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Cards;
using FinTrack.Modules.CreditCards.Application.CashAdvances;
using FinTrack.Modules.CreditCards.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinTrack.Modules.CreditCards.Tests.Application;

/// <summary>
/// ADR-0009'un gerçek davranışı: iki DbContext aynı SQLite bağlantısında açık
/// transaction'a katılır; hata durumunda iki modülün yazısı birlikte geri alınır.
/// </summary>
public sealed class CashAdvanceAtomicityTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 17);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Olusturma_iki_modulu_de_ayni_transaction_da_yazar()
    {
        await using var database = await CardTestDatabase.CreateWithCashInflowsAsync();
        await using var cardsDb = database.CreateContext(UserId);
        await using var inflowsDb = database.CreateInflowContext(UserId);
        var writer = CreateWriter(inflowsDb);
        var factory = new CardHandlerFactory(cardsDb, UserId, Now, inflowWriter: writer);
        var card = await CreateCardAsync(factory);

        var result = await factory.CreateCashAdvance().HandleAsync(
            new CreateCashAdvanceCommand(card.Id, 3_000m, "TRY", Today.AddDays(-2), "Acil nakit"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.LinkedCashInflowId);

        await using var verifyCards = database.CreateContext(UserId);
        await using var verifyInflows = database.CreateInflowContext(UserId);
        var advance = await verifyCards.CashAdvances.SingleOrDefaultAsync(x => x.Id == result.Value.Id);
        var inflow = await verifyInflows.CashInflows.SingleOrDefaultAsync(
            x => x.Id == result.Value.LinkedCashInflowId!.Value);

        Assert.NotNull(advance);
        Assert.NotNull(inflow);
        Assert.Equal(advance!.Id, inflow!.DebtLinkId);
        Assert.Equal(CashInflowSource.CashAdvance, inflow.Source);
        Assert.False(inflow.IsEarnedIncome);
        Assert.Equal(3_000m, inflow.Amount);
    }

    [Fact]
    public async Task Kart_yazma_hatasinda_para_girisi_de_geri_alinir()
    {
        await using var database = await CardTestDatabase.CreateWithCashInflowsAsync();
        await using var cardsDb = database.CreateContext(UserId);
        await using var inflowsDb = database.CreateInflowContext(UserId);
        var writer = CreateWriter(inflowsDb);
        var factory = new CardHandlerFactory(cardsDb, UserId, Now, inflowWriter: writer);
        var card = await CreateCardAsync(factory);

        var failing = new CountingUnitOfWork(factory.UnitOfWork)
        {
            ForcedOutcome = SaveChangesOutcome.ConcurrencyConflict,
        };

        var result = await factory.CreateCashAdvance(failing).HandleAsync(
            new CreateCashAdvanceCommand(card.Id, 5_000m, null, Today.AddDays(-1), null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ConcurrencyConflict", result.Error.Code);
        Assert.Equal(1, failing.SaveCount);
        Assert.Equal(0, factory.WriteTransactions.LastScope!.CommitCount);

        await using var verifyCards = database.CreateContext(UserId);
        await using var verifyInflows = database.CreateInflowContext(UserId);
        Assert.Empty(await verifyCards.CashAdvances.ToListAsync());
        Assert.Empty(await verifyInflows.CashInflows.ToListAsync());
    }

    [Fact]
    public async Task Iptalde_kart_yazma_hatasinda_para_girisi_aktif_kalir()
    {
        await using var database = await CardTestDatabase.CreateWithCashInflowsAsync();
        await using var cardsDb = database.CreateContext(UserId);
        await using var inflowsDb = database.CreateInflowContext(UserId);
        var writer = CreateWriter(inflowsDb);
        var factory = new CardHandlerFactory(cardsDb, UserId, Now, inflowWriter: writer);
        var card = await CreateCardAsync(factory);

        var created = await factory.CreateCashAdvance().HandleAsync(
            new CreateCashAdvanceCommand(card.Id, 2_000m, null, Today.AddDays(-1), null),
            CancellationToken.None);
        Assert.True(created.IsSuccess);

        var failing = new CountingUnitOfWork(factory.UnitOfWork)
        {
            ForcedOutcome = SaveChangesOutcome.ConcurrencyConflict,
        };

        var cancelled = await factory.CancelCashAdvance(failing).HandleAsync(
            new CancelCashAdvanceCommand(card.Id, created.Value.Id, "Yanlış işlem", created.Value.RowVersion),
            CancellationToken.None);

        Assert.True(cancelled.IsFailure);
        Assert.Equal("ConcurrencyConflict", cancelled.Error.Code);
        Assert.Equal(0, factory.WriteTransactions.LastScope!.CommitCount);

        await using var verifyCards = database.CreateContext(UserId);
        await using var verifyInflows = database.CreateInflowContext(UserId);
        var advance = await verifyCards.CashAdvances.SingleAsync(x => x.Id == created.Value.Id);
        var inflow = await verifyInflows.CashInflows.SingleAsync(
            x => x.Id == created.Value.LinkedCashInflowId!.Value);

        Assert.Null(advance.CancelledAtUtc);
        Assert.Null(inflow.CancelledAtUtc);
    }

    private static DebtCashInflowWriter CreateWriter(CashInflowsDbContext db) =>
        new(new CashInflowRepository(db), new UnitOfWork(db));

    private static async Task<CardDto> CreateCardAsync(CardHandlerFactory factory) =>
        (await factory.CreateCard().HandleAsync(
            new CreateCardCommand("Banka", "Kart", null, null, null, null),
            CancellationToken.None)).Value;
}
