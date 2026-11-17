using FinTrack.ModuleContracts;
using FinTrack.Modules.CreditCards.Application.Cards;
using FinTrack.Modules.CreditCards.Application.CashAdvances;
using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.Modules.CreditCards.Application.Payments;
using FinTrack.Modules.CreditCards.Application.Reporting;
using FinTrack.Modules.CreditCards.Application.Statements;
using FinTrack.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.CreditCards.Application;

/// <summary>
/// Handler'lar MediatR olmadan DI ile çözülür (ADR-0003). Kayıtlar açıktır;
/// sihirli tarama yoktur, böylece bağımlılıklar kod üzerinden izlenebilir.
/// </summary>
public static class CreditCardsApplicationRegistration
{
    public static IServiceCollection AddCreditCardsApplication(this IServiceCollection services)
    {
        // Ortak hesaplama servisleri.
        services.AddScoped<CardDebtService>();
        services.AddScoped<StatementDerivation>();
        services.AddScoped<ICardReportQuery, CardReportQuery>();

        // Kart.
        services.AddScoped<ICommandHandler<CreateCardCommand, Result<CardDto>>, CreateCardHandler>();
        services.AddScoped<ICommandHandler<UpdateCardCommand, Result<CardDto>>, UpdateCardHandler>();
        services.AddScoped<ICommandHandler<ArchiveCardCommand, Result>, ArchiveCardHandler>();
        services.AddScoped<IQueryHandler<ListCardsQuery, Result<IReadOnlyList<CardDto>>>, ListCardsHandler>();
        services.AddScoped<IQueryHandler<GetCardQuery, Result<CardDto>>, GetCardHandler>();
        services.AddScoped<IQueryHandler<GetCardBalanceQuery, Result<CardBalanceDto>>, GetCardBalanceHandler>();

        // Kart ödemesi.
        services.AddScoped<ICommandHandler<CreateCardPaymentCommand, Result<CardPaymentDto>>, CreateCardPaymentHandler>();
        services.AddScoped<ICommandHandler<CancelCardPaymentCommand, Result>, CancelCardPaymentHandler>();
        services.AddScoped<IQueryHandler<ListCardPaymentsQuery, Result<PagedResult<CardPaymentDto>>>, ListCardPaymentsHandler>();

        // Nakit avans (ADR-0009).
        services.AddScoped<ICommandHandler<CreateCashAdvanceCommand, Result<CashAdvanceDto>>, CreateCashAdvanceHandler>();
        services.AddScoped<ICommandHandler<CancelCashAdvanceCommand, Result>, CancelCashAdvanceHandler>();
        services.AddScoped<IQueryHandler<ListCashAdvancesQuery, Result<PagedResult<CashAdvanceDto>>>, ListCashAdvancesHandler>();

        // Ekstre.
        services.AddScoped<ICommandHandler<CreateStatementCommand, Result<StatementDto>>, CreateStatementHandler>();
        services.AddScoped<ICommandHandler<CancelStatementCommand, Result>, CancelStatementHandler>();
        services.AddScoped<IQueryHandler<ListStatementsQuery, Result<PagedResult<StatementDto>>>, ListStatementsHandler>();
        services.AddScoped<IQueryHandler<GetStatementQuery, Result<StatementDto>>, GetStatementHandler>();

        return services;
    }
}
