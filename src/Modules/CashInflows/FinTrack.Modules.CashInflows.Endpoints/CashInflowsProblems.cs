using System.Diagnostics;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Modules.CashInflows.Endpoints;

/// <summary>
/// Modül hatalarını docs/06 §1.2–1.3 sözleşmesine göre ProblemDetails'e çevirir.
/// Ortak bir projeye taşımak için üçüncü tekrar beklenir; şimdilik modül içidir.
/// </summary>
internal static class CashInflowsProblems
{
    public static IResult FromError(HttpContext context, Error error)
    {
        var (status, code, title, type) = Resolve(error);
        var problem = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = status,
            Detail = error.Message,
            Instance = context.Request.Path,
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        if (status == StatusCodes.Status400BadRequest && TryResolveField(error.Code) is { } field)
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

    private static (int Status, string Code, string Title, string Type) Resolve(Error error) =>
        error.Type switch
        {
            ErrorType.NotFound => (
                StatusCodes.Status404NotFound,
                "NotFound",
                "Kayıt bulunamadı",
                "https://fintrack.local/errors/not-found"),

            ErrorType.Forbidden => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "Yetkisiz işlem",
                "https://fintrack.local/errors/forbidden"),

            ErrorType.Conflict when error.Code == "ConcurrencyConflict" => (
                StatusCodes.Status409Conflict,
                "ConcurrencyConflict",
                "Eşzamanlılık çakışması",
                "https://fintrack.local/errors/concurrency"),

            ErrorType.Conflict => (
                StatusCodes.Status409Conflict,
                "StateConflict",
                "Durum çakışması",
                "https://fintrack.local/errors/state-conflict"),

            ErrorType.Validation when error.Code == "DebtSourceRequiresOwnerEndpoint" => (
                StatusCodes.Status400BadRequest,
                "DebtSourceRequiresOwnerEndpoint",
                "Borç doğuran kaynak",
                "https://fintrack.local/errors/owner-endpoint-required"),

            ErrorType.Validation when error.Code.Contains("Currency", StringComparison.Ordinal) => (
                StatusCodes.Status400BadRequest,
                "UnsupportedCurrency",
                "Desteklenmeyen para birimi",
                "https://fintrack.local/errors/unsupported-currency"),

            _ => (
                StatusCodes.Status400BadRequest,
                "ValidationFailed",
                "Doğrulama hatası",
                "https://fintrack.local/errors/validation"),
        };

    private static string? TryResolveField(string code) => code switch
    {
        "UserRequired" => "user",
        "CashInflowAmountInvalid" or "CashInflowAmountScale" or "CashInflowAmountTooLarge" => "amount",
        "CashInflowCurrencyUnsupported" => "currency",
        "CashInflowDateRequired" or "CashInflowDateInFuture" => "occurredOn",
        "CashInflowSourceRequired" or "CashInflowSourceInvalid"
            or "DebtSourceRequiresOwnerEndpoint" => "source",
        "CashInflowDescriptionTooLong" => "description",
        "CancellationReasonRequired" or "CancellationReasonTooLong" => "reason",
        "RowVersionInvalid" => "rowVersion",
        _ => null,
    };
}
