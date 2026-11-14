using System.Diagnostics;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Modules.CreditCards.Endpoints;

/// <summary>
/// Modül hatalarını docs/06 §1.2–1.3 sözleşmesine göre ProblemDetails'e çevirir.
/// Ortak bir projeye taşımak için üçüncü tekrar beklenir; şimdilik modül içidir.
/// </summary>
internal static class CreditCardsProblems
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

    public static IResult Validation(HttpContext context, string field, string message) =>
        Validation(context, new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] });

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

            _ => (
                StatusCodes.Status400BadRequest,
                "ValidationFailed",
                "Doğrulama hatası",
                "https://fintrack.local/errors/validation"),
        };

    private static string? TryResolveField(string code) => code switch
    {
        "UserRequired" => "user",
        "CardBankNameRequired" or "CardBankNameTooLong" => "bankName",
        "CardDisplayNameRequired" or "CardDisplayNameTooLong" => "displayName",
        "CardLogoKeyTooLong" => "logoKey",
        "CardCreditLimitInvalid" or "CardCreditLimitScale" or "CardCreditLimitTooLarge" => "creditLimit",
        "CardStatementDayRange" => "statementDay",
        "CardPaymentDueDayRange" => "paymentDueDay",
        "RowVersionInvalid" => "rowVersion",
        _ => null,
    };
}
