using FinTrack.Modules.CreditCards.Application;
using FinTrack.Modules.CreditCards.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.CreditCards.Endpoints;

/// <summary>
/// CreditCards modülünün kompozisyon noktası.
/// Api host'u bu modül hakkında yalnızca bu sınıfı bilir; iç tiplere erişemez.
/// </summary>
public static class CreditCardsModule
{
    public const string Name = "CreditCards";
    public const string RoutePrefix = "/api/v1/cards";

    public static IServiceCollection AddCreditCardsModule(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddCreditCardsApplication();
        services.AddCreditCardsInfrastructure(connectionString);
        return services;
    }

    public static IEndpointRouteBuilder MapCreditCardsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(RoutePrefix)
            .WithTags(Name)
            .RequireAuthorization();
        CardEndpoints.Map(group);
        CardPaymentEndpoints.Map(group);
        StatementEndpoints.Map(group);

        return app;
    }
}
