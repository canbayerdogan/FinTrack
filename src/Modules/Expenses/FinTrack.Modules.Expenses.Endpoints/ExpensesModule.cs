using FinTrack.Modules.Expenses.Application;
using FinTrack.Modules.Expenses.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.Expenses.Endpoints;

/// <summary>
/// Expenses modülünün kompozisyon noktası.
/// Api host'u bu modül hakkında yalnızca bu sınıfı bilir; iç tiplere erişemez.
/// </summary>
public static class ExpensesModule
{
    public const string Name = "Expenses";
    public const string RoutePrefix = "/api/v1/expenses";
    public const string CategoriesRoutePrefix = "/api/v1/categories";

    public static IServiceCollection AddExpensesModule(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddExpensesApplication();
        services.AddExpensesInfrastructure(connectionString);
        return services;
    }

    public static IEndpointRouteBuilder MapExpensesEndpoints(this IEndpointRouteBuilder app)
    {
        var categories = app.MapGroup(CategoriesRoutePrefix)
            .WithTags(Name)
            .RequireAuthorization();
        CategoryEndpoints.Map(categories);

        var expenses = app.MapGroup(RoutePrefix)
            .WithTags(Name)
            .RequireAuthorization();
        ExpenseEndpoints.Map(expenses);
        RefundEndpoints.Map(expenses);

        return app;
    }
}
