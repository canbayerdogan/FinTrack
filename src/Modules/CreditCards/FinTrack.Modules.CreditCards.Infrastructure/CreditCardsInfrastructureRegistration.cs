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
        services.AddDbContext<CreditCardsDbContext>((sp, options) =>
            CreditCardsDbContextOptions.UseFinTrackSqlServer(
                options,
                sp.GetRequiredService<IWriteConnectionProvider>().Connection));

        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<ICardQueries, CardQueries>();
        services.AddScoped<ICardPaymentRepository, CardPaymentRepository>();
        services.AddScoped<ICardPaymentQueries, CardPaymentQueries>();
        services.AddScoped<ICashAdvanceRepository, CashAdvanceRepository>();
        services.AddScoped<ICashAdvanceQueries, CashAdvanceQueries>();
        services.AddScoped<IStatementRepository, StatementRepository>();
        services.AddScoped<IStatementQueries, StatementQueries>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IWriteScopeParticipant>(sp =>
            (IWriteScopeParticipant)sp.GetRequiredService<IUnitOfWork>());
        services.AddScoped<ITransactionScopeFactory, TransactionScopeFactory>();
        services.AddScoped<ICardQuery, CardQuery>();
        services.AddScoped<IModuleMigration, CreditCardsModuleMigration>();

        return services;
    }
}
