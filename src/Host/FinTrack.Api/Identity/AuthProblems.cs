using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace FinTrack.Api.Identity;

/// <summary>
/// API sözleşmesindeki <c>ProblemDetails</c> gövdelerini tek yerden üretir.
/// </summary>
internal static class AuthProblems
{
    private const string BusinessRuleType = "https://fintrack.local/errors/business-rule";
    private const string ValidationType = "https://fintrack.local/errors/validation";
    private const string UnauthorizedType = "https://fintrack.local/errors/unauthorized";
    private const string ConflictType = "https://fintrack.local/errors/conflict";
    private const string NotFoundType = "https://fintrack.local/errors/not-found";

    public static IResult Validation(HttpContext context, IDictionary<string, string[]> errors) =>
        Results.ValidationProblem(
            errors,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Doğrulama hatası",
            type: ValidationType,
            instance: context.Request.Path,
            extensions: Extensions(context, "ValidationFailed"));

    public static IResult InvalidCredentials(HttpContext context) =>
        Problem(
            context,
            StatusCodes.Status401Unauthorized,
            "InvalidCredentials",
            "Kimlik doğrulama başarısız",
            "E-posta veya parola hatalı.",
            UnauthorizedType);

    public static IResult InvalidRefreshToken(HttpContext context) =>
        Problem(
            context,
            StatusCodes.Status401Unauthorized,
            "InvalidRefreshToken",
            "Refresh token geçersiz",
            "Refresh token geçersiz veya süresi dolmuş.",
            UnauthorizedType);

    public static IResult Unauthorized(HttpContext context) =>
        Problem(
            context,
            StatusCodes.Status401Unauthorized,
            "Unauthorized",
            "Kimlik doğrulama gerekli",
            "Geçerli bir erişim token'ı gönderilmelidir.",
            UnauthorizedType);

    public static IResult Forbidden(HttpContext context) =>
        Problem(
            context,
            StatusCodes.Status403Forbidden,
            "Forbidden",
            "Erişim reddedildi",
            "Bu işlem için yetkiniz yok.",
            UnauthorizedType);

    public static IResult EmailAlreadyRegistered(HttpContext context) =>
        Problem(
            context,
            StatusCodes.Status409Conflict,
            "EmailAlreadyRegistered",
            "E-posta zaten kayıtlı",
            "Bu e-posta adresiyle daha önce kayıt olunmuş.",
            ConflictType);

    public static IResult ConcurrencyConflict(HttpContext context) =>
        Problem(
            context,
            StatusCodes.Status409Conflict,
            "ConcurrencyConflict",
            "Eşzamanlılık çakışması",
            "Kayıt bu sırada değişti. Güncel veriyi yükleyip tekrar deneyin.",
            ConflictType);

    public static IResult NotFound(HttpContext context) =>
        Problem(
            context,
            StatusCodes.Status404NotFound,
            "NotFound",
            "Kayıt bulunamadı",
            "İstenen kayıt bulunamadı.",
            NotFoundType);

    private static IResult Problem(
        HttpContext context,
        int statusCode,
        string code,
        string title,
        string detail,
        string type) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: detail,
            type: type,
            instance: context.Request.Path,
            extensions: Extensions(context, code));

    private static Dictionary<string, object?> Extensions(HttpContext context, string code) =>
        new()
        {
            ["code"] = code,
            ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier,
        };
}
