using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Application.Payments;

/// <summary>
/// Kartın ödemelerini listeler (docs/06 §6.4). Kart yoksa veya kullanıcıya ait
/// değilse 404 döner; böylece başka kullanıcının verisi sızdırılmaz.
/// </summary>
public sealed class ListCardPaymentsHandler(
    ICurrentUser currentUser,
    ICardQueries cards,
    ICardPaymentQueries queries)
    : IQueryHandler<ListCardPaymentsQuery, Result<PagedResult<CardPaymentDto>>>
{
    public async Task<Result<PagedResult<CardPaymentDto>>> HandleAsync(
        ListCardPaymentsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var card = await cards.FindAsync(userId, query.CardId, cancellationToken);
        if (card is null)
        {
            return Result.Failure<PagedResult<CardPaymentDto>>(ApplicationErrors.CardNotFound());
        }

        var result = await queries.ListAsync(userId, query.CardId, query.Filter, cancellationToken);
        return Result.Success(result);
    }
}
