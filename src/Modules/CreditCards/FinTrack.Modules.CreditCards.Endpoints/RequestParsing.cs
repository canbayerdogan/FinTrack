using FinTrack.Modules.CreditCards.Domain;

namespace FinTrack.Modules.CreditCards.Endpoints;

/// <summary>
/// Sorgu/istek metinlerini sözleşmedeki enum biçimlerine çevirir.
/// Bilinmeyen değer sessizce varsayılana düşmez; 400 üretilir.
/// </summary>
internal static class RequestParsing
{
    public static bool TryParseCardStatus(string? value, out CardStatus? parsed)
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse<CardStatus>(value.Trim(), ignoreCase: true, out var candidate)
            && Enum.IsDefined(candidate))
        {
            parsed = candidate;
            return true;
        }

        return false;
    }
}
