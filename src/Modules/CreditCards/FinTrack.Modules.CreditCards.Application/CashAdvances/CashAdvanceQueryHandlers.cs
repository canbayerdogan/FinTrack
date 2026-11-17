using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Application.CashAdvances;

/// <summary>
/// Kartın nakit avanslarını listeler (docs/06 §6.5). Kart yoksa veya
/// kullanıcıya ait değilse 404 döner; başka kullanıcının verisi sızdırılmaz.
/// </summary>
public sealed class ListCashAdvancesHandler(
    ICurrentUser currentUser,
    ICardQueries cards,
    ICashAdvanceQueries queries)
    : IQueryHandler<ListCashAdvancesQuery, Result<PagedResult<CashAdvanceDto>>>
{
    public async Task<Result<PagedResult<CashAdvanceDto>>> HandleAsync(
        ListCashAdvancesQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var card = await cards.FindAsync(userId, query.CardId, cancellationToken);
        if (card is null)
        {
            return Result.Failure<PagedResult<CashAdvanceDto>>(ApplicationErrors.CardNotFound());
        }

        var result = await queries.ListAsync(userId, query.CardId, query.Filter, cancellationToken);
        return Result.Success(result);
    }
}
