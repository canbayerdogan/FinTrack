using System.Security.Cryptography;
using System.Text;

namespace FinTrack.Api.Idempotency;

/// <summary>
/// docs/11 §3.1 kapsam dizesi ve §3.2 RFC 8785 tabanlı isteği karma hesabı.
/// </summary>
public static class IdempotencyScope
{
    public const string HeaderName = "Idempotency-Key";

    /// <summary>v1.2: RFC 8785 double çakışmalarını önleyen mutlak sayı sınırı.</summary>
    public const decimal MaxAbsoluteNumber = 549_755_813_888m; // 2^39

    public static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public static string UserHash(string userId) => Sha256(userId);

    public static string Compute(
        string userId,
        string method,
        string routeTemplate,
        string canonicalRouteValues,
        string key) =>
        Sha256(string.Join('\n', userId, method.ToUpperInvariant(), routeTemplate, canonicalRouteValues, key));

    public static string CanonicalRouteValues(IReadOnlyDictionary<string, string?> values) =>
        string.Join(',', values
            .Where(pair => pair.Value is not null)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
}
