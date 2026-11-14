using System.ComponentModel.DataAnnotations;

namespace FinTrack.Api.Identity;

/// <summary>
/// Kimlik sözleşmesi doğrulaması. İş kuralı mesajları Türkçedir; alan adları API ile aynıdır.
/// </summary>
public static class AuthValidation
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    private static readonly EmailAddressAttribute EmailValidator = new();

    public static Dictionary<string, string[]> ValidateRegister(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateEmail(request.Email, errors);
        ValidatePassword(request.Password, errors);
        ValidateTimeZone(request.PreferredTimeZone, required: false, errors);
        return errors;
    }

    public static Dictionary<string, string[]> ValidateLogin(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateEmail(request.Email, errors);

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = ["Parola zorunludur."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateRefreshToken(string? refreshToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            errors["refreshToken"] = ["Refresh token zorunludur."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateUpdateProfile(UpdateProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateTimeZone(request.PreferredTimeZone, required: true, errors);

        if (!TryParseRowVersion(request.RowVersion, out _))
        {
            errors["rowVersion"] = ["Geçerli bir rowVersion zorunludur."];
        }

        return errors;
    }

    public static bool TryParseRowVersion(string? value, out byte[] rowVersion)
    {
        rowVersion = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written != 8)
        {
            return false;
        }

        rowVersion = buffer[..written];
        return true;
    }

    private static void ValidateEmail(string? email, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            errors["email"] = ["E-posta zorunludur."];
            return;
        }

        var trimmed = email.Trim();
        if (trimmed.Length > 256 || !EmailValidator.IsValid(trimmed))
        {
            errors["email"] = ["Geçerli bir e-posta adresi giriniz."];
        }
    }

    private static void ValidatePassword(string? password, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrEmpty(password))
        {
            errors["password"] = ["Parola zorunludur."];
            return;
        }

        if (password.Length < PasswordMinLength || password.Length > PasswordMaxLength)
        {
            errors["password"] = [$"Parola {PasswordMinLength}–{PasswordMaxLength} karakter olmalıdır."];
            return;
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            errors["password"] = ["Parola en az bir harf ve bir rakam içermelidir."];
        }
    }

    private static void ValidateTimeZone(
        string? timeZone,
        bool required,
        Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
        {
            if (required)
            {
                errors["preferredTimeZone"] = ["Saat dilimi zorunludur."];
            }

            return;
        }

        var trimmed = timeZone.Trim();
        if (trimmed.Length > 100 || !TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out _))
        {
            errors["preferredTimeZone"] = ["Geçerli bir IANA saat dilimi kimliği giriniz."];
        }
    }
}
