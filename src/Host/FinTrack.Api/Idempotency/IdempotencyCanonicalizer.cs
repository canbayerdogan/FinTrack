using System.Text.Json;
using Jcs.Net;

namespace FinTrack.Api.Idempotency;

/// <summary>
/// RFC 8785 (Jcs.Net) kanonikleştirmesi + docs/11 §3.2.1 sayı sınırı doğrulaması.
/// Ham gövde saklanmaz; yalnızca SHA-256 özeti tutulur.
/// </summary>
public static class IdempotencyCanonicalizer
{
    public sealed class InvalidBodyException(string message) : Exception(message);

    public static string ComputeRequestHash(string body)
    {
        string canonical;
        try
        {
            canonical = JsonCanonicalizer.Canonicalize(body);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidBodyException("İstek gövdesi kanonik JSON olarak işlenemedi.");
        }

        try
        {
            using var document = JsonDocument.Parse(canonical);
            ValidateNumbers(document.RootElement);
        }
        catch (JsonException)
        {
            throw new InvalidBodyException("İstek gövdesi geçerli bir JSON değil.");
        }

        return IdempotencyScope.Sha256(canonical);
    }

    private static void ValidateNumbers(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                var number = element.GetDouble();
                if (double.IsNaN(number) || double.IsInfinity(number) ||
                    Math.Abs(number) >= (double)IdempotencyScope.MaxAbsoluteNumber)
                {
                    throw new InvalidBodyException(
                        $"Sayı değeri desteklenen sınırın dışında (|sayı| < {IdempotencyScope.MaxAbsoluteNumber}).");
                }
                break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    ValidateNumbers(property.Value);
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    ValidateNumbers(item);
                }
                break;
        }
    }
}
