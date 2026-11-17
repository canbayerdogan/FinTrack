using FinTrack.ModuleContracts;
using FinTrack.Modules.CashInflows.Application.Abstractions;
using FinTrack.Modules.CashInflows.Application.CashInflows;
using FinTrack.Modules.CashInflows.Domain;

namespace FinTrack.Modules.CashInflows.Application.Reporting;

/// <summary>
/// Reporting sözleşmesinin CashInflows tarafı (docs/05 §7.2). Kazanılmış gelir,
/// bilinen diğer girişler ve belirsiz girişler birbirine karışmaz.
/// </summary>
public sealed class CashInflowReportQuery(
    ICashInflowQueries inflows) : ICashInflowReportQuery
{
    private const int PageSize = 200;

    public async Task<CashInflowReportTotals> GetMonthlyAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var items = await ListActiveInflowsAsync(userId, from, to, cancellationToken);

        var earnedIncome = items
            .Where(item => item.IsEarnedIncome == true)
            .Sum(item => item.Amount);

        var unknown = items
            .Where(item => item.Source == CashInflowSource.Unknown)
            .ToArray();

        var other = items
            .Where(item => item.IsEarnedIncome == false
                && item.Source != CashInflowSource.Unknown)
            .GroupBy(item => item.Source.ToString())
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CashInflowSourceTotal(group.Key, group.Sum(item => item.Amount)))
            .ToArray();

        return new CashInflowReportTotals(
            earnedIncome,
            other.Sum(item => item.Amount),
            other,
            unknown.Sum(item => item.Amount),
            unknown.Length);
    }

    private async Task<IReadOnlyList<CashInflowDto>> ListActiveInflowsAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var items = new List<CashInflowDto>();
        var page = 1;

        while (true)
        {
            var result = await inflows.ListAsync(
                userId,
                new CashInflowFilter(
                    From: from,
                    To: to,
                    Source: null,
                    Status: CashInflowStatus.Active,
                    IncludeCancelled: false,
                    Page: page,
                    PageSize: PageSize),
                cancellationToken);

            items.AddRange(result.Items);

            if (result.Items.Count == 0 || page >= result.TotalPages)
            {
                break;
            }

            page++;
        }

        return items;
    }
}
