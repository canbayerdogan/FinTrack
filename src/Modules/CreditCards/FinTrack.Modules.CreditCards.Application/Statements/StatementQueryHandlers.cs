using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Application.Statements;

/// <summary>
/// Kartın ekstrelerini listeler (docs/06 §6.6). Kart yoksa veya kullanıcıya ait
/// değilse 404 döner. Türetilen alanlar her okumada yeniden hesaplanır.
/// </summary>
public sealed class ListStatementsHandler(
    ICurrentUser currentUser,
    ICardQueries cards,
    IStatementQueries queries,
    StatementDerivation derivation)
    : IQueryHandler<ListStatementsQuery, Result<PagedResult<StatementDto>>>
{
    public async Task<Result<PagedResult<StatementDto>>> HandleAsync(
        ListStatementsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var card = await cards.FindAsync(userId, query.CardId, cancellationToken);
        if (card is null)
        {
            return Result.Failure<PagedResult<StatementDto>>(ApplicationErrors.CardNotFound());
        }

        var records = await queries.ListAsync(userId, query.CardId, query.Filter, cancellationToken);

        var timeZoneId = currentUser.PreferredTimeZone;
        var items = new List<StatementDto>(records.Items.Count);
        foreach (var record in records.Items)
        {
            items.Add(await derivation.ToDtoAsync(userId, record, timeZoneId, cancellationToken));
        }

        var result = new PagedResult<StatementDto>(
            items,
            records.Page,
            records.PageSize,
            records.TotalCount);

        return Result.Success(result);
    }
}

/// <summary>Tek ekstreyi türetilen alanlarıyla döner (docs/06 §6.6).</summary>
public sealed class GetStatementHandler(
    ICurrentUser currentUser,
    IStatementQueries queries,
    StatementDerivation derivation)
    : IQueryHandler<GetStatementQuery, Result<StatementDto>>
{
    public async Task<Result<StatementDto>> HandleAsync(
        GetStatementQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var record = await queries.FindAsync(userId, query.CardId, query.StatementId, cancellationToken);
        if (record is null)
        {
            return Result.Failure<StatementDto>(ApplicationErrors.StatementNotFound());
        }

        var dto = await derivation.ToDtoAsync(
            userId,
            record,
            currentUser.PreferredTimeZone,
            cancellationToken);

        return Result.Success(dto);
    }
}
