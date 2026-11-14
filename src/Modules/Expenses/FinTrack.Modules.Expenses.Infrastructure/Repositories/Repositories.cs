using FinTrack.Modules.Expenses.Application.Abstractions;
using FinTrack.Modules.Expenses.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Expenses.Infrastructure.Repositories;

internal sealed class CategoryRepository(ExpensesDbContext db) : ICategoryRepository
{
    public Task<Category?> FindAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken) =>
        db.Categories.FirstOrDefaultAsync(
            category => category.Id == categoryId && category.UserId == userId,
            cancellationToken);

    public Task<bool> NameExistsAsync(
        Guid userId,
        string normalizedName,
        Guid? excludeCategoryId,
        CancellationToken cancellationToken) =>
        db.Categories.AnyAsync(
            category => category.UserId == userId
                && category.NormalizedName == normalizedName
                && (excludeCategoryId == null || category.Id != excludeCategoryId),
            cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken) =>
        await db.Categories.AddAsync(category, cancellationToken);
}

internal sealed class ExpenseRepository(ExpensesDbContext db) : IExpenseRepository
{
    public Task<Expense?> FindAsync(Guid userId, Guid expenseId, CancellationToken cancellationToken) =>
        db.Expenses
            .Include(expense => expense.Refunds)
            .FirstOrDefaultAsync(
                expense => expense.Id == expenseId && expense.UserId == userId,
                cancellationToken);

    public async Task AddAsync(Expense expense, CancellationToken cancellationToken) =>
        await db.Expenses.AddAsync(expense, cancellationToken);

    public async Task AddRefundAsync(Refund refund, CancellationToken cancellationToken) =>
        await db.Refunds.AddAsync(refund, cancellationToken);
}
