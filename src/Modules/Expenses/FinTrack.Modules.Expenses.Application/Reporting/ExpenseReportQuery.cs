using FinTrack.ModuleContracts;
using FinTrack.Modules.Expenses.Application.Abstractions;
using FinTrack.Modules.Expenses.Application.Expenses;
using FinTrack.Modules.Expenses.Domain;

namespace FinTrack.Modules.Expenses.Application.Reporting;

/// <summary>
/// Reporting sözleşmesinin Expenses tarafı (docs/05 §7.2–7.3). Harcama
/// <c>OccurredOn</c>, iade <c>RefundedOn</c> tarihine göre döneme girer;
/// iptal edilen kayıtlar hariçtir. İade orijinal harcamanın kategorisine
/// negatif gider olarak yansır.
/// </summary>
public sealed class ExpenseReportQuery(
    IExpenseQueries expenses) : IExpenseReportQuery
{
    private const int PageSize = 200;

    public async Task<ExpenseReportTotals> GetMonthlyAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var allExpenses = await ListAllActiveExpensesAsync(userId, cancellationToken);

        var refundsByExpense = new Dictionary<Guid, IReadOnlyList<RefundDto>>();
        foreach (var expense in allExpenses)
        {
            var refunds = await expenses.ListRefundsAsync(userId, expense.Id, cancellationToken);
            refundsByExpense[expense.Id] = (refunds ?? [])
                .Where(refund => refund.Status == RefundStatus.Active)
                .ToArray();
        }

        var expensesInPeriod = allExpenses
            .Where(expense => expense.OccurredOn >= from && expense.OccurredOn <= to)
            .ToArray();

        var refundsInPeriod = allExpenses
            .SelectMany(expense => refundsByExpense[expense.Id]
                .Where(refund => refund.RefundedOn >= from && refund.RefundedOn <= to)
                .Select(refund => new { Expense = expense, Refund = refund }))
            .ToArray();

        var gross = expensesInPeriod.Sum(expense => expense.Amount);
        var refundTotal = refundsInPeriod.Sum(item => item.Refund.Amount);

        var categoryNames = allExpenses.ToDictionary(
            expense => expense.CategoryId,
            expense => expense.CategoryName);

        var netByCategory = new Dictionary<Guid, decimal>();
        foreach (var expense in expensesInPeriod)
        {
            netByCategory[expense.CategoryId] =
                netByCategory.GetValueOrDefault(expense.CategoryId) + expense.Amount;
        }

        foreach (var item in refundsInPeriod)
        {
            netByCategory[item.Expense.CategoryId] =
                netByCategory.GetValueOrDefault(item.Expense.CategoryId) - item.Refund.Amount;
        }

        var categories = netByCategory
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => categoryNames[entry.Key], StringComparer.OrdinalIgnoreCase)
            .Select(entry => new ExpenseCategoryTotal(
                entry.Key,
                categoryNames[entry.Key],
                entry.Value))
            .ToArray();

        return new ExpenseReportTotals(gross, refundTotal, gross - refundTotal, categories);
    }

    private async Task<IReadOnlyList<ExpenseDto>> ListAllActiveExpensesAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var items = new List<ExpenseDto>();
        var page = 1;

        while (true)
        {
            var result = await expenses.ListAsync(
                userId,
                new ExpenseFilter(
                    From: null,
                    To: null,
                    CategoryId: null,
                    CardId: null,
                    PaymentMethod: null,
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
