using FinTrack.Modules.CashInflows.Application.Abstractions;
using FinTrack.Modules.CashInflows.Application.Common;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CashInflows.Application.CashInflows;

public sealed class ListCashInflowsHandler(
    ICurrentUser currentUser,
    ICashInflowQueries queries)
    : IQueryHandler<ListCashInflowsQuery, Result<PagedResult<CashInflowDto>>>
{
    public async Task<Result<PagedResult<CashInflowDto>>> HandleAsync(
        ListCashInflowsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var result = await queries.ListAsync(userId, query.Filter, cancellationToken);
        return Result.Success(result);
    }
}

public sealed class GetCashInflowHandler(
    ICurrentUser currentUser,
    ICashInflowQueries queries)
    : IQueryHandler<GetCashInflowQuery, Result<CashInflowDto>>
{
    public async Task<Result<CashInflowDto>> HandleAsync(
        GetCashInflowQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var inflow = await queries.FindAsync(userId, query.Id, cancellationToken);

        return inflow is null
            ? Result.Failure<CashInflowDto>(ApplicationErrors.CashInflowNotFound())
            : Result.Success(inflow);
    }
}
