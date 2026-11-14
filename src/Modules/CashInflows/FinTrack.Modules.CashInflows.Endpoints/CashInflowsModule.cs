using FinTrack.ModuleContracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.CashInflows.Endpoints;

/// <summary>
/// CashInflows modÃ¼lÃ¼nÃ¼n kompozisyon noktasÄ±.
/// Api host'u bu modÃ¼l hakkÄ±nda yalnÄ±zca bu sÄ±nÄ±fÄ± bilir; iÃ§ tiplere eriÅŸemez.
/// </summary>
public static class CashInflowsModule
{
    public const string Name = "CashInflows";
    public const string RoutePrefix = "/api/v1/cash-inflows";

    public static IServiceCollection AddCashInflowsModule(this IServiceCollection services)
    {
        // TODO(slice-5): DbContext, handler'lar, validator'lar, modÃ¼l iÃ§i servisler.
        return services;
    }

    public static IEndpointRouteBuilder MapCashInflowsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(RoutePrefix).WithTags(Name);

        // GeÃ§ici iskelet uÃ§ noktasÄ±. TODO(slice-5): dilim tamamlanÄ±nca kaldÄ±r.
        // TODO(slice-1): RequireAuthorization() eklenmeli.
        group.MapGet("/_status", () => Results.Ok(new ModuleStatus(Name)))
            .WithName("CashInflowsStatus")
            .WithSummary("ModÃ¼lÃ¼n ayakta olduÄŸunu doÄŸrular (geÃ§ici).");

        return app;
    }
}
