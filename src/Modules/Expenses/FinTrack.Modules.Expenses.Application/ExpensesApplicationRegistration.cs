using FinTrack.Modules.Expenses.Application.Abstractions;
using FinTrack.Modules.Expenses.Application.Categories;
using FinTrack.Modules.Expenses.Application.Common;
using FinTrack.Modules.Expenses.Application.Expenses;
using FinTrack.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.Expenses.Application;

/// <summary>
/// Handler'lar MediatR olmadan DI ile çözülür (ADR-0003). Kayıtlar açıktır;
/// sihirli tarama yoktur, böylece bağımlılıklar kod üzerinden izlenebilir.
/// </summary>
public static class ExpensesApplicationRegistration
{
    public static IServiceCollection AddExpensesApplication(this IServiceCollection services)
    {
        services.AddScoped<ExpenseReferenceService>();

        services.AddScoped<ICommandHandler<CreateCategoryCommand, Result<CategoryDto>>, CreateCategoryHandler>();
        services.AddScoped<ICommandHandler<UpdateCategoryCommand, Result<CategoryDto>>, UpdateCategoryHandler>();
        services.AddScoped<ICommandHandler<ArchiveCategoryCommand, Result>, ArchiveCategoryHandler>();
        services.AddScoped<IQueryHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>, GetCategoriesHandler>();

        services.AddScoped<ICommandHandler<CreateExpenseCommand, Result<ExpenseDto>>, CreateExpenseHandler>();
        services.AddScoped<ICommandHandler<UpdateExpenseCommand, Result<ExpenseDto>>, UpdateExpenseHandler>();
        services.AddScoped<ICommandHandler<CancelExpenseCommand, Result>, CancelExpenseHandler>();
        services.AddScoped<IQueryHandler<ListExpensesQuery, Result<PagedResult<ExpenseDto>>>, ListExpensesHandler>();
        services.AddScoped<IQueryHandler<GetExpenseQuery, Result<ExpenseDto>>, GetExpenseHandler>();

        services.AddScoped<ICommandHandler<AddRefundCommand, Result<RefundDto>>, AddRefundHandler>();
        services.AddScoped<ICommandHandler<CancelRefundCommand, Result>, CancelRefundHandler>();
        services.AddScoped<IQueryHandler<ListRefundsQuery, Result<IReadOnlyList<RefundDto>>>, ListRefundsHandler>();

        return services;
    }
}
