using System.ComponentModel.DataAnnotations;

namespace FinTrack.Mobile.Core.Validation;

/// <summary>
/// API'deki AuthValidation ile aynı sınırları kullanan istemci tarafı doğrulama.
/// Amaç gereksiz istek göndermemek; yetki her zaman sunucuda doğrulanır.
/// </summary>
public static class InputValidation
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    private static readonly EmailAddressAttribute EmailValidator = new();

    public static string? ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "E-posta zorunludur.";
        }

        var trimmed = email.Trim();
        return trimmed.Length > 256 || !EmailValidator.IsValid(trimmed)
            ? "Geçerli bir e-posta adresi giriniz."
            : null;
    }

    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "Parola zorunludur.";
        }

        if (password.Length is < PasswordMinLength or > PasswordMaxLength)
        {
            return $"Parola {PasswordMinLength}–{PasswordMaxLength} karakter olmalıdır.";
        }

        return !password.Any(char.IsLetter) || !password.Any(char.IsDigit)
            ? "Parola en az bir harf ve bir rakam içermelidir."
            : null;
    }

    public static string? ValidateLoginPassword(string? password) =>
        string.IsNullOrWhiteSpace(password) ? "Parola zorunludur." : null;

    public static string? ValidateTimeZone(string? timeZone, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
        {
            return required ? "Saat dilimi zorunludur." : null;
        }

        var trimmed = timeZone.Trim();
        return trimmed.Length > 100 || !TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out _)
            ? "Geçerli bir IANA saat dilimi kimliği giriniz."
            : null;
    }

    public static string? ValidatePositiveAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Tutar zorunludur.";
        }

        var normalized = text.Trim().Replace(",", ".", StringComparison.Ordinal);
        return decimal.TryParse(
                   normalized,
                   System.Globalization.NumberStyles.Number,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out var amount) &&
               amount > 0
            ? null
            : "Tutar sıfırdan büyük bir sayı olmalıdır.";
    }

    public static bool TryParseAmount(string? text, out decimal amount)
    {
        amount = 0m;
        return !string.IsNullOrWhiteSpace(text) &&
               decimal.TryParse(
                   text.Trim().Replace(",", ".", StringComparison.Ordinal),
                   System.Globalization.NumberStyles.Number,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out amount) &&
               amount > 0;
    }
}
