using FinTrack.Modules.Debts.Domain;

namespace FinTrack.Modules.Debts.Endpoints;

/// <summary>
/// Sorgu parametrelerini sözleşmedeki enum değerlerine çevirir. Bilinmeyen
/// değer sessizce varsayılana düşmez; 400 üretilir.
/// </summary>
internal static class RequestParsing
{
    public static bool TryParseLoanStatus(string? value, out LoanStatus? parsed) =>
        TryParseEnum(value, out parsed);

    public static bool TryParsePersonalDebtStatus(string? value, out PersonalDebtStatus? parsed) =>
        TryParseEnum(value, out parsed);

    private static bool TryParseEnum<TEnum>(string? value, out TEnum? parsed)
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
}
