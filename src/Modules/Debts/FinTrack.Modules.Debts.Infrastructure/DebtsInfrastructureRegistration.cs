using FinTrack.ModuleContracts;
using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.Modules.Debts.Infrastructure.Persistence;
using FinTrack.Modules.Debts.Infrastructure.Queries;
using FinTrack.Modules.Debts.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.Debts.Infrastructure;

public static class DebtsInfrastructureRegistration
{
    public static IServiceCollection AddDebtsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<DebtsDbContext>((sp, options) =>
            DebtsDbContextOptions.UseFinTrackSqlServer(
                options,
                sp.GetRequiredService<IWriteConnectionProvider>().Connection));

        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<ILoanQueries, LoanQueries>();
        services.AddScoped<IPersonalDebtRepository, PersonalDebtRepository>();
        services.AddScoped<IPersonalDebtQueries, PersonalDebtQueries>();
        services.AddScoped<IDebtSummaryQueries, DebtSummaryQueries>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IModuleMigration, DebtsModuleMigration>();

        return services;
    }
}
