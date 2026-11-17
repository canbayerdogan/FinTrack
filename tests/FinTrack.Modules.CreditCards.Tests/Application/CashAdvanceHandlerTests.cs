using FinTrack.ModuleContracts;
using FinTrack.Modules.CreditCards.Application.Cards;
using FinTrack.Modules.CreditCards.Application.CashAdvances;
using FinTrack.Modules.CreditCards.Application.Payments;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.Modules.CreditCards.Tests.Support;
using FinTrack.SharedKernel;
using Xunit;

namespace FinTrack.Modules.CreditCards.Tests.Application;

/// <summary>
/// Slice 6 akışları (docs/07 §4): avans kart borcuna bir kez girer, bağlı para
/// girişi aynı transaction'da yazılır, iptal ikisini birlikte kapatır.
/// </summary>
public sealed class CashAdvanceHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 17);
    private static readonly DateOnly TakenOn = Today.AddDays(-2);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Avans_borca_bir_kez_girer_ve_writer_a_bagli_kaydi_verir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);
        factory.ExpenseQuery.AsOfTotals = new(5_000m, 1_000m);

        var result = await factory.CreateCashAdvance().HandleAsync(
            new CreateCashAdvanceCommand(card.Id, 3_000m, "TRY", TakenOn, "Acil nakit"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var advance = result.Value;
        Assert.Equal(CashAdvanceStatus.Active, advance.Status);
        Assert.Equal(factory.InflowWriter.CreatedInflowId, advance.LinkedCashInflowId);
        Assert.Equal(8, Convert.FromBase64String(advance.RowVersion).Length);

        var request = Assert.IsType<DebtInflowCreate>(factory.InflowWriter.LastCreate);
        Assert.Equal(UserId, request.UserId);
        Assert.Equal(3_000m, request.Amount);
        Assert.Equal("TRY", request.Currency);
        Assert.Equal(TakenOn, request.OccurredOn);
        Assert.Equal(DebtInflowKind.CashAdvance, request.Kind);
        Assert.Equal(advance.Id, request.DebtLinkId);
        Assert.Equal("Acil nakit", request.Description);
        Assert.Same(factory.WriteTransactions.LastScope, factory.InflowWriter.LastCreateScope);
        Assert.Equal(1, factory.WriteTransactions.LastScope!.CommitCount);
        Assert.True(factory.WriteTransactions.LastScope.Disposed);

        var balance = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(card.Id),
            CancellationToken.None);
        Assert.True(balance.IsSuccess);
        Assert.Equal(7_000m, balance.Value.Debt);
        Assert.Equal(3_000m, balance.Value.Breakdown.CashAdvances);
        Assert.Equal(0m, balance.Value.Breakdown.Payments);
    }

    [Fact]
    public async Task Kart_odemesi_avans_borcunu_azaltir_ikinci_borcl_kalemi_olusturmaz()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);
        factory.ExpenseQuery.AsOfTotals = new(0m, 0m);

        var advance = (await factory.CreateCashAdvance().HandleAsync(
            new CreateCashAdvanceCommand(card.Id, 3_000m, null, TakenOn, null),
            CancellationToken.None)).Value;
        await factory.CreatePayment().HandleAsync(
            new CreateCardPaymentCommand(card.Id, 1_000m, null, TakenOn, null),
            CancellationToken.None);

        var balance = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(card.Id),
            CancellationToken.None);

        Assert.True(balance.IsSuccess);
        Assert.Equal(2_000m, balance.Value.Debt);
        Assert.Equal(3_000m, balance.Value.Breakdown.CashAdvances);
        Assert.Equal(1_000m, balance.Value.Breakdown.Payments);
        Assert.NotEqual(Guid.Empty, advance.LinkedCashInflowId!.Value);
    }

    [Fact]
    public async Task Iptal_bagli_para_girisini_de_kapatir_ve_bakiyeyi_gunceller()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);
        factory.ExpenseQuery.AsOfTotals = new(5_000m, 0m);

        var advance = (await factory.CreateCashAdvance().HandleAsync(
            new CreateCashAdvanceCommand(card.Id, 3_000m, null, TakenOn, null),
            CancellationToken.None)).Value;

        var missingReason = await factory.CancelCashAdvance().HandleAsync(
            new CancelCashAdvanceCommand(card.Id, advance.Id, "  ", advance.RowVersion),
            CancellationToken.None);
        Assert.True(missingReason.IsFailure);
        Assert.Equal("CancellationReasonRequired", missingReason.Error.Code);
        Assert.Null(factory.InflowWriter.LastCancel);

        var cancelled = await factory.CancelCashAdvance().HandleAsync(
            new CancelCashAdvanceCommand(card.Id, advance.Id, "Yanlış işlem", advance.RowVersion),
            CancellationToken.None);
        Assert.True(cancelled.IsSuccess);

        var request = Assert.IsType<DebtInflowCancel>(factory.InflowWriter.LastCancel);
        Assert.Equal(factory.InflowWriter.CreatedInflowId, request.CashInflowId);
        Assert.Equal(DebtInflowKind.CashAdvance, request.Kind);
        Assert.Equal(advance.Id, request.DebtLinkId);
        Assert.Equal("Yanlış işlem", request.Reason);
        Assert.Same(factory.WriteTransactions.LastScope, factory.InflowWriter.LastCancelScope);

        var balance = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(card.Id),
            CancellationToken.None);
        Assert.Equal(5_000m, balance.Value.Debt);
        Assert.Equal(0m, balance.Value.Breakdown.CashAdvances);
    }

    [Fact]
    public async Task Iptal_idempotenttir_eski_surum_ve_yabanci_kart_reddedilir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);

        var advance = (await factory.CreateCashAdvance().HandleAsync(
            new CreateCashAdvanceCommand(card.Id, 500m, null, TakenOn, null),
            CancellationToken.None)).Value;

        var stale = await factory.CancelCashAdvance().HandleAsync(
            new CancelCashAdvanceCommand(card.Id, advance.Id, "İptal", "AAAAAAAAB9E="),
            CancellationToken.None);
        Assert.True(stale.IsFailure);
        Assert.Equal("ConcurrencyConflict", stale.Error.Code);

        Assert.True((await factory.CancelCashAdvance().HandleAsync(
            new CancelCashAdvanceCommand(card.Id, advance.Id, "İptal", advance.RowVersion),
            CancellationToken.None)).IsSuccess);

        var stored = await factory.Advances.FindAsync(UserId, card.Id, advance.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.True((await factory.CancelCashAdvance().HandleAsync(
            new CancelCashAdvanceCommand(card.Id, advance.Id, "Tekrar", RowVersionCodec.ToBase64(stored!.RowVersion)),
            CancellationToken.None)).IsSuccess);

        var foreign = await factory.CancelCashAdvance().HandleAsync(
            new CancelCashAdvanceCommand(Guid.NewGuid(), advance.Id, "İptal", advance.RowVersion),
            CancellationToken.None);
        Assert.True(foreign.IsFailure);
        Assert.Equal("NotFound", foreign.Error.Code);
    }

    [Fact]
    public async Task Liste_varsayilan_aktif_ve_sayfalama_dogru()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);

        var first = (await factory.CreateCashAdvance().HandleAsync(
            new CreateCashAdvanceCommand(card.Id, 100m, null, TakenOn.AddDays(-1), null),
            CancellationToken.None)).Value;
        var second = (await factory.CreateCashAdvance().HandleAsync(
            new CreateCashAdvanceCommand(card.Id, 200m, null, TakenOn, null),
            CancellationToken.None)).Value;
        await factory.CancelCashAdvance().HandleAsync(
            new CancelCashAdvanceCommand(card.Id, first.Id, "İptal", first.RowVersion),
            CancellationToken.None);

        var active = await factory.ListCashAdvances().HandleAsync(
            new ListCashAdvancesQuery(card.Id, new CashAdvanceFilter(null, false, 1, 50)),
            CancellationToken.None);
        Assert.True(active.IsSuccess);
        Assert.Equal(second.Id, Assert.Single(active.Value.Items).Id);

        var cancelled = await factory.ListCashAdvances().HandleAsync(
            new ListCashAdvancesQuery(card.Id, new CashAdvanceFilter(CashAdvanceStatus.Cancelled, false, 1, 50)),
            CancellationToken.None);
        Assert.Equal(first.Id, Assert.Single(cancelled.Value.Items).Id);

        var all = await factory.ListCashAdvances().HandleAsync(
            new ListCashAdvancesQuery(card.Id, new CashAdvanceFilter(null, true, 1, 1)),
            CancellationToken.None);
        Assert.Equal(2, all.Value.TotalCount);
        Assert.Equal(2, all.Value.TotalPages);
        Assert.Single(all.Value.Items);
    }

    [Fact]
    public async Task Gecmis_tarihli_iptal_kesitte_aktif_sayilir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);
        factory.ExpenseQuery.AsOfTotals = new(0m, 0m);

        var advance = (await factory.CreateCashAdvance().HandleAsync(
            new CreateCashAdvanceCommand(card.Id, 1_000m, null, TakenOn, null),
            CancellationToken.None)).Value;
        await factory.CancelCashAdvance().HandleAsync(
            new CancelCashAdvanceCommand(card.Id, advance.Id, "İptal", advance.RowVersion),
            CancellationToken.None);

        // İptal UTC olarak bugün; dünün kesitinde avans hâlâ aktiftir.
        var asOfYesterday = await factory.AdvanceQueries.SumActiveAdvancesAsync(
            UserId, card.Id, null, Today.AddDays(-1), UserTime.DefaultTimeZoneId, CancellationToken.None);
        var asOfToday = await factory.AdvanceQueries.SumActiveAdvancesAsync(
            UserId, card.Id, null, Today, UserTime.DefaultTimeZoneId, CancellationToken.None);

        Assert.Equal(1_000m, asOfYesterday);
        Assert.Equal(0m, asOfToday);
    }

    [Fact]
    public async Task Yabanci_arsivli_ve_gecersiz_avans_reddedilir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);

        var unknown = await factory.CreateCashAdvance().HandleAsync(
            new CreateCashAdvanceCommand(Guid.NewGuid(), 100m, null, TakenOn, null),
            CancellationToken.None);
        Assert.True(unknown.IsFailure);
        Assert.Equal("NotFound", unknown.Error.Code);

        var future = await factory.CreateCashAdvance().HandleAsync(
            new CreateCashAdvanceCommand(card.Id, 100m, null, Today.AddDays(1), null),
            CancellationToken.None);
        Assert.True(future.IsFailure);
        Assert.Equal("CashAdvanceDateInFuture", future.Error.Code);

        await factory.ArchiveCard().HandleAsync(
            new ArchiveCardCommand(card.Id, card.RowVersion),
            CancellationToken.None);
        var archived = await factory.CreateCashAdvance().HandleAsync(
            new CreateCashAdvanceCommand(card.Id, 100m, null, TakenOn, null),
            CancellationToken.None);
        Assert.True(archived.IsFailure);
        Assert.Equal("CardArchived", archived.Error.Code);
    }

    [Fact]
    public async Task Writer_hatasinda_avans_kaydedilmez_ve_commit_yapilmaz()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);
        factory.InflowWriter.NextCreateFailure =
            Result.Failure(Error.Conflict("StateConflict", "Yazma hatası."));

        var result = await factory.CreateCashAdvance().HandleAsync(
            new CreateCashAdvanceCommand(card.Id, 500m, null, TakenOn, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StateConflict", result.Error.Code);
        Assert.Null(await factory.Advances.FindAsync(UserId, card.Id, result.IsSuccess ? result.Value.Id : Guid.Empty, CancellationToken.None));
        Assert.Equal(0, factory.WriteTransactions.LastScope!.CommitCount);
        Assert.True(factory.WriteTransactions.LastScope.Disposed);
    }

    private static async Task<CardDto> CreateCardAsync(CardHandlerFactory factory) =>
        (await factory.CreateCard().HandleAsync(
            new CreateCardCommand("Banka", "Kart", null, null, null, null),
            CancellationToken.None)).Value;
}
