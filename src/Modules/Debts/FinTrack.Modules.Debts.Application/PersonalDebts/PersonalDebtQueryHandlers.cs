using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.Modules.Debts.Application.Common;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Application.PersonalDebts;

public sealed class ListPersonalDebtsHandler(
    ICurrentUser currentUser,
    IPersonalDebtQueries queries)
    : IQueryHandler<ListPersonalDebtsQuery, Result<PagedResult<PersonalDebtDto>>>
{
    public async Task<Result<PagedResult<PersonalDebtDto>>> HandleAsync(
        ListPersonalDebtsQuery query,
        CancellationToken cancellationToken)
    {
        var items = await queries.ListAsync(
            currentUser.RequireUserId(),
            query.Filter,
            cancellationToken);
        return Result.Success(items);
    }
}

public sealed class GetPersonalDebtHandler(
    ICurrentUser currentUser,
    IPersonalDebtQueries queries)
    : IQueryHandler<GetPersonalDebtQuery, Result<PersonalDebtDto>>
{
    public async Task<Result<PersonalDebtDto>> HandleAsync(
        GetPersonalDebtQuery query,
        CancellationToken cancellationToken)
    {
        var debt = await queries.FindAsync(currentUser.RequireUserId(), query.DebtId, cancellationToken);
        return debt is null
            ? Result.Failure<PersonalDebtDto>(ApplicationErrors.PersonalDebtNotFound())
            : Result.Success(debt);
    }
}

public sealed class ListRepaymentsHandler(
    ICurrentUser currentUser,
    IPersonalDebtQueries queries)
    : IQueryHandler<ListRepaymentsQuery, Result<IReadOnlyList<RepaymentDto>>>
{
    public async Task<Result<IReadOnlyList<RepaymentDto>>> HandleAsync(
        ListRepaymentsQuery query,
        CancellationToken cancellationToken)
    {
        var items = await queries.ListRepaymentsAsync(
            currentUser.RequireUserId(),
            query.DebtId,
            cancellationToken);
        return Result.Success(items);
    }
}
