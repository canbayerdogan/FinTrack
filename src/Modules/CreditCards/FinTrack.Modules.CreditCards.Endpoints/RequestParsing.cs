using System.Globalization;
using FinTrack.Modules.CreditCards.Domain;

namespace FinTrack.Modules.CreditCards.Endpoints;

/// <summary>
/// Sorgu/istek metinlerini sözleşmedeki enum ve tarih biçimlerine çevirir.
/// Bilinmeyen değer sessizce varsayılana düşmez; 400 üretilir.
/// </summary>
internal static class RequestParsing
{
    public static bool TryParseCardStatus(string? value, out CardStatus? parsed) =>
        TryParseEnum(value, out parsed);

    public static bool TryParsePaymentStatus(string? value, out PaymentStatus? parsed) =>
        TryParseEnum(value, out parsed);

    public static bool TryParseCashAdvanceStatus(string? value, out CashAdvanceStatus? parsed) =>
        TryParseEnum(value, out parsed);

    public static bool TryParseStatementStatus(string? value, out StatementStatus? parsed) =>
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
