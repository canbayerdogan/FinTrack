using FinTrack.Modules.Expenses.Application.Abstractions;
using FinTrack.Modules.Expenses.Application.Common;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Expenses.Application.Expenses;

public sealed class ListExpensesHandler(
    ICurrentUser currentUser,
    IExpenseQueries queries)
    : IQueryHandler<ListExpensesQuery, Result<PagedResult<ExpenseDto>>>
{
    public async Task<Result<PagedResult<ExpenseDto>>> HandleAsync(
        ListExpensesQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var result = await queries.ListAsync(userId, query.Filter, cancellationToken);
        return Result.Success(result);
    }
}

public sealed class GetExpenseHandler(
    ICurrentUser currentUser,
    IExpenseQueries queries)
    : IQueryHandler<GetExpenseQuery, Result<ExpenseDto>>
{
    public async Task<Result<ExpenseDto>> HandleAsync(
        GetExpenseQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var expense = await queries.FindAsync(userId, query.Id, cancellationToken);

        return expense is null
            ? Result.Failure<ExpenseDto>(ApplicationErrors.ExpenseNotFound())
            : Result.Success(expense);
    }
}

public sealed class ListRefundsHandler(
    ICurrentUser currentUser,
    IExpenseQueries queries)
    : IQueryHandler<ListRefundsQuery, Result<IReadOnlyList<RefundDto>>>
{
    public async Task<Result<IReadOnlyList<RefundDto>>> HandleAsync(
        ListRefundsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var refunds = await queries.ListRefundsAsync(userId, query.ExpenseId, cancellationToken);

        return refunds is null
            ? Result.Failure<IReadOnlyList<RefundDto>>(ApplicationErrors.ExpenseNotFound())
            : Result.Success(refunds);
    }
}
