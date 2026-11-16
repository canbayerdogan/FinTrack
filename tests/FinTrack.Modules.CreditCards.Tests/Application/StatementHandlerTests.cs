using FinTrack.Modules.CreditCards.Application.Cards;
using FinTrack.Modules.CreditCards.Application.Payments;
using FinTrack.Modules.CreditCards.Application.Statements;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.Modules.CreditCards.Tests.Support;
using Xunit;

namespace FinTrack.Modules.CreditCards.Tests.Application;

/// <summary>
/// Slice 4 ekstre senaryoları (docs/08 §7/7–17). Türetilen alanlar okuma anında
/// güncel kayıtlardan hesaplanır; ekstre yeni gider/borç kaynağı değildir.
/// </summary>
public sealed class StatementHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 9, 30, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Ekstre_bakiyeyi_degistirmez_ve_turetilen_alanlari_doner()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);
        factory.ExpenseQuery.AsOfTotals = new(1000m, 100m);

        var before = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(card.Id),
            CancellationToken.None);

        var created = await CreateStatementAsync(
            factory,
            card.Id,
            new DateOnly(2026, 10, 26),
            new DateOnly(2026, 11, 13),
            new DateOnly(2026, 11, 20),
            950m);

        Assert.True(created.IsSuccess);
        var statement = created.Value;
        Assert.Equal(StatementStatus.Active, statement.Status);
        Assert.Equal(950m, statement.StatementAmount);
        Assert.Equal(0m, statement.CarriedOverAmount);
        Assert.Equal(900m, statement.ComputedDebtAtPeriodEnd);
        Assert.Equal(50m, statement.ReconciliationDifference);
        Assert.Equal(new DateOnly(2026, 11, 13), factory.ExpenseQuery.LastAsOf);

        var after = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(card.Id),
            CancellationToken.None);
        Assert.Equal(before.Value.Debt, after.Value.Debt);
        Assert.Equal(900m, after.Value.Debt);
    }

    [Fact]
    public async Task Devreden_borc_onceki_donem_sonu_haric_mevcut_donem_sonu_dahil_hesaplanir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var lateNow = new DateTime(2026, 11, 30, 9, 30, 0, DateTimeKind.Utc);
        var factory = new CardHandlerFactory(db, UserId, lateNow);
        var card = await CreateCardAsync(factory);
        factory.ExpenseQuery.AsOfTotals = new(0m, 0m);

        var first = await CreateStatementAsync(
            factory,
            card.Id,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31),
            new DateOnly(2026, 11, 5),
            1000m);
        Assert.True(first.IsSuccess);

        await CreatePaymentAsync(factory, card.Id, 200m, new DateOnly(2026, 10, 31));
        await CreatePaymentAsync(factory, card.Id, 300m, new DateOnly(2026, 11, 1));
        await CreatePaymentAsync(factory, card.Id, 50m, new DateOnly(2026, 11, 25));
        await CreatePaymentAsync(factory, card.Id, 100m, new DateOnly(2026, 11, 26));

        var second = await CreateStatementAsync(
            factory,
            card.Id,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 25),
            new DateOnly(2026, 12, 5),
            1200m);

        Assert.True(second.IsSuccess);
        // 10-31 (önceki dönem sonu) hariç; 11-01 ve 11-25 (mevcut dönem sonu) dahil.
        Assert.Equal(650m, second.Value.CarriedOverAmount);

        var cancel = await factory.CancelStatement().HandleAsync(
            new CancelStatementCommand(card.Id, first.Value.Id, "Yanlış dönem", first.Value.RowVersion),
            CancellationToken.None);
        Assert.True(cancel.IsSuccess);

        // Önceki aktif ekstre kalmadığından devreden borç okuma anında 0'a döner.
        var reread = await factory.GetStatement().HandleAsync(
            new GetStatementQuery(card.Id, second.Value.Id),
            CancellationToken.None);
        Assert.True(reread.IsSuccess);
        Assert.Equal(0m, reread.Value.CarriedOverAmount);
    }

    [Fact]
    public async Task Devreden_borc_iptal_zamanini_kesite_gore_degerlendirir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var lateNow = new DateTime(2026, 11, 30, 9, 30, 0, DateTimeKind.Utc);
        var factory = new CardHandlerFactory(db, UserId, lateNow);
        var card = await CreateCardAsync(factory);
        factory.ExpenseQuery.AsOfTotals = new(0m, 0m);

        var first = await CreateStatementAsync(
            factory,
            card.Id,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31),
            new DateOnly(2026, 11, 5),
            1000m);
        Assert.True(first.IsSuccess);

        // Kesitten (11-15) önce iptal edilen ödeme: devredenden düşülmez.
        var earlyPayment = await CreatePaymentAsync(
            factory,
            card.Id,
            300m,
            new DateOnly(2026, 11, 1));
        var earlyCancelFactory = new CardHandlerFactory(
            db,
            UserId,
            new DateTime(2026, 11, 5, 9, 30, 0, DateTimeKind.Utc));
        var earlyCancel = await earlyCancelFactory.CancelPayment().HandleAsync(
            new CancelCardPaymentCommand(card.Id, earlyPayment.Id, "İptal", earlyPayment.RowVersion),
            CancellationToken.None);
        Assert.True(earlyCancel.IsSuccess);

        // Kesitten (11-15) sonra iptal edilen ödeme: kesitte aktif, devredenden düşülür.
        var latePayment = await CreatePaymentAsync(
            factory,
            card.Id,
            400m,
            new DateOnly(2026, 11, 10));
        var lateCancelFactory = new CardHandlerFactory(
            db,
            UserId,
            new DateTime(2026, 11, 20, 9, 30, 0, DateTimeKind.Utc));
        var lateCancel = await lateCancelFactory.CancelPayment().HandleAsync(
            new CancelCardPaymentCommand(card.Id, latePayment.Id, "İptal", latePayment.RowVersion),
            CancellationToken.None);
        Assert.True(lateCancel.IsSuccess);

        var second = await CreateStatementAsync(
            factory,
            card.Id,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 15),
            new DateOnly(2026, 11, 20),
            500m);

        Assert.True(second.IsSuccess);
        Assert.Equal(600m, second.Value.CarriedOverAmount);
        Assert.Equal(-400m, second.Value.ComputedDebtAtPeriodEnd);
    }

    [Fact]
    public async Task Gecmis_tarihli_odeme_ve_iptal_ekstreyi_okuma_aninda_gunceller()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);
        factory.ExpenseQuery.AsOfTotals = new(1000m, 0m);

        var statement = await CreateStatementAsync(
            factory,
            card.Id,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 13),
            new DateOnly(2026, 11, 20),
            800m);
        Assert.True(statement.IsSuccess);
        Assert.Equal(1000m, statement.Value.ComputedDebtAtPeriodEnd);
        Assert.Equal(-200m, statement.Value.ReconciliationDifference);

        var payment = await CreatePaymentAsync(
            factory,
            card.Id,
            250m,
            new DateOnly(2026, 11, 10));

        var withPayment = await factory.GetStatement().HandleAsync(
            new GetStatementQuery(card.Id, statement.Value.Id),
            CancellationToken.None);
        Assert.Equal(750m, withPayment.Value.ComputedDebtAtPeriodEnd);
        Assert.Equal(50m, withPayment.Value.ReconciliationDifference);

        // İptal, kesitle (11-13) aynı kullanıcı yerel gününde yapılır; ekstre
        // okunduğunda ödeme geriye dönük olarak kesit dışına düşer.
        var cancelFactory = new CardHandlerFactory(
            db,
            UserId,
            new DateTime(2026, 11, 13, 9, 30, 0, DateTimeKind.Utc));
        var cancel = await cancelFactory.CancelPayment().HandleAsync(
            new CancelCardPaymentCommand(card.Id, payment.Id, "İptal", payment.RowVersion),
            CancellationToken.None);
        Assert.True(cancel.IsSuccess);

        var afterCancel = await factory.GetStatement().HandleAsync(
            new GetStatementQuery(card.Id, statement.Value.Id),
            CancellationToken.None);
        Assert.Equal(1000m, afterCancel.Value.ComputedDebtAtPeriodEnd);
        Assert.Equal(-200m, afterCancel.Value.ReconciliationDifference);
    }

    [Fact]
    public async Task Donem_cakismasi_yalnizca_aktif_ekstrelerde_reddedilir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);

        var first = await CreateStatementAsync(
            factory,
            card.Id,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 15),
            new DateOnly(2026, 11, 20),
            500m);
        Assert.True(first.IsSuccess);

        var overlap = await CreateStatementAsync(
            factory,
            card.Id,
            new DateOnly(2026, 11, 10),
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 30),
            600m);
        Assert.True(overlap.IsFailure);
        Assert.Equal("StatementPeriodOverlap", overlap.Error.Code);

        var adjacent = await CreateStatementAsync(
            factory,
            card.Id,
            new DateOnly(2026, 11, 16),
            new DateOnly(2026, 11, 30),
            new DateOnly(2026, 12, 5),
            700m);
        Assert.True(adjacent.IsSuccess);

        // İptal edilen ekstre dönemi serbest bırakır.
        var cancel = await factory.CancelStatement().HandleAsync(
            new CancelStatementCommand(card.Id, first.Value.Id, "Yanlış dönem", first.Value.RowVersion),
            CancellationToken.None);
        Assert.True(cancel.IsSuccess);

        var samePeriod = await CreateStatementAsync(
            factory,
            card.Id,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 15),
            new DateOnly(2026, 11, 20),
            500m);
        Assert.True(samePeriod.IsSuccess);
    }

    [Fact]
    public async Task Ekstre_validasyonlari_ve_erisim_kurallari()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);

        var invalidPeriod = await CreateStatementAsync(
            factory,
            card.Id,
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 10),
            new DateOnly(2026, 11, 30),
            100m);
        Assert.Equal("StatementPeriodInvalid", invalidPeriod.Error.Code);

        var dueBeforeEnd = await CreateStatementAsync(
            factory,
            card.Id,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 15),
            new DateOnly(2026, 11, 10),
            100m);
        Assert.Equal("StatementDueDateBeforePeriodEnd", dueBeforeEnd.Error.Code);

        var missing = await factory.CreateStatement().HandleAsync(
            new CreateStatementCommand(card.Id, null, null, null, 100m, "TRY"),
            CancellationToken.None);
        Assert.Equal("StatementPeriodStartRequired", missing.Error.Code);

        var unknown = await CreateStatementAsync(
            factory,
            Guid.NewGuid(),
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 15),
            new DateOnly(2026, 11, 20),
            100m);
        Assert.Equal("NotFound", unknown.Error.Code);

        var archivedCard = await CreateCardAsync(factory, "Arşiv");
        await factory.ArchiveCard().HandleAsync(
            new ArchiveCardCommand(archivedCard.Id, archivedCard.RowVersion),
            CancellationToken.None);
        var archived = await CreateStatementAsync(
            factory,
            archivedCard.Id,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 15),
            new DateOnly(2026, 11, 20),
            100m);
        Assert.Equal("CardArchived", archived.Error.Code);
    }

    [Fact]
    public async Task Ekstre_listelenir_ve_baska_kullanici_goremez()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        var otherUser = Guid.NewGuid();
        Guid cardId;
        Guid firstId;

        await using (var db = database.CreateContext(UserId))
        {
            var factory = new CardHandlerFactory(db, UserId, Now);
            var card = await CreateCardAsync(factory);
            cardId = card.Id;

            var first = await CreateStatementAsync(
                factory,
                card.Id,
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 10, 31),
                new DateOnly(2026, 11, 5),
                500m);
            firstId = first.Value.Id;
            await CreateStatementAsync(
                factory,
                card.Id,
                new DateOnly(2026, 11, 1),
                new DateOnly(2026, 11, 10),
                new DateOnly(2026, 11, 15),
                600m);

            var list = await factory.ListStatements().HandleAsync(
                new ListStatementsQuery(card.Id, new StatementFilter(null, false, 1, 50)),
                CancellationToken.None);

            Assert.True(list.IsSuccess);
            Assert.Equal(2, list.Value.TotalCount);
            Assert.Equal(new DateOnly(2026, 11, 10), list.Value.Items[0].PeriodEnd);
        }

        await using (var db = database.CreateContext(otherUser))
        {
            var factory = new CardHandlerFactory(db, otherUser, Now);

            var get = await factory.GetStatement().HandleAsync(
                new GetStatementQuery(cardId, firstId),
                CancellationToken.None);
            Assert.Equal("NotFound", get.Error.Code);

            var list = await factory.ListStatements().HandleAsync(
                new ListStatementsQuery(cardId, new StatementFilter(null, false, 1, 50)),
                CancellationToken.None);
            Assert.Equal("NotFound", list.Error.Code);

            var cancel = await factory.CancelStatement().HandleAsync(
                new CancelStatementCommand(cardId, firstId, "İptal", Convert.ToBase64String(new byte[8])),
                CancellationToken.None);
            Assert.Equal("NotFound", cancel.Error.Code);
        }
    }

    private static async Task<CardDto> CreateCardAsync(
        CardHandlerFactory factory,
        string displayName = "Kart") =>
        (await factory.CreateCard().HandleAsync(
            new CreateCardCommand("Banka", displayName, null, null, null, null),
            CancellationToken.None)).Value;

    private static Task<FinTrack.SharedKernel.Result<StatementDto>> CreateStatementAsync(
        CardHandlerFactory factory,
        Guid cardId,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly dueDate,
        decimal amount) =>
        factory.CreateStatement().HandleAsync(
            new CreateStatementCommand(cardId, periodStart, periodEnd, dueDate, amount, "TRY"),
            CancellationToken.None);

    private static async Task<CardPaymentDto> CreatePaymentAsync(
        CardHandlerFactory factory,
        Guid cardId,
        decimal amount,
        DateOnly paidOn)
    {
        var result = await factory.CreatePayment().HandleAsync(
            new CreateCardPaymentCommand(cardId, amount, "TRY", paidOn, null),
            CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
