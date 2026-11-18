using System.Diagnostics;

namespace FinTrack.Api.Idempotency;

/// <summary> Idempotency hata gövdelerini tek yerden üretir (docs/11 §4.2). </summary>
internal static class IdempotencyProblems
{
    private const string ValidationType = "https://fintrack.local/errors/validation";
    private const string ConflictType = "https://fintrack.local/errors/conflict";
    private const string InternalType = "https://fintrack.local/errors/internal";

    public static IResult Validation(HttpContext context, string field, string message) =>
        Results.ValidationProblem(
            new Dictionary<string, string[]> { [field] = [message] },
            statusCode: StatusCodes.Status400BadRequest,
            title: "Doğrulama hatası",
            type: ValidationType,
            instance: context.Request.Path,
            extensions: Extensions(context, "ValidationFailed"));

    public static IResult KeyReused(HttpContext context) =>
        Problem(
            context,
            StatusCodes.Status409Conflict,
            "IdempotencyKeyReused",
            "Anahtar farklı gövdeyle kullanıldı",
            "Bu Idempotency-Key daha önce farklı bir istek gövdesiyle kullanıldı. Yeni bir anahtar kullanın.",
            ConflictType);

    public static IResult InProgress(HttpContext context) =>
        Problem(
            context,
            StatusCodes.Status409Conflict,
            "IdempotencyInProgress",
            "İstek hâlâ işleniyor",
            "Aynı anahtarlı istek hâlâ işleniyor. Aynı anahtar ve gövdeyle güvenle tekrar deneyin.",
            ConflictType);

    public static IResult Expired(HttpContext context) =>
        Problem(
            context,
            StatusCodes.Status409Conflict,
            "IdempotencyExpired",
            "İstek kaydının süresi doldu",
            "24 saatlik saklama süresi doldu. Yeni işlem otomatik açılmaz; durum sorgusunu kullanın.",
            ConflictType);

    public static IResult ResponseTooLarge(HttpContext context) =>
        Problem(
            context,
            StatusCodes.Status500InternalServerError,
            "InternalError",
            "Yanıt sözleşmesi ihlali",
            "Endpoint yanıtı 64 KB replay sınırını aştı; işlem kaydedilmedi.",
            InternalType);

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

    internal static Dictionary<string, object?> Extensions(HttpContext context, string code) =>
        new()
        {
            ["code"] = code,
            ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier,
        };
}
