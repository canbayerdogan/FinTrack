using System.Globalization;

namespace FinTrack.Mobile.Core.Formatting;

/// <summary>
/// v1 tek para birimi TRY; tutarlar her yerde aynı biçimde gösterilir.
/// </summary>
public static class MoneyFormat
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string Format(decimal amount, string currency = "TRY") =>
        $"{amount.ToString("N2", Tr)} {currency}";

    public static string Signed(decimal amount, string currency = "TRY") =>
        $"{(amount >= 0 ? "+" : string.Empty)}{Format(amount, currency)}";
}
