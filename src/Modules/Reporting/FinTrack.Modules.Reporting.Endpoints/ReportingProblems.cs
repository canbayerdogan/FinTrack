using System.Diagnostics;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Modules.Reporting.Endpoints;

/// <summary>
/// Raporlama hatalarını docs/06 §1.2–1.3 ProblemDetails sözleşmesine çevirir.
/// </summary>
internal static class ReportingProblems
{
    public static IResult FromError(HttpContext context, Error error)
    {
        var problem = new ProblemDetails
        {
            Type = "https://fintrack.local/errors/validation",
            Title = "Doğrulama hatası",
            Status = StatusCodes.Status400BadRequest,
            Detail = error.Message,
            Instance = context.Request.Path,
        };

        problem.Extensions["code"] = "ValidationFailed";
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        if (TryResolveField(error.Code) is { } field)
        {
            problem.Extensions["errors"] = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [field] = [error.Message],
            };
        }

        return Results.Problem(problem);
    }

    public static IResult Validation(HttpContext context, IReadOnlyDictionary<string, string[]> errors)
    {
        var problem = new ProblemDetails
        {
            Type = "https://fintrack.local/errors/validation",
            Title = "Doğrulama hatası",
            Status = StatusCodes.Status400BadRequest,
            Detail = "İstek doğrulanamadı.",
            Instance = context.Request.Path,
        };

        problem.Extensions["code"] = "ValidationFailed";
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        problem.Extensions["errors"] = errors;

        return Results.Problem(problem);
    }

    private static string? TryResolveField(string code) => code switch
    {
        "InvalidYear" => "year",
        "InvalidMonth" => "month",
        "InvalidDays" => "days",
        _ => null,
    };
}
