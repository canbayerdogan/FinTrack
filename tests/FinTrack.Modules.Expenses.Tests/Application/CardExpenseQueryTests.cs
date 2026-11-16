using FinTrack.Modules.Expenses.Domain;
using FinTrack.Modules.Expenses.Infrastructure.Queries;
using FinTrack.Modules.Expenses.Infrastructure.Repositories;
using FinTrack.Modules.Expenses.Tests.Support;
using Xunit;

namespace FinTrack.Modules.Expenses.Tests.Application;

/// <summary>
/// <see cref="ICardExpenseQuery"/> uygulaması Expenses tarafında yaşar (ADR-0008).
/// Kart borcu yalnızca aktif harcama ve iadelerden beslenir.
/// </summary>
public sealed class CardExpenseQueryTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 9, 30, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 14);
    private static readonly DateOnly Past = Today.AddDays(-1);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Kart_toplamlari_yalnizca_aktif_harcama_ve_iadeleri_sayar()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);

        var category = Category.Create(UserId, "Market", null, Now).Value;
        await db.Categories.AddAsync(category);

        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();

        var refundedExpense = CreateExpense(cardA, 100m, category.Id);
        var refund = refundedExpense.AddRefund(30m, "TRY", Past, null, Today, Now).Value;

        var cancelledExpense = CreateExpense(cardA, 50m, category.Id);
        cancelledExpense.Cancel("Yanlış kayıt", Now);

        var cashExpense = CreateExpense(null, 70m, category.Id, PaymentMethod.Cash);

        var otherCardExpense = CreateExpense(cardB, 200m, category.Id);
        var cancelledRefund = otherCardExpense.AddRefund(10m, "TRY", Past, null, Today, Now).Value;
        otherCardExpense.CancelRefund(cancelledRefund.Id, "İade iptal", Now);

        await db.Expenses.AddRangeAsync(refundedExpense, cancelledExpense, cashExpense, otherCardExpense);

        var repository = new ExpenseRepository(db);
        await repository.AddRefundAsync(refund, CancellationToken.None);
        await repository.AddRefundAsync(cancelledRefund, CancellationToken.None);

        await db.SaveChangesAsync();

        var query = new CardExpenseQuery(db);

        var totalsA = await query.GetCardTotalsAsync(UserId, cardA, CancellationToken.None);
        Assert.Equal(100m, totalsA.Expenses);
        Assert.Equal(30m, totalsA.Refunds);

        var totalsB = await query.GetCardTotalsAsync(UserId, cardB, CancellationToken.None);
        Assert.Equal(200m, totalsB.Expenses);
        Assert.Equal(0m, totalsB.Refunds);
    }

    [Fact]
    public async Task Baska_kullanicinin_kart_kayitlari_toplama_girmez()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var otherUser = Guid.NewGuid();
        var otherCard = Guid.NewGuid();

        await using (var db = database.CreateContext(UserId))
        {
            var category = Category.Create(UserId, "Market", null, Now).Value;
            await db.Categories.AddAsync(category);
            await db.Expenses.AddAsync(CreateExpense(otherCard, 500m, category.Id));
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateContext(otherUser))
        {
            var query = new CardExpenseQuery(db);
            var totals = await query.GetCardTotalsAsync(otherUser, otherCard, CancellationToken.None);

            Assert.Equal(0m, totals.Expenses);
            Assert.Equal(0m, totals.Refunds);
        }
    }

    [Fact]
    public async Task Kesit_gelecek_hareketleri_ve_iadeleri_haric_tutar()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);

        var category = Category.Create(UserId, "Market", null, Now).Value;
        await db.Categories.AddAsync(category);

        var cardId = Guid.NewGuid();
        var asOf = new DateOnly(2026, 11, 10);

        var pastExpense = CreateExpense(cardId, 100m, category.Id, occurredOn: new DateOnly(2026, 11, 5));
        var futureExpense = CreateExpense(cardId, 200m, category.Id, occurredOn: new DateOnly(2026, 11, 12));
        var pastRefund = pastExpense.AddRefund(30m, "TRY", new DateOnly(2026, 11, 8), null, Today, Now).Value;
        var futureRefund = pastExpense.AddRefund(20m, "TRY", new DateOnly(2026, 11, 12), null, Today, Now).Value;

        await db.Expenses.AddRangeAsync(pastExpense, futureExpense);
        var repository = new ExpenseRepository(db);
        await repository.AddRefundAsync(pastRefund, CancellationToken.None);
        await repository.AddRefundAsync(futureRefund, CancellationToken.None);
        await db.SaveChangesAsync();

        var query = new CardExpenseQuery(db);
        var totals = await query.GetCardTotalsAsOfAsync(
            UserId,
            cardId,
            asOf,
            "Europe/Istanbul",
            CancellationToken.None);

        Assert.Equal(100m, totals.Expenses);
        Assert.Equal(30m, totals.Refunds);
    }

    [Fact]
    public async Task Ayni_gun_iptal_kesitte_iptal_sonraki_gun_iptal_aktif_sayilir()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);

        var category = Category.Create(UserId, "Market", null, Now).Value;
        await db.Categories.AddAsync(category);

        var cardId = Guid.NewGuid();
        var asOf = new DateOnly(2026, 11, 14);

        // Yerel gün (İstanbul) 11-14 iken iptal: kesitte iptal sayılır.
        var sameDayCancelled = CreateExpense(cardId, 100m, category.Id);
        sameDayCancelled.Cancel("Aynı gün iptal", new DateTime(2026, 11, 14, 9, 30, 0, DateTimeKind.Utc));

        // UTC 11-14 21:30 = yerel 11-15: kesitten sonra, kesitte aktif sayılır.
        var nextDayCancelled = CreateExpense(
            cardId,
            200m,
            category.Id,
            occurredOn: new DateOnly(2026, 11, 1));

        var sameDayRefund = nextDayCancelled
            .AddRefund(40m, "TRY", new DateOnly(2026, 11, 10), null, Today, Now).Value;
        nextDayCancelled.CancelRefund(
            sameDayRefund.Id,
            "Aynı gün iptal",
            new DateTime(2026, 11, 14, 9, 30, 0, DateTimeKind.Utc));

        var nextDayRefund = nextDayCancelled
            .AddRefund(25m, "TRY", new DateOnly(2026, 11, 13), null, Today, Now).Value;
        nextDayCancelled.CancelRefund(
            nextDayRefund.Id,
            "Sonraki gün iptal",
            new DateTime(2026, 11, 14, 21, 30, 0, DateTimeKind.Utc));

        nextDayCancelled.Cancel(
            "Sonraki gün iptal",
            new DateTime(2026, 11, 14, 21, 30, 0, DateTimeKind.Utc));

        await db.Expenses.AddAsync(sameDayCancelled);
        await db.Expenses.AddAsync(nextDayCancelled);
        var repository = new ExpenseRepository(db);
        await repository.AddRefundAsync(sameDayRefund, CancellationToken.None);
        await repository.AddRefundAsync(nextDayRefund, CancellationToken.None);
        await db.SaveChangesAsync();

        var query = new CardExpenseQuery(db);
        var totals = await query.GetCardTotalsAsOfAsync(
            UserId,
            cardId,
            asOf,
            "Europe/Istanbul",
            CancellationToken.None);

        Assert.Equal(200m, totals.Expenses);
        Assert.Equal(25m, totals.Refunds);
    }

    [Fact]
    public async Task Kesit_iptal_gununu_kullanici_saat_diliminde_degerlendirir()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);

        var category = Category.Create(UserId, "Market", null, Now).Value;
        await db.Categories.AddAsync(category);

        var cardId = Guid.NewGuid();
        var expense = CreateExpense(cardId, 100m, category.Id);

        // UTC 11-14 10:30: UTC+14'te yerel 11-15, UTC-11'de yerel 11-13.
        expense.Cancel("Saat dilimi", new DateTime(2026, 11, 14, 10, 30, 0, DateTimeKind.Utc));

        await db.Expenses.AddAsync(expense);
        await db.SaveChangesAsync();

        var query = new CardExpenseQuery(db);
        var asOf = new DateOnly(2026, 11, 14);

        // UTC+14'te kesit 11-14 09:59:59Z'de biter; iptal sonrası olduğundan aktif.
        var kiritimati = await query.GetCardTotalsAsOfAsync(
            UserId,
            cardId,
            asOf,
            "Pacific/Kiritimati",
            CancellationToken.None);
        Assert.Equal(100m, kiritimati.Expenses);

        // UTC-11'de kesit 11-15 10:59:59Z'de biter; iptal kesitten önce olduğundan hariç.
        var pagoPago = await query.GetCardTotalsAsOfAsync(
            UserId,
            cardId,
            asOf,
            "Pacific/Pago_Pago",
            CancellationToken.None);
        Assert.Equal(0m, pagoPago.Expenses);
    }

    private static Expense CreateExpense(
        Guid? cardId,
        decimal amount,
        Guid categoryId,
        PaymentMethod paymentMethod = PaymentMethod.CreditCard,
        DateOnly? occurredOn = null) =>
        Expense.Create(
            UserId,
            amount,
            "TRY",
            occurredOn ?? Past,
            categoryId,
            null,
            paymentMethod,
            cardId,
            Today,
            Now).Value;
}
