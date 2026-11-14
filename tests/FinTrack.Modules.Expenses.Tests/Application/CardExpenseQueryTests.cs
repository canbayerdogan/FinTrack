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

    private static Expense CreateExpense(
        Guid? cardId,
        decimal amount,
        Guid categoryId,
        PaymentMethod paymentMethod = PaymentMethod.CreditCard) =>
        Expense.Create(
            UserId,
            amount,
            "TRY",
            Past,
            categoryId,
            null,
            paymentMethod,
            cardId,
            Today,
            Now).Value;
}
