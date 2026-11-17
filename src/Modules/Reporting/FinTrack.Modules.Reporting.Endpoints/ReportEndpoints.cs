using FinTrack.Modules.Reporting.Application.Reporting;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FinTrack.Modules.Reporting.Endpoints;

/// <summary>
/// Raporlama uç noktaları (docs/06 §8). Salt okunur: hiçbir kayıt oluşturmaz.
/// </summary>
internal static class ReportEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/monthly", async (
                int? year,
                int? month,
                IQueryHandler<GetMonthlyReportQuery, Result<MonthlyReportDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

                if (year is null or < 2000 or > 2100)
                {
                    errors["year"] = ["year 2000 ile 2100 arasında olmalıdır."];
                }

                if (month is null or < 1 or > 12)
                {
                    errors["month"] = ["month 1 ile 12 arasında olmalıdır."];
                }

                if (errors.Count > 0)
                {
                    return ReportingProblems.Validation(context, errors);
                }

                var result = await handler.HandleAsync(
                    new GetMonthlyReportQuery(year!.Value, month!.Value),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ReportingProblems.FromError(context, result.Error);
            })
            .WithName("GetMonthlyReport")
            .WithSummary("Takvim ayı için gelir/gider özeti ve kategori kırılımı döner.");

        group.MapGet("/debt-summary", async (
                IQueryHandler<GetDebtSummaryQuery, Result<DebtSummaryDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new GetDebtSummaryQuery(), cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ReportingProblems.FromError(context, result.Error);
            })
            .WithName("GetDebtSummary")
            .WithSummary("Kart, kredi ve kişisel borç toplamını döner (salt okunur).");

        group.MapGet("/upcoming-payments", async (
                int? days,
                IQueryHandler<GetUpcomingPaymentsQuery, Result<UpcomingPaymentsDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var resolvedDays = days ?? 30;
                if (resolvedDays is < 1 or > 90)
                {
                    var errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["days"] = ["days 1 ile 90 arasında olmalıdır."],
                    };

                    return ReportingProblems.Validation(context, errors);
                }

                var result = await handler.HandleAsync(
                    new GetUpcomingPaymentsQuery(resolvedDays),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ReportingProblems.FromError(context, result.Error);
            })
            .WithName("GetUpcomingPayments")
            .WithSummary("Yaklaşan kredi taksitleri ve kart ekstre vadelerini bilgi amaçlı listeler.");
    }
}
