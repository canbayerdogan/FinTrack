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
/// Kart borcunu kesitli olarak hesaplar (docs/06 §6.3, docs/05 §5.3).
/// <c>debt = expenses − refunds + cashAdvances − payments</c>. Kesit
/// verilmezse kullanıcının bugünkü tarihi kullanılır; gelecek tarih reddedilir.
/// Arşivli kartın geçmiş kayıtları korunur; bakiyesi okunabilir.
/// </summary>
public sealed class GetCardBalanceHandler(
    ICurrentUser currentUser,
    ICardQueries queries,
    CardDebtService debtService,
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

        var timeZoneId = currentUser.PreferredTimeZone;
        var today = UserTime.Today(clock, timeZoneId);
        var asOf = query.AsOf ?? today;
        if (asOf > today)
        {
            return Result.Failure<CardBalanceDto>(ApplicationErrors.AsOfInFuture());
        }

        var components = await debtService.ComputeAsync(
            userId,
            query.Id,
            asOf,
            timeZoneId,
            cancellationToken);

        var balance = new CardBalanceDto(
            card.Id,
            asOf,
            Money.DefaultCurrency,
            components.Debt,
            IsInCredit: components.Debt < 0m,
            new CardBalanceBreakdownDto(
                components.Expenses,
                components.Refunds,
                components.CashAdvances,
                components.Payments));

        return Result.Success(balance);
    }
}
