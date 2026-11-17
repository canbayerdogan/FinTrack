using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.Modules.Debts.Application.Common;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Application.Loans;

public sealed class ListLoansHandler(ICurrentUser currentUser, ILoanQueries queries)
    : IQueryHandler<ListLoansQuery, Result<PagedResult<LoanDto>>>
{
    public async Task<Result<PagedResult<LoanDto>>> HandleAsync(
        ListLoansQuery query,
        CancellationToken cancellationToken)
    {
        var items = await queries.ListAsync(currentUser.RequireUserId(), query.Filter, cancellationToken);
        return Result.Success(items);
    }
}

public sealed class GetLoanHandler(ICurrentUser currentUser, ILoanQueries queries)
    : IQueryHandler<GetLoanQuery, Result<LoanDto>>
{
    public async Task<Result<LoanDto>> HandleAsync(
        GetLoanQuery query,
        CancellationToken cancellationToken)
    {
        var loan = await queries.FindAsync(currentUser.RequireUserId(), query.LoanId, cancellationToken);
        return loan is null
            ? Result.Failure<LoanDto>(ApplicationErrors.LoanNotFound())
            : Result.Success(loan);
    }
}
