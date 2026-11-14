using FinTrack.Modules.Expenses.Application.Abstractions;
using FinTrack.Modules.Expenses.Application.Categories;
using FinTrack.Modules.Expenses.Application.Common;
using FinTrack.Modules.Expenses.Application.Expenses;
using FinTrack.Modules.Expenses.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Expenses.Infrastructure.Queries;

internal sealed class CategoryQueries(ExpensesDbContext db) : ICategoryQueries
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(
        Guid userId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var query = db.Categories
            .AsNoTracking()
            .Where(category => category.UserId == userId);

        if (!includeArchived)
        {
            query = query.Where(category => !category.IsArchived);
        }

        var categories = await query
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);

        return categories.Select(CategoryMapper.ToDto).ToList();
    }
}

internal sealed class ExpenseQueries(ExpensesDbContext db) : IExpenseQueries
{
    public async Task<PagedResult<ExpenseDto>> ListAsync(
        Guid userId,
        ExpenseFilter filter,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var query = db.Expenses
            .AsNoTracking()
            .Where(expense => expense.UserId == userId);

        if (filter.From is not null)
        {
            query = query.Where(expense => expense.OccurredOn >= filter.From.Value);
        }

        if (filter.To is not null)
        {
            query = query.Where(expense => expense.OccurredOn <= filter.To.Value);
        }

        if (filter.CategoryId is not null)
        {
            query = query.Where(expense => expense.CategoryId == filter.CategoryId.Value);
        }

        if (filter.CardId is not null)
        {
            query = query.Where(expense => expense.CardId == filter.CardId.Value);
        }

        if (filter.PaymentMethod is not null)
        {
            query = query.Where(expense => expense.PaymentMethod == filter.PaymentMethod.Value);
        }

        // Durum, aktif iadelerin toplamından türetilir. Filtreler projeksiyondan
        // önce uygulanır; aksi halde EF kayıt tipi üzerinden çeviremez.
        if (filter.Status is not null)
        {
            query = filter.Status.Value switch
            {
                ExpenseStatus.Active => query.Where(expense =>
                    expense.CancelledAtUtc == null
                    && !db.Refunds.Any(refund =>
                        refund.ExpenseId == expense.Id && refund.CancelledAtUtc == null)),
                ExpenseStatus.PartiallyRefunded => query.Where(expense =>
                    expense.CancelledAtUtc == null
                    && (db.Refunds
                        .Where(refund => refund.ExpenseId == expense.Id && refund.CancelledAtUtc == null)
                        .Sum(refund => (decimal?)refund.Amount) ?? 0m) > 0m
                    && (db.Refunds
                        .Where(refund => refund.ExpenseId == expense.Id && refund.CancelledAtUtc == null)
                        .Sum(refund => (decimal?)refund.Amount) ?? 0m) < expense.Amount),
                ExpenseStatus.Refunded => query.Where(expense =>
                    expense.CancelledAtUtc == null
                    && (db.Refunds
                        .Where(refund => refund.ExpenseId == expense.Id && refund.CancelledAtUtc == null)
                        .Sum(refund => (decimal?)refund.Amount) ?? 0m) >= expense.Amount),
                ExpenseStatus.Cancelled => query.Where(expense => expense.CancelledAtUtc != null),
                _ => query,
            };
        }
        else if (!filter.IncludeCancelled)
        {
            query = query.Where(expense => expense.CancelledAtUtc == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var paged = query
            .OrderByDescending(expense => expense.OccurredOn)
            .ThenByDescending(expense => expense.RecordedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        var rows = await Project(paged).ToListAsync(cancellationToken);

        var items = rows
            .Select(row => ToDto(row.Expense, row.CategoryName, row.RefundedAmount))
            .ToList();

        return new PagedResult<ExpenseDto>(items, page, pageSize, totalCount);
    }

    public async Task<ExpenseDto?> FindAsync(
        Guid userId,
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        var row = await Project(db.Expenses
                .AsNoTracking()
                .Where(expense => expense.UserId == userId && expense.Id == expenseId))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : ToDto(row.Expense, row.CategoryName, row.RefundedAmount);
    }

    public async Task<IReadOnlyList<RefundDto>?> ListRefundsAsync(
        Guid userId,
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Expenses
            .AsNoTracking()
            .AnyAsync(expense => expense.Id == expenseId && expense.UserId == userId, cancellationToken);

        if (!exists)
        {
            return null;
        }

        var refunds = await db.Refunds
            .AsNoTracking()
            .Where(refund => refund.ExpenseId == expenseId && refund.UserId == userId)
            .OrderByDescending(refund => refund.RefundedOn)
            .ThenByDescending(refund => refund.RecordedAtUtc)
            .ToListAsync(cancellationToken);

        return refunds.Select(ExpenseMapper.ToDto).ToList();
    }

    private IQueryable<ExpenseRow> Project(IQueryable<Expense> expenses) =>
        expenses.Select(expense => new ExpenseRow(
            expense,
            db.Categories
                .Where(category => category.Id == expense.CategoryId)
                .Select(category => category.Name)
                .FirstOrDefault() ?? string.Empty,
            expense.Refunds
                .Where(refund => refund.CancelledAtUtc == null)
                .Sum(refund => (decimal?)refund.Amount) ?? 0m));

    private static ExpenseDto ToDto(Expense expense, string categoryName, decimal refundedAmount)
    {
        var status = expense.CancelledAtUtc is not null
            ? ExpenseStatus.Cancelled
            : refundedAmount <= 0m
                ? ExpenseStatus.Active
                : refundedAmount >= expense.Amount
                    ? ExpenseStatus.Refunded
                    : ExpenseStatus.PartiallyRefunded;

        return new ExpenseDto(
            expense.Id,
            expense.Amount,
            expense.Currency,
            expense.OccurredOn,
            expense.CategoryId,
            categoryName,
            expense.Description,
            expense.PaymentMethod,
            expense.CardId,
            status,
            refundedAmount,
            expense.Amount - refundedAmount,
            expense.RecordedAtUtc,
            RowVersionCodec.ToBase64(expense.RowVersion));
    }

    private sealed record ExpenseRow(Expense Expense, string CategoryName, decimal RefundedAmount);
}
