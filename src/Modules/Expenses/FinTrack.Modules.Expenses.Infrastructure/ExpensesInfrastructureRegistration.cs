using FinTrack.ModuleContracts;
using FinTrack.Modules.Expenses.Application.Abstractions;
using FinTrack.Modules.Expenses.Infrastructure.Persistence;
using FinTrack.Modules.Expenses.Infrastructure.Queries;
using FinTrack.Modules.Expenses.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.Expenses.Infrastructure;

public static class ExpensesInfrastructureRegistration
{
    public static IServiceCollection AddExpensesInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<ExpensesDbContext>((sp, options) =>
            ExpensesDbContextOptions.UseFinTrackSqlServer(
                options,
                sp.GetRequiredService<IWriteConnectionProvider>().Connection));

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();
        services.AddScoped<IExpenseQueries, ExpenseQueries>();
        services.AddScoped<ICardExpenseQuery, CardExpenseQuery>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IWriteScopeParticipant>(sp =>
            (IWriteScopeParticipant)sp.GetRequiredService<IUnitOfWork>());
        services.AddScoped<ITransactionScopeFactory, TransactionScopeFactory>();
        services.AddScoped<IModuleMigration, ExpensesModuleMigration>();

        return services;
    }
}
