using FinTrack.Modules.CashInflows.Application;
using FinTrack.Modules.CashInflows.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.CashInflows.Endpoints;

/// <summary>
/// CashInflows modülünün kompozisyon noktası.
/// Api host'u bu modül hakkında yalnızca bu sınıfı bilir; iç tiplere erişemez.
/// </summary>
public static class CashInflowsModule
{
    public const string Name = "CashInflows";
    public const string RoutePrefix = "/api/v1/cash-inflows";

    public static IServiceCollection AddCashInflowsModule(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddCashInflowsApplication();
        services.AddCashInflowsInfrastructure(connectionString);
        return services;
    }

    public static IEndpointRouteBuilder MapCashInflowsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(RoutePrefix)
            .WithTags(Name)
            .RequireAuthorization();
        CashInflowEndpoints.Map(group);

        return app;
    }
}
