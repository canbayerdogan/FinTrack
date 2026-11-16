using FinTrack.ModuleContracts;
using FinTrack.Modules.CreditCards.Application.Abstractions;

namespace FinTrack.Modules.CreditCards.Application.Cards;

/// <summary>
/// Bir kesitte kart borcunun bileşenleri (docs/06 §6.3). Nakit avans slice 6'da
/// dolar; slice 4'te sıfırdır.
/// </summary>
public sealed record CardBalanceComponents(
    decimal Expenses,
    decimal Refunds,
    decimal CashAdvances,
    decimal Payments)
{
    /// <summary><c>debt = expenses − refunds + cashAdvances − payments</c>.</summary>
    public decimal Debt => Expenses - Refunds + CashAdvances - Payments;
}

/// <summary>
/// Kesitli kart borcunu tek yerden hesaplar (docs/05 §5.3). Hem bakiye uç
/// noktası hem ekstre türetimi bu servisi kullanır; böylece kesit ve iptal
/// semantiği tek noktada tutulur. Harcama/iade verisi Expenses'tan
/// <see cref="ICardExpenseQuery"/> ile okunur (ADR-0008); ödemeler CreditCards
/// modülünün kendi verisidir.
/// </summary>
public sealed class CardDebtService(
    ICardExpenseQuery expenseQuery,
    ICardPaymentQueries paymentQueries)
{
    public async Task<CardBalanceComponents> ComputeAsync(
        Guid userId,
        Guid cardId,
        DateOnly asOfDate,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        var totals = await expenseQuery.GetCardTotalsAsOfAsync(
            userId,
            cardId,
            asOfDate,
            timeZoneId,
            cancellationToken);

        var payments = await paymentQueries.SumActivePaymentsAsync(
            userId,
            cardId,
            paidAfterExclusive: null,
            asOfDate,
            timeZoneId,
            cancellationToken);

        const decimal cashAdvances = 0m;

        return new CardBalanceComponents(totals.Expenses, totals.Refunds, cashAdvances, payments);
    }
}
