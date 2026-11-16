using System.Globalization;
using FinTrack.Modules.CashInflows.Domain;

namespace FinTrack.Modules.CashInflows.Endpoints;

/// <summary>
/// Sorgu/istek metinlerini sözleşmedeki enum ve tarih biçimlerine çevirir.
/// Bilinmeyen değer sessizce varsayılana düşmez; 400 üretilir.
/// </summary>
internal static class RequestParsing
{
    public static bool TryParseSource(string? value, out CashInflowSource? parsed) =>
        TryParseEnum(value, out parsed);

    public static bool TryParseStatus(string? value, out CashInflowStatus? parsed) =>
        TryParseEnum(value, out parsed);

    public static bool TryParseEnum<TEnum>(string? value, out TEnum? parsed)
        where TEnum : struct, Enum
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var candidate)
            && Enum.IsDefined(candidate))
        {
            parsed = candidate;
            return true;
        }

        return false;
    }

    public static bool TryParseDate(string? value, out DateOnly? parsed)
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (DateOnly.TryParseExact(
                value.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            parsed = date;
            return true;
        }

        return false;
    }
}
