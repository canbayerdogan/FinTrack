using System.Globalization;
using FinTrack.Modules.Expenses.Domain;

namespace FinTrack.Modules.Expenses.Endpoints;

/// <summary>
/// Sorgu/istek metinlerini sözleşmedeki enum ve tarih biçimlerine çevirir.
/// Bilinmeyen değer sessizce varsayılana düşmez; 400 üretilir.
/// </summary>
internal static class RequestParsing
{
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

    public static bool TryParseGuid(string? value, out Guid? parsed)
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Guid.TryParse(value.Trim(), out var guid))
        {
            parsed = guid;
            return true;
        }

        return false;
    }

    public static bool TryParsePaymentMethod(string? value, out PaymentMethod? parsed) =>
        TryParseEnum(value, out parsed);

    public static bool TryParseStatus(string? value, out ExpenseStatus? parsed) =>
        TryParseEnum(value, out parsed);
}
