using FinTrack.ModuleContracts;
using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.Modules.Debts.Application.Loans;
using FinTrack.Modules.Debts.Application.PersonalDebts;
using FinTrack.Modules.Debts.Domain;

namespace FinTrack.Modules.Debts.Application.Reporting;

/// <summary>
/// Reporting sözleşmesinin Debts tarafı. Kalan tutarlar aggregate'lerden
/// türetilir; iptal edilen borçlar ve ödenmiş/iptal taksitler hariçtir.
/// </summary>
public sealed class DebtReportQuery(
    ILoanQueries loans,
    IPersonalDebtQueries personalDebts) : IDebtReportQuery
{
    private const int PageSize = 200;

    public async Task<IReadOnlyList<LoanDebtEntry>> GetLoansAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var items = await ListLoansAsync(userId, cancellationToken);

        return items
            .OrderBy(loan => loan.Id)
            .Select(loan => new LoanDebtEntry(
                loan.Id,
                loan.BankName,
                loan.RemainingAmount))
            .ToArray();
    }

    public async Task<IReadOnlyList<PersonalDebtEntry>> GetPersonalDebtsAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var items = await ListPersonalDebtsAsync(userId, cancellationToken);

        return items
            .OrderBy(debt => debt.Id)
            .Select(debt => new PersonalDebtEntry(
                debt.Id,
                debt.CounterpartyName,
                debt.RemainingAmount))
            .ToArray();
    }

    public async Task<IReadOnlyList<LoanInstallmentDue>> GetUpcomingInstallmentsAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var items = await ListLoansAsync(userId, cancellationToken);

        return items
            .SelectMany(loan => loan.Installments
                .Where(installment => installment.Status == InstallmentStatus.Planned
                    && installment.DueDate >= from
                    && installment.DueDate <= to)
                .Select(installment => new LoanInstallmentDue(
                    loan.Id,
                    installment.Number,
                    installment.DueDate,
                    installment.Amount)))
            .OrderBy(due => due.DueDate)
            .ThenBy(due => due.LoanId)
            .ThenBy(due => due.InstallmentNumber)
            .ToArray();
    }

    private async Task<IReadOnlyList<LoanDto>> ListLoansAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var items = new List<LoanDto>();
        var page = 1;

        while (true)
        {
            var result = await loans.ListAsync(
                userId,
                new LoanFilter(
                    Status: null,
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

    private async Task<IReadOnlyList<PersonalDebtDto>> ListPersonalDebtsAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var items = new List<PersonalDebtDto>();
        var page = 1;

        while (true)
        {
            var result = await personalDebts.ListAsync(
                userId,
                new PersonalDebtFilter(
                    Status: null,
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
