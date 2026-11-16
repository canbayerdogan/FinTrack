using FinTrack.Modules.CreditCards.Application.Cards;
using FinTrack.Modules.CreditCards.Application.Payments;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.Modules.CreditCards.Tests.Support;
using Xunit;

namespace FinTrack.Modules.CreditCards.Tests.Application;

/// <summary>
/// Slice 4 ödeme senaryoları (docs/08 §7/1–6, 9–10, 13, 17, 19–20).
/// Ödeme gider değildir; yalnızca kart borcunu düşürür.
/// </summary>
public sealed class PaymentHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 9, 30, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 14);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Odeme_bakiyeyi_dusurur_gider_toplamini_degistirmez()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);
        factory.ExpenseQuery.AsOfTotals = new(1000m, 100m);

        var result = await factory.CreatePayment().HandleAsync(
            new CreateCardPaymentCommand(card.Id, 400m, "TRY", Today, "Ekstre ödemesi"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var payment = result.Value;
        Assert.Equal(PaymentStatus.Active, payment.Status);
        Assert.Equal(Today, payment.PaidOn);
        Assert.Equal(8, Convert.FromBase64String(payment.RowVersion).Length);

        var balance = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(card.Id),
            CancellationToken.None);

        Assert.True(balance.IsSuccess);
        Assert.Equal(500m, balance.Value.Debt);
        Assert.Equal(1000m, balance.Value.Breakdown.Expenses);
        Assert.Equal(100m, balance.Value.Breakdown.Refunds);
        Assert.Equal(400m, balance.Value.Breakdown.Payments);
        Assert.Equal(Today, factory.ExpenseQuery.LastAsOf);
    }

    [Fact]
    public async Task Borcu_asan_odeme_kart_alacagi_uretir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);
        factory.ExpenseQuery.AsOfTotals = new(100m, 0m);

        var payment = await factory.CreatePayment().HandleAsync(
            new CreateCardPaymentCommand(card.Id, 150m, "TRY", Today, null),
            CancellationToken.None);
        Assert.True(payment.IsSuccess);

        var balance = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(card.Id),
            CancellationToken.None);

        Assert.True(balance.IsSuccess);
        Assert.Equal(-50m, balance.Value.Debt);
        Assert.True(balance.Value.IsInCredit);
        Assert.Equal(150m, balance.Value.Breakdown.Payments);
    }

    [Fact]
    public async Task Iptal_gerekcesiz_reddedilir_gerekceli_bakiyeyi_geri_yukseltir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);
        factory.ExpenseQuery.AsOfTotals = new(500m, 0m);

        var payment = (await factory.CreatePayment().HandleAsync(
            new CreateCardPaymentCommand(card.Id, 300m, "TRY", Today, null),
            CancellationToken.None)).Value;

        var missingReason = await factory.CancelPayment().HandleAsync(
            new CancelCardPaymentCommand(card.Id, payment.Id, "  ", payment.RowVersion),
            CancellationToken.None);
        Assert.True(missingReason.IsFailure);
        Assert.Equal("CancellationReasonRequired", missingReason.Error.Code);

        var afterFailedCancel = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(card.Id),
            CancellationToken.None);
        Assert.Equal(200m, afterFailedCancel.Value.Debt);

        var cancelled = await factory.CancelPayment().HandleAsync(
            new CancelCardPaymentCommand(card.Id, payment.Id, "Yanlış giriş", payment.RowVersion),
            CancellationToken.None);
        Assert.True(cancelled.IsSuccess);

        var afterCancel = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(card.Id),
            CancellationToken.None);
        Assert.Equal(500m, afterCancel.Value.Debt);
        Assert.Equal(0m, afterCancel.Value.Breakdown.Payments);
    }

    [Fact]
    public async Task Iptal_edilmis_odeme_listede_varsayilan_olarak_gorunmez()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);

        var first = (await factory.CreatePayment().HandleAsync(
            new CreateCardPaymentCommand(card.Id, 100m, "TRY", Today.AddDays(-1), null),
            CancellationToken.None)).Value;
        var second = (await factory.CreatePayment().HandleAsync(
            new CreateCardPaymentCommand(card.Id, 200m, "TRY", Today, null),
            CancellationToken.None)).Value;

        await factory.CancelPayment().HandleAsync(
            new CancelCardPaymentCommand(card.Id, first.Id, "İptal", first.RowVersion),
            CancellationToken.None);

        var active = await factory.ListPayments().HandleAsync(
            new ListCardPaymentsQuery(card.Id, new CardPaymentFilter(null, false, 1, 50)),
            CancellationToken.None);
        Assert.True(active.IsSuccess);
        var activeItem = Assert.Single(active.Value.Items);
        Assert.Equal(second.Id, activeItem.Id);

        var cancelled = await factory.ListPayments().HandleAsync(
            new ListCardPaymentsQuery(card.Id, new CardPaymentFilter(PaymentStatus.Cancelled, false, 1, 50)),
            CancellationToken.None);
        Assert.Single(cancelled.Value.Items);

        var all = await factory.ListPayments().HandleAsync(
            new ListCardPaymentsQuery(card.Id, new CardPaymentFilter(null, true, 1, 1)),
            CancellationToken.None);
        Assert.Equal(2, all.Value.TotalCount);
        Assert.Equal(2, all.Value.TotalPages);
        Assert.Single(all.Value.Items);
    }

    [Fact]
    public async Task Odeme_gelecek_tarih_arsivli_ve_yabanci_kartta_reddedilir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);

        var future = await factory.CreatePayment().HandleAsync(
            new CreateCardPaymentCommand(card.Id, 100m, "TRY", Today.AddDays(1), null),
            CancellationToken.None);
        Assert.Equal("CardPaymentDateInFuture", future.Error.Code);

        var unknown = await factory.CreatePayment().HandleAsync(
            new CreateCardPaymentCommand(Guid.NewGuid(), 100m, "TRY", Today, null),
            CancellationToken.None);
        Assert.Equal("NotFound", unknown.Error.Code);

        await factory.ArchiveCard().HandleAsync(
            new ArchiveCardCommand(card.Id, card.RowVersion),
            CancellationToken.None);

        var archived = await factory.CreatePayment().HandleAsync(
            new CreateCardPaymentCommand(card.Id, 100m, "TRY", Today, null),
            CancellationToken.None);
        Assert.Equal("CardArchived", archived.Error.Code);
    }

    [Fact]
    public async Task Odeme_iptali_rowVersion_ister_ve_eski_surumu_reddeder()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);

        var payment = (await factory.CreatePayment().HandleAsync(
            new CreateCardPaymentCommand(card.Id, 100m, "TRY", Today, null),
            CancellationToken.None)).Value;

        var missing = await factory.CancelPayment().HandleAsync(
            new CancelCardPaymentCommand(card.Id, payment.Id, "İptal", null),
            CancellationToken.None);
        Assert.Equal("RowVersionInvalid", missing.Error.Code);

        var wrong = await factory.CancelPayment().HandleAsync(
            new CancelCardPaymentCommand(card.Id, payment.Id, "İptal", Convert.ToBase64String(new byte[8])),
            CancellationToken.None);
        Assert.Equal("ConcurrencyConflict", wrong.Error.Code);

        var cancelled = await factory.CancelPayment().HandleAsync(
            new CancelCardPaymentCommand(card.Id, payment.Id, "İptal", payment.RowVersion),
            CancellationToken.None);
        Assert.True(cancelled.IsSuccess);

        var stale = await factory.CancelPayment().HandleAsync(
            new CancelCardPaymentCommand(card.Id, payment.Id, "İptal", payment.RowVersion),
            CancellationToken.None);
        Assert.Equal("ConcurrencyConflict", stale.Error.Code);
    }

    [Fact]
    public async Task Kesitli_bakiye_odeme_tarihini_ve_ayni_gun_iptalini_dikkate_alir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);
        var card = await CreateCardAsync(factory);
        factory.ExpenseQuery.AsOfTotals = new(0m, 0m);

        var payment = (await factory.CreatePayment().HandleAsync(
            new CreateCardPaymentCommand(card.Id, 100m, "TRY", new DateOnly(2026, 11, 10), null),
            CancellationToken.None)).Value;

        // İptal, kullanıcının yerel saatinde 2026-11-12; kesit 11-11'de ödeme hâlâ aktif.
        var cancelFactory = new CardHandlerFactory(
            db,
            UserId,
            new DateTime(2026, 11, 12, 9, 30, 0, DateTimeKind.Utc));
        var cancelled = await cancelFactory.CancelPayment().HandleAsync(
            new CancelCardPaymentCommand(card.Id, payment.Id, "İptal", payment.RowVersion),
            CancellationToken.None);
        Assert.True(cancelled.IsSuccess);

        var beforeCancelDay = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(card.Id, new DateOnly(2026, 11, 11)),
            CancellationToken.None);
        Assert.Equal(100m, beforeCancelDay.Value.Breakdown.Payments);
        Assert.Equal(-100m, beforeCancelDay.Value.Debt);

        // Aynı yerel gün (11-12) iptali kesitte iptal sayılır.
        var sameDay = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(card.Id, new DateOnly(2026, 11, 12)),
            CancellationToken.None);
        Assert.Equal(0m, sameDay.Value.Breakdown.Payments);
        Assert.Equal(0m, sameDay.Value.Debt);
    }

    [Fact]
    public async Task Kullanicilar_birbirinin_odemesini_goremez_ve_iptal_edemez()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        var otherUser = Guid.NewGuid();
        Guid cardId;
        Guid paymentId;
        string paymentRowVersion;

        await using (var db = database.CreateContext(UserId))
        {
            var factory = new CardHandlerFactory(db, UserId, Now);
            var card = await CreateCardAsync(factory);
            cardId = card.Id;
            var payment = (await factory.CreatePayment().HandleAsync(
                new CreateCardPaymentCommand(card.Id, 100m, "TRY", Today, null),
                CancellationToken.None)).Value;
            paymentId = payment.Id;
            paymentRowVersion = payment.RowVersion;
        }

        await using (var db = database.CreateContext(otherUser))
        {
            var factory = new CardHandlerFactory(db, otherUser, Now);

            var list = await factory.ListPayments().HandleAsync(
                new ListCardPaymentsQuery(cardId, new CardPaymentFilter(null, false, 1, 50)),
                CancellationToken.None);
            Assert.Equal("NotFound", list.Error.Code);

            var create = await factory.CreatePayment().HandleAsync(
                new CreateCardPaymentCommand(cardId, 100m, "TRY", Today, null),
                CancellationToken.None);
            Assert.Equal("NotFound", create.Error.Code);

            var cancel = await factory.CancelPayment().HandleAsync(
                new CancelCardPaymentCommand(cardId, paymentId, "İptal", paymentRowVersion),
                CancellationToken.None);
            Assert.Equal("NotFound", cancel.Error.Code);
        }
    }

    private static async Task<CardDto> CreateCardAsync(CardHandlerFactory factory) =>
        (await factory.CreateCard().HandleAsync(
            new CreateCardCommand("Banka", "Kart", null, null, null, null),
            CancellationToken.None)).Value;
}
