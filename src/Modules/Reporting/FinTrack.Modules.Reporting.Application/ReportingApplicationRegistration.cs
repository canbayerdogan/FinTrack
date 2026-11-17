using FinTrack.Modules.Reporting.Application.Reporting;
using FinTrack.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.Reporting.Application;

/// <summary>
/// Reporting salt okunur olduğu için handler'lar doğrudan DI ile kaydedilir
/// (ADR-0003). Veri erişimi yalnızca ModuleContracts sözleşmeleri üzerindendir.
/// </summary>
public static class ReportingApplicationRegistration
{
    public static IServiceCollection AddReportingApplication(this IServiceCollection services)
    {
        services.AddScoped<
            IQueryHandler<GetMonthlyReportQuery, Result<MonthlyReportDto>>,
            GetMonthlyReportHandler>();
        services.AddScoped<
            IQueryHandler<GetDebtSummaryQuery, Result<DebtSummaryDto>>,
            GetDebtSummaryHandler>();
        services.AddScoped<
            IQueryHandler<GetUpcomingPaymentsQuery, Result<UpcomingPaymentsDto>>,
            GetUpcomingPaymentsHandler>();

        return services;
    }
}
