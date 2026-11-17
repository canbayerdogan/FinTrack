using FinTrack.Modules.Debts.Application;
using FinTrack.Modules.Debts.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.Debts.Endpoints;

/// <summary>
/// Debts modülünün kompozisyon noktası.
/// Api host'u bu modül hakkında yalnızca bu sınıfı bilir; iç tiplere erişemez.
/// </summary>
public static class DebtsModule
{
    public const string Name = "Debts";
    public const string LoanRoutePrefix = "/api/v1/loans";
    public const string PersonalDebtRoutePrefix = "/api/v1/personal-debts";

    public static IServiceCollection AddDebtsModule(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDebtsApplication();
        services.AddDebtsInfrastructure(connectionString);
        return services;
    }

    public static IEndpointRouteBuilder MapDebtsEndpoints(this IEndpointRouteBuilder app)
    {
        var loans = app.MapGroup(LoanRoutePrefix).WithTags(Name).RequireAuthorization();
        LoanEndpoints.Map(loans);

        var personalDebts = app.MapGroup(PersonalDebtRoutePrefix).WithTags(Name).RequireAuthorization();
        PersonalDebtEndpoints.Map(personalDebts);

        return app;
    }
}
