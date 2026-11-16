using FinTrack.ModuleContracts;
using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Infrastructure.Persistence;
using FinTrack.Modules.CreditCards.Infrastructure.Queries;
using FinTrack.Modules.CreditCards.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.CreditCards.Infrastructure;

public static class CreditCardsInfrastructureRegistration
{
    public static IServiceCollection AddCreditCardsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<CreditCardsDbContext>(options =>
            CreditCardsDbContextOptions.UseFinTrackSqlServer(options, connectionString));

        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<ICardQueries, CardQueries>();
        services.AddScoped<ICardPaymentRepository, CardPaymentRepository>();
        services.AddScoped<ICardPaymentQueries, CardPaymentQueries>();
        services.AddScoped<IStatementRepository, StatementRepository>();
        services.AddScoped<IStatementQueries, StatementQueries>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITransactionScopeFactory, TransactionScopeFactory>();
        services.AddScoped<ICardQuery, CardQuery>();
        services.AddScoped<IModuleMigration, CreditCardsModuleMigration>();

        return services;
    }
}
