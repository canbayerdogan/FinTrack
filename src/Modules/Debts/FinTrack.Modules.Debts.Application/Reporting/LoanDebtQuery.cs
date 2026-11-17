using FinTrack.ModuleContracts;
using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Application.Reporting;

/// <summary>
/// Reporting modülünün borç toplamı sözleşmesi (Slice 8 hazırlığı). Kredi ve
/// kişisel borçların kalan tutarlarını tek bir toplamda birleştirir; iptal
/// edilen borçlar dahil edilmez.
/// </summary>
public sealed class LoanDebtQuery(IDebtSummaryQueries queries) : ILoanDebtQuery
{
    public async Task<Money> GetTotalRemainingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var total = await queries.GetTotalRemainingAsync(userId, cancellationToken);
        return Money.Try(total);
    }
}
