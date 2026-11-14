using FinTrack.Modules.CreditCards.Application.Cards;
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
        services.AddScoped<ICommandHandler<CreateCardCommand, Result<CardDto>>, CreateCardHandler>();
        services.AddScoped<ICommandHandler<UpdateCardCommand, Result<CardDto>>, UpdateCardHandler>();
        services.AddScoped<ICommandHandler<ArchiveCardCommand, Result>, ArchiveCardHandler>();
        services.AddScoped<IQueryHandler<ListCardsQuery, Result<IReadOnlyList<CardDto>>>, ListCardsHandler>();
        services.AddScoped<IQueryHandler<GetCardQuery, Result<CardDto>>, GetCardHandler>();
        services.AddScoped<IQueryHandler<GetCardBalanceQuery, Result<CardBalanceDto>>, GetCardBalanceHandler>();

        return services;
    }
}
