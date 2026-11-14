using FinTrack.ModuleContracts;
using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Application.Cards;

public sealed class ListCardsHandler(
    ICurrentUser currentUser,
    ICardQueries queries)
    : IQueryHandler<ListCardsQuery, Result<IReadOnlyList<CardDto>>>
{
    public async Task<Result<IReadOnlyList<CardDto>>> HandleAsync(
        ListCardsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var cards = await queries.ListAsync(userId, query.Filter, cancellationToken);
        return Result.Success(cards);
    }
}

public sealed class GetCardHandler(
    ICurrentUser currentUser,
    ICardQueries queries)
    : IQueryHandler<GetCardQuery, Result<CardDto>>
{
    public async Task<Result<CardDto>> HandleAsync(
        GetCardQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var card = await queries.FindAsync(userId, query.Id, cancellationToken);

        return card is null
            ? Result.Failure<CardDto>(ApplicationErrors.CardNotFound())
            : Result.Success(card);
    }
}

/// <summary>
/// Kart borcunu harcama/iadeden hesaplar (docs/06 §6.3, docs/05 §5.3).
/// Slice 3 formülü: <c>debt = expenses − refunds</c>; ödeme ve nakit avans
/// terimleri sonraki dilimlerde eklenir. Tüm zamanlar üzerinden çalışır.
/// Arşivli kartın geçmiş kayıtları korunur; bakiyesi okunabilir.
/// </summary>
public sealed class GetCardBalanceHandler(
    ICurrentUser currentUser,
    ICardQueries queries,
    ICardExpenseQuery expenseQuery,
    IClock clock)
    : IQueryHandler<GetCardBalanceQuery, Result<CardBalanceDto>>
{
    public async Task<Result<CardBalanceDto>> HandleAsync(
        GetCardBalanceQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var card = await queries.FindAsync(userId, query.Id, cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardBalanceDto>(ApplicationErrors.CardNotFound());
        }

        var totals = await expenseQuery.GetCardTotalsAsync(userId, query.Id, cancellationToken);

        const decimal cashAdvances = 0m;
        const decimal payments = 0m;

        var debt = totals.Expenses - totals.Refunds + cashAdvances - payments;

        var balance = new CardBalanceDto(
            card.Id,
            clock.UtcNow,
            Money.DefaultCurrency,
            debt,
            IsInCredit: debt < 0m,
            new CardBalanceBreakdownDto(totals.Expenses, totals.Refunds, cashAdvances, payments));

        return Result.Success(balance);
    }
}
