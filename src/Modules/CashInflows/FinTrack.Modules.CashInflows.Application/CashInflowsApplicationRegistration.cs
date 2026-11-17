using FinTrack.ModuleContracts;
using FinTrack.Modules.CashInflows.Application.CashInflows;
using FinTrack.Modules.CashInflows.Application.Common;
using FinTrack.Modules.CashInflows.Application.Reporting;
using FinTrack.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.CashInflows.Application;

/// <summary>
/// Handler'lar MediatR olmadan DI ile çözülür (ADR-0003). Kayıtlar açıktır;
/// sihirli tarama yoktur, böylece bağımlılıklar kod üzerinden izlenebilir.
/// </summary>
public static class CashInflowsApplicationRegistration
{
    public static IServiceCollection AddCashInflowsApplication(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<CreateCashInflowCommand, Result<CashInflowDto>>, CreateCashInflowHandler>();
        services.AddScoped<ICommandHandler<UpdateCashInflowCommand, Result<CashInflowDto>>, UpdateCashInflowHandler>();
        services.AddScoped<ICommandHandler<CancelCashInflowCommand, Result>, CancelCashInflowHandler>();
        services.AddScoped<IQueryHandler<ListCashInflowsQuery, Result<PagedResult<CashInflowDto>>>, ListCashInflowsHandler>();
        services.AddScoped<IQueryHandler<GetCashInflowQuery, Result<CashInflowDto>>, GetCashInflowHandler>();

        services.AddScoped<ICashInflowReportQuery, CashInflowReportQuery>();

        // ADR-0009: borç sahibi modüllerin kullandığı yazma sözleşmesi.
        services.AddScoped<IDebtCashInflowWriter, DebtCashInflowWriter>();

        return services;
    }
}
