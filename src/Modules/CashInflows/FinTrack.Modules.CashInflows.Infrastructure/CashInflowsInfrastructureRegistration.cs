using FinTrack.ModuleContracts;
using FinTrack.Modules.CashInflows.Application.Abstractions;
using FinTrack.Modules.CashInflows.Infrastructure.Persistence;
using FinTrack.Modules.CashInflows.Infrastructure.Queries;
using FinTrack.Modules.CashInflows.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.CashInflows.Infrastructure;

public static class CashInflowsInfrastructureRegistration
{
    public static IServiceCollection AddCashInflowsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<CashInflowsDbContext>(options =>
            CashInflowsDbContextOptions.UseFinTrackSqlServer(options, connectionString));

        services.AddScoped<ICashInflowRepository, CashInflowRepository>();
        services.AddScoped<ICashInflowQueries, CashInflowQueries>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IModuleMigration, CashInflowsModuleMigration>();

        return services;
    }
}
