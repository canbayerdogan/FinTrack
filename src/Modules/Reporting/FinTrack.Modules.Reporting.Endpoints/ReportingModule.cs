using FinTrack.Modules.Reporting.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.Reporting.Endpoints;

/// <summary>
/// Reporting modülünün kompozisyon noktası. API host'u bu modül hakkında
/// yalnızca bu sınıfı bilir; iç tiplere erişemez.
/// </summary>
public static class ReportingModule
{
    public const string Name = "Reporting";
    public const string RoutePrefix = "/api/v1/reports";

    public static IServiceCollection AddReportingModule(this IServiceCollection services)
    {
        // Reporting'in kendi DbContext'i veya migration'ı yoktur: veri yalnızca
        // ModuleContracts üzerinden okunur (docs/03 §5.5, docs/05 §7.6).
        services.AddReportingApplication();
        return services;
    }

    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(RoutePrefix).WithTags(Name).RequireAuthorization();
        ReportEndpoints.Map(group);
        return app;
    }
}
